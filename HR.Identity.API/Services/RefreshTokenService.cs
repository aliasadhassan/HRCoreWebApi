using HR.Identity.API.Data;
using HR.Identity.API.Helpers;
using HR.Identity.API.Models;
using HR.Identity.API.Models.Common;
using HR.Shared.Library.Helpers;
using Microsoft.EntityFrameworkCore;

namespace HR.Identity.API.Services;

public enum RefreshStatus { Success, Invalid, Superseded, Reused }

public record RefreshResult(RefreshStatus Status, User? User = null, string? Token = null, DateTime ExpiresAt = default);

public class RefreshTokenService(AppDbContext db, JwtTokenHelper jwt, ILogger<RefreshTokenService> logger)
{
    private const string RotatedReason = "Rotated";

    // Do parallel requests (2 tabs / interceptor race) same token bhejein to
    // doosri request ko "chori" na samjha jaye — 30s grace window
    private static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(30);

    /// <summary>Login / SSO pe naya session (nayi family) start karta hai.</summary>
    public async Task<(string Token, DateTime ExpiresAt)> IssueAsync(Guid userId, string? ip, string? userAgent)
    {
        var now = DateTime.UtcNow;

        // Is user ke expired tokens ki safai
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.ExpiresAt < now)
            .ExecuteDeleteAsync();

        var (entity, raw) = Create(userId, familyId: Guid.NewGuid(), ip, userAgent);
        await db.SaveChangesAsync();   // controller ki pending changes (LastLoginAt, audit) bhi saath save

        return (raw, entity.ExpiresAt);
    }

    /// <summary>Purana token revoke, naya token same family mein.</summary>
    public async Task<RefreshResult> RotateAsync(string rawToken, string? ip, string? userAgent)
    {
        var now = DateTime.UtcNow;
        var hash = TokenHasher.Hash(rawToken);

        var stored = await db.RefreshTokens
            .Include(t => t.User).ThenInclude(u => u.Tenant)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (stored is null)
            return new(RefreshStatus.Invalid);

        if (stored.RevokedAt is not null)
        {
            if (stored.RevokedReason == RotatedReason && now - stored.RevokedAt < ReuseGrace)
                return new(RefreshStatus.Superseded);

            // Revoked token dobara aaya => leak/chori. Poora session chain khatam.
            await RevokeFamilyAsync(stored.FamilyId, ip, "Reuse detected");
            logger.LogWarning("Refresh token reuse detected for user {UserId}; family {FamilyId} revoked",
                stored.UserId, stored.FamilyId);
            return new(RefreshStatus.Reused);
        }

        if (stored.ExpiresAt <= now || !stored.User.IsActive || stored.User.Tenant.Status != TenantStatus.Active)
            return new(RefreshStatus.Invalid);

        var (next, raw) = Create(stored.UserId, stored.FamilyId, ip, userAgent);

        stored.RevokedAt = now;
        stored.RevokedByIp = ip;
        stored.RevokedReason = RotatedReason;
        stored.ReplacedByTokenId = next.Id;

        await db.SaveChangesAsync();
        return new(RefreshStatus.Success, stored.User, raw, next.ExpiresAt);
    }

    /// <summary>Logout: is token ka poora session (family) band.</summary>
    public async Task RevokeAsync(string rawToken, string? ip, string reason)
    {
        var hash = TokenHasher.Hash(rawToken);
        var familyId = await db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .FirstOrDefaultAsync();

        if (familyId is not null)
            await RevokeFamilyAsync(familyId.Value, ip, reason);
    }

    /// <summary>Password reset / account disable: har device se logout.</summary>
    public Task RevokeAllForUserAsync(Guid userId, string reason)
    {
        var now = DateTime.UtcNow;
        return db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevokedReason, reason));
    }

    private Task RevokeFamilyAsync(Guid familyId, string? ip, string reason)
    {
        var now = DateTime.UtcNow;
        return db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevokedByIp, ip)
                .SetProperty(t => t.RevokedReason, reason));
    }

    private (RefreshToken Entity, string Raw) Create(Guid userId, Guid familyId, string? ip, string? userAgent)
    {
        var generated = jwt.GenerateRefreshToken();

        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = TokenHasher.Hash(generated.RefreshToken),
            FamilyId = familyId,
            ExpiresAt = generated.RefreshTokenExpiryDate,
            CreatedByIp = ip,
            UserAgent = userAgent
        };

        db.RefreshTokens.Add(entity);   // EF yahin sequential Guid Id assign kar deta hai
        return (entity, generated.RefreshToken);
    }
}
