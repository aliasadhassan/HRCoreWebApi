namespace HR.Shared.Library.Persistence;

using System.Collections;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

public enum AuditAction : byte { Created = 1, Updated = 2, Deleted = 3 }

/// <summary>Ek field ka badlaav. Json mein chhote naam (f/o/n) — har row mein bohot saare hote hain.</summary>
public sealed record AuditFieldChange(string Field, string? Old, string? New);

/// <summary>SaveChanges se pehle ChangeTracker ki ek entry ka khulasa — service isay apni AuditLogs table mein likhti hai.</summary>
public sealed record AuditCapture(
    string EntityType, Guid EntityId, AuditAction Action, string? Label, Guid? SubjectEmployeeId, IReadOnlyList<AuditFieldChange> Changes);

/// <summary>
/// Audit page ka "data changes": har SaveChanges mein kya bana / badla / hata — purani aur nayi value ke saath.
/// ApplyAuditRules se PEHLE chalao (soft delete abhi Deleted state mein ho, audit fields abhi na badle hon).
/// Password / token jaisi cheezen kabhi value ke saath nahi likhi jatin; bank account sirf aakhri 4.
/// </summary>
public static class AuditTrail
{
    public const int MaxValueLength = 500;
    public const string Hidden = "••••";

    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "TenantId", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "RowVersion", "xmin", "IsDeleted", "DomainEvents",
        "LastSyncedAt"   // Employee API se har sync pe badalta hai — shor
    };

    private static readonly string[] Secret = ["password", "hash", "token", "secret", "securitystamp"];
    private static readonly string[] LastFour = ["iban", "accountnumber", "accountno"];

    /// <summary>Row ka insani naam — pehli bhari hui string inme se.</summary>
    private static readonly string[] LabelProperties =
        ["FullName", "DisplayName", "Name", "Title", "Subject", "Code", "EmployeeCode", "Email", "AssetTag", "ReferenceNo"];

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <param name="include">Kaun si CLR types audit hon (AuditLog khud, outbox wagaira nahi).</param>
    /// <param name="isEmployee">Ye type khud employee hai (Employee / PayrollEmployee) — subject = apna Id.</param>
    /// <param name="skipFields">Type-specific shor (LastLoginAt jaisi cheezen) jo audit mein nahi chahiye.</param>
    public static List<AuditCapture> Capture(
        ChangeTracker changeTracker, Func<Type, bool> include, Func<Type, bool>? isEmployee = null,
        Func<Type, string, bool>? skipFields = null)
    {
        var result = new List<AuditCapture>();
        foreach (var entry in changeTracker.Entries())
        {
            if (CaptureEntry(entry, include, isEmployee, skipFields) is { } capture)
                result.Add(capture);
        }
        return result;
    }

    private static AuditCapture? CaptureEntry(
        EntityEntry entry, Func<Type, bool> include, Func<Type, bool>? isEmployee, Func<Type, string, bool>? skipFields)
    {
        if (entry.State is EntityState.Unchanged or EntityState.Detached)
            return null;

        var clr = entry.Metadata.ClrType;
        var ownership = entry.Metadata.IsOwned() ? entry.Metadata.FindOwnership() : null;
        // Owner ke soft delete pe owned (Address) bhi Deleted dikhta hai — shor
        if (ownership is not null && entry.State == EntityState.Deleted)
            return null;
        if (!include(ownership?.PrincipalEntityType.ClrType ?? clr))
            return null;

        var action = ActionOf(entry);
        var changes = action == AuditAction.Deleted ? [] : ChangesOf(entry, action, clr, skipFields);
        // Sirf audit fields / xmin badle (koi asli badlaav nahi) — likhne ki zaroorat nahi
        if (action == AuditAction.Updated && changes.Count == 0)
            return null;

        if (ownership is not null)
        {
            var ownerId = ownership.Properties.Select(fk => entry.Property(fk.Name).CurrentValue).OfType<Guid>().FirstOrDefault();
            var type = $"{ownership.PrincipalEntityType.ClrType.Name}.{ownership.PrincipalToDependent?.Name}";
            return new AuditCapture(type, ownerId, action, null, GuidOf(entry, "EmployeeId"), changes);
        }

        // "Id" na ho (TenantSettings) to primary key ka pehla Guid
        var key = entry.Metadata.FindPrimaryKey()?.Properties ?? [];
        var id = key.Select(k => entry.Property(k.Name).CurrentValue).OfType<Guid>().FirstOrDefault();
        var subject = isEmployee?.Invoke(clr) == true ? id : GuidOf(entry, "EmployeeId");
        return new AuditCapture(clr.Name, id, action, LabelOf(entry), subject, changes);
    }

    private static AuditAction ActionOf(EntityEntry entry) => entry.State switch
    {
        EntityState.Added => AuditAction.Created,
        EntityState.Deleted => AuditAction.Deleted,
        _ => IsSoftDelete(entry) ? AuditAction.Deleted : AuditAction.Updated
    };

    private static List<AuditFieldChange> ChangesOf(
        EntityEntry entry, AuditAction action, Type clr, Func<Type, string, bool>? skipFields)
    {
        var changes = new List<AuditFieldChange>();
        foreach (var p in entry.Properties)
        {
            var name = p.Metadata.Name;
            if (p.Metadata.IsShadowProperty() || Skipped.Contains(name) || skipFields?.Invoke(clr, name) == true)
                continue;

            if (action == AuditAction.Created && p.CurrentValue is not (null or string { Length: 0 }))
                changes.Add(new AuditFieldChange(name, null, Format(name, p.CurrentValue)));
            else if (action == AuditAction.Updated && p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
                changes.Add(new AuditFieldChange(name, Format(name, p.OriginalValue), Format(name, p.CurrentValue)));
        }
        return changes;
    }

    public static string Serialize(IReadOnlyList<AuditFieldChange> changes)
        => JsonSerializer.Serialize(changes.Select(c => new Row(c.Field, c.Old, c.New)), Json);

    public static IReadOnlyList<AuditFieldChange> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return (JsonSerializer.Deserialize<List<Row>>(json, Json) ?? []).Select(r => new AuditFieldChange(r.F, r.O, r.N)).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record Row(string F, string? O, string? N);

    private static bool IsSoftDelete(EntityEntry entry)
    {
        var p = entry.Properties.FirstOrDefault(x => x.Metadata.Name == "IsDeleted");
        return p is { IsModified: true, CurrentValue: true, OriginalValue: false };
    }

    private static string? LabelOf(EntityEntry entry)
    {
        foreach (var name in LabelProperties)
        {
            var p = entry.Properties.FirstOrDefault(x => x.Metadata.Name == name);
            if (p?.CurrentValue is string { Length: > 0 } s)
                return s.Length > 200 ? s[..200] : s;
            if (name == "FullName" && Text(entry, "FirstName") is { } first)   // Employee: FullName mapped nahi
            {
                var full = $"{first} {Text(entry, "LastName")}".Trim();
                return full.Length > 200 ? full[..200] : full;
            }
        }
        return null;
    }

    private static string? Text(EntityEntry entry, string name)
        => entry.Properties.FirstOrDefault(x => x.Metadata.Name == name)?.CurrentValue as string is { Length: > 0 } s ? s : null;

    private static Guid? GuidOf(EntityEntry entry, string name)
        => entry.Properties.FirstOrDefault(x => x.Metadata.Name == name)?.CurrentValue as Guid?;

    public static string? Format(string field, object? value)
    {
        if (value is null)
            return null;

        var lower = field.ToLowerInvariant();
        if (value is not bool && Secret.Any(lower.Contains))   // MustChangePassword = true/false, secret nahi
            return Hidden;

        var text = value switch
        {
            string s => s,
            DateTime d => d.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly t => t.ToString("HH:mm", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            Enum e => e.ToString(),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            IEnumerable list => JsonSerializer.Serialize(list, Json),
            _ => value.ToString()
        };
        if (text is null)
            return null;

        if (LastFour.Any(lower.Contains) && text.Length > 4)
            text = new string('•', 4) + text[^4..];

        return text.Length > MaxValueLength ? text[..MaxValueLength] + "…" : text;
    }
}
