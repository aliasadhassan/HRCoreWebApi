namespace HR.Employee.API.Tests.Audit;

using HR.Shared.Library.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class AuditTrailTests
{
    sealed class Thing
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Laptop";
        public string? Iban { get; set; }
        public string? PasswordHash { get; set; }
        public bool MustChangePassword { get; set; }
        public Guid? EmployeeId { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Place Address { get; set; } = new();
    }

    sealed class Place { public string? City { get; set; } }

    sealed class Person { public Guid Id { get; set; } = Guid.NewGuid(); public string FirstName { get; set; } = "Sara"; public string LastName { get; set; } = "Malik"; }

    sealed class Ctx : DbContext
    {
        public Ctx() : base(new DbContextOptionsBuilder<Ctx>().UseNpgsql("Host=unused").Options) { }
        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<Thing>().OwnsOne(x => x.Address);
            b.Entity<Person>();
        }
    }

    static List<AuditCapture> Capture(Ctx db) => AuditTrail.Capture(db.ChangeTracker, _ => true, t => t == typeof(Person));

    [Fact]
    public void Created_lists_non_empty_fields_and_hides_secrets()
    {
        using var db = new Ctx();
        var emp = Guid.NewGuid();
        db.Add(new Thing { Iban = "PK36SCBL0000001123456702", PasswordHash = "abc", MustChangePassword = true, EmployeeId = emp });

        var c = Assert.Single(Capture(db), x => x.EntityType == nameof(Thing));
        Assert.Equal(AuditAction.Created, c.Action);
        Assert.Equal("Laptop", c.Label);
        Assert.Equal(emp, c.SubjectEmployeeId);
        Assert.Equal("••••6702", c.Changes.Single(x => x.Field == "Iban").New);
        Assert.Equal(AuditTrail.Hidden, c.Changes.Single(x => x.Field == "PasswordHash").New);
        Assert.Equal("true", c.Changes.Single(x => x.Field == "MustChangePassword").New);
        Assert.DoesNotContain(c.Changes, x => x.Field is "Id" or "IsDeleted" or "UpdatedAt");
    }

    [Fact]
    public void Updated_only_real_changes_and_skips_audit_only_saves()
    {
        using var db = new Ctx();
        var t = new Thing();
        db.Attach(t);
        t.UpdatedAt = DateTime.UtcNow;
        Assert.DoesNotContain(Capture(db), x => x.EntityType == nameof(Thing));

        t.Name = "Desktop";
        var c = Assert.Single(Capture(db), x => x.EntityType == nameof(Thing));
        Assert.Equal(AuditAction.Updated, c.Action);
        var change = Assert.Single(c.Changes);
        Assert.Equal(("Name", "Laptop", "Desktop"), (change.Field, change.Old, change.New));
    }

    [Fact]
    public void Soft_delete_is_recorded_as_deleted()
    {
        using var db = new Ctx();
        var t = new Thing();
        db.Attach(t);
        t.IsDeleted = true;
        var c = Assert.Single(Capture(db), x => x.EntityType == nameof(Thing));
        Assert.Equal(AuditAction.Deleted, c.Action);
        Assert.Equal(t.Id, c.EntityId);
    }

    [Fact]
    public void Owned_change_is_logged_against_owner()
    {
        using var db = new Ctx();
        var t = new Thing();
        db.Attach(t);
        t.Address.City = "Lahore";
        var c = Assert.Single(Capture(db), x => x.EntityType == "Thing.Address");
        Assert.Equal(t.Id, c.EntityId);
        Assert.Equal("Lahore", c.Changes.Single(x => x.Field == "City").New);
    }

    [Fact]
    public void Employee_type_is_its_own_subject_and_label_from_names()
    {
        using var db = new Ctx();
        var p = new Person();
        db.Add(p);
        var c = Assert.Single(Capture(db), x => x.EntityType == nameof(Person));
        Assert.Equal(p.Id, c.SubjectEmployeeId);
        Assert.Equal("Sara Malik", c.Label);
    }

    [Fact]
    public void Long_values_are_cut_and_json_round_trips()
    {
        var formatted = AuditTrail.Format("Notes", new string('x', 900))!;
        Assert.Equal(AuditTrail.MaxValueLength + 1, formatted.Length);

        var json = AuditTrail.Serialize([new AuditFieldChange("Name", "a", null)]);
        var back = Assert.Single(AuditTrail.Deserialize(json));
        Assert.Equal(("Name", "a", (string?)null), (back.Field, back.Old, back.New));
        Assert.Empty(AuditTrail.Deserialize("not json"));
    }
}
