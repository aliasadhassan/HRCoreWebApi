using HR.Identity.API.Data;
using HR.Identity.API.Models;
using HR.Identity.API.Models.Admin;
using HR.Shared.Library.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HR.Identity.API.Controllers;

/// <summary>
/// Roles aur unki permissions.
///   - System roles (TenantId NULL) sab companies ke liye hain: read-only, lekin "Duplicate" karke apna version.
///   - Company ke apne roles: bana, badal, mita sakte hain (jab tak koi user us role pe na ho).
/// Permission badli → users ke agle token refresh pe asar (AccessTokenFactory taaza padhta hai).
/// </summary>
[ApiController]
[Route("api/roles")]
[HasPermission(Permissions.RolesManage)]
public sealed class RolesController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => User.GetTenantId() ?? throw new UnauthorizedAccessException("Tenant missing in token.");
    private Guid MeId => User.GetUserId() ?? throw new UnauthorizedAccessException("User missing in token.");

    [HttpGet("permissions")]
    public async Task<ActionResult<IReadOnlyList<PermissionDto>>> Catalog(CancellationToken ct)
        => Ok(await db.Permissions.AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .Select(p => new PermissionDto(p.Id, p.Code, p.Module, p.Description, p.SortOrder))
            .ToListAsync(ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> List(CancellationToken ct)
    {
        var tenantId = TenantId;
        var roles = await db.Roles.AsNoTracking()
            .Where(r => r.TenantId == null || r.TenantId == tenantId)
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name)
            .Select(r => new RoleDto(
                r.Id, r.Name, r.Description, r.IsSystem,
                r.UserRoles.Count(ur => ur.User.TenantId == tenantId && !ur.User.IsDeleted),
                r.RolePermissions.Select(rp => rp.PermissionId).ToList()))
            .ToListAsync(ct);
        return Ok(roles);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveRoleRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (await NameTakenAsync(name, exceptId: null, ct))
            return Problem(statusCode: 409, detail: "A role with this name already exists.");

        var permissionIds = await ValidPermissionIdsAsync(request.PermissionIds, ct);
        if (permissionIds is null)
            return Problem(statusCode: 400, detail: "One or more permissions don't exist.");

        var role = NewRole(name, request.Description, permissionIds);
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
        return Created($"api/roles/{role.Id}", new { id = role.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveRoleRequest request, CancellationToken ct)
    {
        var role = await db.Roles.Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id && (r.TenantId == null || r.TenantId == TenantId), ct);
        if (role is null) return NotFound();
        if (role.IsSystem || role.TenantId is null)
            return Problem(statusCode: 409, detail: "Built-in roles can't be changed. Duplicate it to make your own version.");

        var name = request.Name.Trim();
        if (await NameTakenAsync(name, exceptId: id, ct))
            return Problem(statusCode: 409, detail: "A role with this name already exists.");

        var permissionIds = await ValidPermissionIdsAsync(request.PermissionIds, ct);
        if (permissionIds is null)
            return Problem(statusCode: 400, detail: "One or more permissions don't exist.");

        // Khud ko roles.manage se bahar na kar do — warna ye page hi band ho jayega
        var rolesManageId = await db.Permissions.Where(p => p.Code == Permissions.RolesManage)
                                                .Select(p => p.Id).FirstAsync(ct);
        var removesRolesManage = role.RolePermissions.Any(rp => rp.PermissionId == rolesManageId)
                                 && !permissionIds.Contains(rolesManageId);
        var iHaveThisRole = await db.UserRoles.AnyAsync(ur => ur.UserId == MeId && ur.RoleId == id, ct);
        if (removesRolesManage && iHaveThisRole && !await StillHasRolesManageAsync(exceptRoleId: id, rolesManageId, ct))
            return Problem(statusCode: 409, detail: "You'd lose access to this page. Keep 'Create roles, assign permissions' on at least one of your roles.");

        role.Name = name;
        role.NormalizedName = name.ToUpperInvariant();
        role.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        role.UpdatedBy = MeId;

        foreach (var removed in role.RolePermissions.Where(rp => !permissionIds.Contains(rp.PermissionId)).ToList())
            role.RolePermissions.Remove(removed);
        foreach (var added in permissionIds.Where(pid => role.RolePermissions.All(rp => rp.PermissionId != pid)))
            role.RolePermissions.Add(new RolePermission { PermissionId = added });

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Kisi bhi role (system ya apna) ki copy — built-in role ko customize karne ka tareeqa.</summary>
    [HttpPost("{id:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(Guid id, [FromBody] DuplicateRoleRequest request, CancellationToken ct)
    {
        var source = await db.Roles.AsNoTracking().Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id && (r.TenantId == null || r.TenantId == TenantId), ct);
        if (source is null) return NotFound();

        var name = request.Name.Trim();
        if (await NameTakenAsync(name, exceptId: null, ct))
            return Problem(statusCode: 409, detail: "A role with this name already exists.");

        var role = NewRole(name, source.Description, source.RolePermissions.Select(rp => rp.PermissionId).ToList());
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
        return Created($"api/roles/{role.Id}", new { id = role.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var tenantId = TenantId;
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);
        if (role is null)
            return await db.Roles.AnyAsync(r => r.Id == id && r.TenantId == null, ct)
                ? Problem(statusCode: 409, detail: "Built-in roles can't be deleted.")
                : NotFound();

        var users = await db.UserRoles.CountAsync(ur => ur.RoleId == id && ur.User.TenantId == tenantId && !ur.User.IsDeleted, ct);
        if (users > 0)
            return Problem(statusCode: 409, detail: $"{users} user(s) still have this role. Give them another role first.");

        role.UpdatedBy = MeId;
        db.Roles.Remove(role);          // AppDbContext isko soft delete bana deta hai
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ───────────── Helpers ─────────────
    private Role NewRole(string name, string? description, IEnumerable<int> permissionIds) => new()
    {
        TenantId = TenantId,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
        IsSystem = false,
        CreatedBy = MeId,
        RolePermissions = permissionIds.Distinct().Select(pid => new RolePermission { PermissionId = pid }).ToList()
    };

    /// <summary>System roles ke naam bhi "taken" hain — do "HR Manager" confuse karte hain.</summary>
    private Task<bool> NameTakenAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var normalized = name.ToUpperInvariant();
        var tenantId = TenantId;
        return db.Roles.AnyAsync(r => r.NormalizedName == normalized
                                      && (r.TenantId == null || r.TenantId == tenantId)
                                      && r.Id != exceptId, ct);
    }

    private async Task<List<int>?> ValidPermissionIdsAsync(IEnumerable<int> requested, CancellationToken ct)
    {
        var ids = requested.Distinct().ToList();
        if (ids.Count == 0) return [];
        var found = await db.Permissions.Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
        return found.Count == ids.Count ? found : null;
    }

    /// <summary>Is role ke baghair bhi mere paas roles.manage kisi aur role se hai?</summary>
    private Task<bool> StillHasRolesManageAsync(Guid exceptRoleId, int rolesManageId, CancellationToken ct)
    {
        var me = MeId;
        return db.UserRoles.AnyAsync(ur => ur.UserId == me
                                           && ur.RoleId != exceptRoleId
                                           && !ur.Role.IsDeleted
                                           && ur.Role.RolePermissions.Any(rp => rp.PermissionId == rolesManageId), ct);
    }
}
