using System.ComponentModel.DataAnnotations;
using HireFlow.Application.Abstractions;
using HireFlow.Domain.Entities.Organizations;
using HireFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.API.Controllers;

[ApiController]
[Route("api/management")]
[Authorize(Roles = "OrganizationAdmin")]
public sealed class OrganizationManagementController(
    HireFlowDbContext db,
    ICurrentTenant tenant,
    IPasswordHasher<User> passwordHasher) : ControllerBase
{
    [HttpGet("organization")]
    public async Task<IActionResult> GetOrganization(CancellationToken ct)
    {
        var org = await db.Organizations.SingleOrDefaultAsync(x => x.Id == tenant.OrganizationId, ct);
        return org is null ? NotFound() : Ok(new { org.Id, org.Name, org.Slug, org.IsActive, org.CreatedAt, org.UpdatedAt });
    }

    [HttpPut("organization")]
    public async Task<IActionResult> UpdateOrganization(UpdateOrganizationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200) return BadRequest("Name is required and must be at most 200 characters.");
        var org = await db.Organizations.SingleOrDefaultAsync(x => x.Id == tenant.OrganizationId, ct);
        if (org is null) return NotFound();
        org.Name = request.Name.Trim();
        org.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { org.Id, org.Name, org.Slug, org.IsActive, org.UpdatedAt });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var users = await (from user in db.Users
                           join userRole in db.UserRoles on new { user.OrganizationId, UserId = user.Id } equals new { userRole.OrganizationId, userRole.UserId }
                           join role in db.Roles on new { userRole.OrganizationId, RoleId = userRole.RoleId } equals new { role.OrganizationId, RoleId = role.Id }
                           orderby user.LastName, user.FirstName
                           select new { user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, RoleId = role.Id, RoleName = role.Name })
            .ToListAsync(ct);
        return Ok(users);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateManagedUserRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 320 || !new EmailAddressAttribute().IsValid(request.Email) ||
            string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Length > 100 || string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Length > 100 ||
            request.Password.Length < 12 || request.Password.Length > 256) return BadRequest("Provide a valid email, names, and a password of 12–256 characters.");
        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        if (await db.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, ct)) return Conflict("A user with this email already exists in this organization.");
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == request.RoleId, ct);
        if (role is null) return BadRequest("Role does not exist in this organization.");
        var user = new User { OrganizationId = tenant.OrganizationId, Email = email, NormalizedEmail = normalizedEmail, FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim() };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { OrganizationId = tenant.OrganizationId, UserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, new { user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, RoleId = role.Id, RoleName = role.Name });
    }

    [HttpPut("users/{userId:guid}")]
    public async Task<IActionResult> UpdateUser(Guid userId, UpdateManagedUserRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Trim().Length > 100 || string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Trim().Length > 100) return BadRequest("First and last names are required and limited to 100 characters.");
        if (userId == tenant.UserId && !request.IsActive) return BadRequest("You cannot deactivate your own account.");
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return NotFound();
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == request.RoleId, ct);
        if (role is null) return BadRequest("Role does not exist in this organization.");
        if (userId == tenant.UserId && role.Name != "OrganizationAdmin") return BadRequest("You cannot remove your own organization administrator role.");
        var roleChanged = !await db.UserRoles.AnyAsync(x => x.UserId == userId && x.RoleId == role.Id, ct);
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.IsActive = request.IsActive;
        if (!request.IsActive || roleChanged)
        {
            var now = DateTimeOffset.UtcNow;
            var sessions = await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null).ToListAsync(ct);
            foreach (var session in sessions) session.RevokedAt = now;
        }
        var assignments = await db.UserRoles.Where(x => x.UserId == userId).ToListAsync(ct);
        db.UserRoles.RemoveRange(assignments);
        db.UserRoles.Add(new UserRole { OrganizationId = tenant.OrganizationId, UserId = userId, RoleId = role.Id });
        await db.SaveChangesAsync(ct);
        return Ok(new { user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, RoleId = role.Id, RoleName = role.Name });
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken ct)
    {
        var roles = await db.Roles.OrderBy(x => x.Name).Select(role => new
        {
            role.Id, role.Name, role.Description, role.IsSystemRole,
            Permissions = db.RolePermissions.Where(x => x.RoleId == role.Id).Join(db.Permissions, x => x.PermissionId, p => p.Id, (x, p) => p.Code).OrderBy(code => code).ToArray()
        }).ToListAsync(ct);
        return Ok(roles);
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole(ManageRoleRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100) return BadRequest("Role name is required and limited to 100 characters.");
        var name = request.Name.Trim();
        if (await db.Roles.AnyAsync(x => x.Name.ToUpper() == name.ToUpper(), ct)) return Conflict("A role with this name already exists.");
        var permissions = await ResolvePermissionsAsync(request.PermissionCodes, ct);
        if (permissions is null) return BadRequest("One or more permission codes are unknown.");
        var role = new Role { OrganizationId = tenant.OrganizationId, Name = name, Description = request.Description?.Trim(), IsSystemRole = false };
        db.Roles.Add(role);
        db.RolePermissions.AddRange(permissions.Select(p => new RolePermission { OrganizationId = tenant.OrganizationId, RoleId = role.Id, PermissionId = p.Id }));
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetRoles), new { id = role.Id }, new { role.Id, role.Name, role.Description, role.IsSystemRole, Permissions = permissions.Select(p => p.Code) });
    }

    [HttpPut("roles/{roleId:guid}")]
    public async Task<IActionResult> UpdateRole(Guid roleId, ManageRoleRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100) return BadRequest("Role name is required and limited to 100 characters.");
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == roleId, ct);
        if (role is null) return NotFound();
        if (role.IsSystemRole) return BadRequest("Built-in roles cannot be modified.");
        var name = request.Name.Trim();
        if (await db.Roles.AnyAsync(x => x.Id != roleId && x.Name.ToUpper() == name.ToUpper(), ct)) return Conflict("A role with this name already exists.");
        var permissions = await ResolvePermissionsAsync(request.PermissionCodes, ct);
        if (permissions is null) return BadRequest("One or more permission codes are unknown.");
        role.Name = name;
        role.Description = request.Description?.Trim();
        var existing = await db.RolePermissions.Where(x => x.RoleId == roleId).ToListAsync(ct);
        db.RolePermissions.RemoveRange(existing);
        db.RolePermissions.AddRange(permissions.Select(p => new RolePermission { OrganizationId = tenant.OrganizationId, RoleId = role.Id, PermissionId = p.Id }));
        await db.SaveChangesAsync(ct);
        return Ok(new { role.Id, role.Name, role.Description, role.IsSystemRole, Permissions = permissions.Select(p => p.Code) });
    }

    [HttpDelete("roles/{roleId:guid}")]
    public async Task<IActionResult> DeleteRole(Guid roleId, CancellationToken ct)
    {
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == roleId, ct);
        if (role is null) return NotFound();
        if (role.IsSystemRole) return BadRequest("Built-in roles cannot be deleted.");
        if (await db.UserRoles.AnyAsync(x => x.RoleId == roleId, ct)) return Conflict("Remove this role from users before deleting it.");
        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions(CancellationToken ct) => Ok(await db.Permissions.OrderBy(x => x.Code).Select(x => new { x.Code, x.Description }).ToListAsync(ct));

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments(CancellationToken ct)
    {
        if (!await db.Departments.AnyAsync(ct))
        {
            var defaults = new[]
            {
                "Computer Science", "Information Technology (IT)", "Mechanical Engineering", "Electrical Engineering",
                "Civil Engineering", "Electronics & Communication", "Human Resources", "Finance"
            };
            db.Departments.AddRange(defaults.Select(name => new Department { OrganizationId = tenant.OrganizationId, Name = name }));
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                foreach (var entry in db.ChangeTracker.Entries<Department>().Where(x => x.State == EntityState.Added)) entry.State = EntityState.Detached;
            }
        }
        return Ok(await db.Departments.OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.ParentDepartmentId }).ToListAsync(ct));
    }

    [HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment(ManageDepartmentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160) return BadRequest("Department name is required and limited to 160 characters.");
        if (request.ParentDepartmentId is Guid parentId && !await db.Departments.AnyAsync(x => x.Id == parentId, ct)) return BadRequest("Parent department does not exist in this organization.");
        var department = new Department { OrganizationId = tenant.OrganizationId, Name = request.Name.Trim(), ParentDepartmentId = request.ParentDepartmentId };
        db.Departments.Add(department);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetDepartments), new { id = department.Id }, new { department.Id, department.Name, department.ParentDepartmentId });
    }

    private async Task<List<Permission>?> ResolvePermissionsAsync(IReadOnlyCollection<string> codes, CancellationToken ct)
    {
        var normalized = codes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var permissions = await db.Permissions.Where(x => normalized.Contains(x.Code)).ToListAsync(ct);
        return permissions.Count == normalized.Length ? permissions : null;
    }
}

public sealed record UpdateOrganizationRequest(string Name);
public sealed record CreateManagedUserRequest(string Email, string FirstName, string LastName, string Password, Guid RoleId);
public sealed record UpdateManagedUserRequest(string FirstName, string LastName, bool IsActive, Guid RoleId);
public sealed record ManageRoleRequest(string Name, string? Description, IReadOnlyCollection<string> PermissionCodes);
public sealed record ManageDepartmentRequest(string Name, Guid? ParentDepartmentId);
