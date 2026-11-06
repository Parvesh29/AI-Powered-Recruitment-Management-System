using HireFlow.Application.Abstractions;
using HireFlow.Domain.Entities.Candidates;
using HireFlow.Domain.Entities.Organizations;
using HireFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Infrastructure.Tests;

public sealed class TenantIsolationTests
{
    [Fact]
    public async Task CandidateQueriesOnlyReturnRowsForAuthenticatedOrganization()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<HireFlowDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();
        var candidateA = new Candidate { OrganizationId = organizationA, FirstName = "Ava" };
        var candidateB = new Candidate { OrganizationId = organizationB, FirstName = "Ben" };

        await using (var seed = new HireFlowDbContext(options, new TestTenantContext(Guid.Empty, Guid.Empty)))
        {
            seed.Organizations.AddRange(
                new Organization { Id = organizationA, Name = "Organization A", Slug = "organization-a" },
                new Organization { Id = organizationB, Name = "Organization B", Slug = "organization-b" });
            seed.Candidates.AddRange(candidateA, candidateB);
            await seed.SaveChangesAsync();
        }

        await using var tenantA = new HireFlowDbContext(options, new TestTenantContext(organizationA, Guid.NewGuid()));
        var visible = await tenantA.Candidates.AsNoTracking().ToListAsync();
        var crossTenantLookup = await tenantA.Candidates.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == candidateB.Id);

        Assert.Single(visible);
        Assert.Equal(candidateA.Id, visible[0].Id);
        Assert.Null(crossTenantLookup);
    }

    [Fact]
    public async Task MissingTenantClaimReturnsNoTenantOwnedRows()
    {
        var options = new DbContextOptionsBuilder<HireFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var organizationId = Guid.NewGuid();
        await using (var seed = new HireFlowDbContext(options, new TestTenantContext(Guid.Empty, Guid.Empty)))
        {
            seed.Organizations.Add(new Organization { Id = organizationId, Name = "Tenant", Slug = "tenant" });
            seed.Candidates.Add(new Candidate { OrganizationId = organizationId, FirstName = "Hidden" });
            await seed.SaveChangesAsync();
        }

        await using var anonymous = new HireFlowDbContext(options, new TestTenantContext(Guid.Empty, Guid.Empty));
        Assert.Empty(await anonymous.Candidates.AsNoTracking().ToListAsync());
    }

    private sealed class TestTenantContext(Guid organizationId, Guid userId) : ICurrentTenant
    {
        public Guid OrganizationId { get; } = organizationId;
        public Guid UserId { get; } = userId;
        public bool IsAuthenticated => UserId != Guid.Empty;
    }
}
