using HireFlow.Domain.Entities.Organizations;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Infrastructure.Persistence;

public static class PermissionCatalogSeeder
{
    private static readonly (string Code, string Description)[] Catalog =
    [
        ("organization.read", "View organization profile"), ("organization.update", "Update organization profile"),
        ("users.read", "View organization users"), ("users.create", "Create organization users"), ("users.update", "Update organization users"),
        ("roles.read", "View roles and permissions"), ("roles.create", "Create roles"), ("roles.update", "Update custom roles"), ("roles.delete", "Delete unused custom roles"),
        ("departments.read", "View departments"), ("departments.create", "Create departments"),
        ("jobs.read", "View jobs"), ("jobs.create", "Create jobs"), ("jobs.update", "Update jobs"), ("jobs.delete", "Archive jobs"),
        ("candidates.read", "View candidates"), ("candidates.create", "Create candidates"), ("candidates.update", "Update candidates"),
        ("applications.read", "View applications"), ("applications.update", "Update application stages"),
        ("interviews.read", "View interviews"), ("interviews.create", "Schedule interviews"), ("interviews.feedback", "Submit interview feedback"),
        ("analytics.read", "View recruitment analytics")
    ];

    public static async Task SeedAsync(HireFlowDbContext db, CancellationToken cancellationToken = default)
    {
        var existing = await db.Permissions.Select(x => x.Code).ToListAsync(cancellationToken);
        var known = existing.ToHashSet(StringComparer.Ordinal);
        var missing = Catalog.Where(x => !known.Contains(x.Code))
            .Select(x => new Permission { Code = x.Code, Description = x.Description }).ToArray();
        if (missing.Length == 0) return;
        db.Permissions.AddRange(missing);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            foreach (var entry in db.ChangeTracker.Entries<Permission>().Where(x => x.State == EntityState.Added))
                entry.State = EntityState.Detached;
            var raced = await db.Permissions.Select(x => x.Code).ToListAsync(cancellationToken);
            var stillMissing = Catalog.Where(x => !raced.Contains(x.Code)).Select(x => new Permission { Code = x.Code, Description = x.Description });
            db.Permissions.AddRange(stillMissing);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
