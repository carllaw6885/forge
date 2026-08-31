using System.Net.Http.Json;
using Forge.Core.Privacy;
using Forge.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Forge.ReferenceCatalog.Tests;

/// <summary>The ADR 09 acceptance demonstration: the module enumerates a subject's personal data, tenant-scoped.</summary>
public class PrivacyTests(SliceFixture fx) : IClassFixture<SliceFixture>
{
    private void RequireServer() =>
        Assert.SkipWhen(fx.UnavailableReason is not null, $"SQL Server container unavailable: {fx.UnavailableReason}");

    private async Task CreateAsync(string tenant, string name, string createdBy, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/catalog/items/")
        {
            Content = JsonContent.Create(new { name, createdBy }),
        };
        request.Headers.Add("X-Tenant", tenant);
        (await fx.Client.SendAsync(request, ct)).EnsureSuccessStatusCode();
    }

    [Fact]
    public void CreatedBy_is_declared_personal_via_the_classification_attribute()
    {
        var attribute = typeof(CatalogItem).GetProperty(nameof(CatalogItem.CreatedBy))!
            .GetCustomAttributes(typeof(ClassifiedAttribute), false)
            .Cast<ClassifiedAttribute>()
            .Single();

        Assert.Equal(DataClassification.Personal, attribute.Classification);
    }

    [Fact]
    public async Task Contributor_enumerates_a_subjects_personal_data_within_the_tenant_only()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        await CreateAsync("tenant-p1", "Alpha", "alice", ct);
        await CreateAsync("tenant-p1", "Beta", "alice", ct);
        await CreateAsync("tenant-p1", "Gamma", "bob", ct);
        await CreateAsync("tenant-p2", "Delta", "alice", ct); // other tenant's data

        using var scope = fx.App!.Services.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<CurrentTenant>();
        tenant.SetTenant("tenant-p1");
        var contributor = scope.ServiceProvider.GetRequiredService<IPrivacyContributor>();

        var aliceData = await contributor.EnumeratePersonalDataAsync("alice", ct);

        Assert.Equal(2, aliceData.Count); // tenant-p2's item is invisible under ambient tenancy
        Assert.All(aliceData, item =>
        {
            Assert.Equal("Forge.ReferenceCatalog", item.Module);
            Assert.Equal(DataClassification.Personal, item.Classification);
            Assert.Equal("alice", item.Value);
        });

        Assert.Empty(await contributor.EnumeratePersonalDataAsync("nobody", ct));
    }
}
