using System.Net;
using System.Net.Http.Json;
using Forge.Events;
using Forge.Modularity;
using Forge.Persistence.SqlServer;
using Forge.ReferenceCatalog;
using Forge.ReferenceCatalog.Contracts;
using Forge.Tenancy;
using Forge.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Xunit;

namespace Forge.ReferenceCatalog.Tests;

/// <summary>
/// The first vertical slice, end to end: explicit composition, module-owned
/// persistence and migration, tenant-owned CRUD, domain + integration events,
/// audit contribution, localisation, Problem Details and OpenAPI.
/// </summary>
public sealed class SliceFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;
    public WebApplication? App { get; private set; }
    public HttpClient Client { get; private set; } = null!;
    public List<EventEnvelope> PublishedIntegrationEvents { get; } = [];

    public string? UnavailableReason { get; private set; }

    private sealed class RecordingHandler(List<EventEnvelope> log) : IIntegrationEventHandler<CatalogItemCreated>
    {
        public Task HandleAsync(EventEnvelope envelope, CatalogItemCreated payload, CancellationToken ct)
        {
            log.Add(envelope);
            return Task.CompletedTask;
        }
    }

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();
        }
        catch (Exception ex)
        {
            if (Environment.GetEnvironmentVariable("FORGE_REQUIRE_SQLSERVER") == "true")
            {
                throw;
            }

            UnavailableReason = ex.Message;
            return;
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        builder.Services.AddLocalization();
        builder.Services.AddOpenApi();
        builder.Services.AddForgeTenancy();
        builder.Services.AddForge(new CatalogModule(_container.GetConnectionString()));
        builder.Services.AddSingleton<IIntegrationEventHandler<CatalogItemCreated>>(
            new RecordingHandler(PublishedIntegrationEvents));

        App = builder.Build();
        App.Services.UseForge();
        App.UseForgeTenancy();
        App.MapOpenApi().WithHostScope();
        App.MapCatalogEndpoints();
        await App.StartAsync();

        using (var scope = App.Services.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<CurrentTenant>().BeginHostScope())
        {
            await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        }

        Client = App.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        if (App is not null)
        {
            await App.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

public class CatalogSliceTests(SliceFixture fx) : IClassFixture<SliceFixture>
{
    private void RequireServer() =>
        Assert.SkipWhen(fx.UnavailableReason is not null, $"SQL Server container unavailable: {fx.UnavailableReason}");

    private static HttpRequestMessage Request(HttpMethod method, string uri, string? tenant, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        if (tenant is not null)
        {
            request.Headers.Add("X-Tenant", tenant);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    [Fact]
    public async Task Create_then_get_round_trips_with_localised_message()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        var created = await fx.Client.SendAsync(
            Request(HttpMethod.Post, "/api/catalog/items/", "tenant-a", new { name = "Anvil" }), ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = await created.Content.ReadFromJsonAsync<CatalogItemResponse>(ct);
        Assert.NotNull(item);
        Assert.Equal("Catalogue item created.", item.Message);

        var fetched = await fx.Client.SendAsync(
            Request(HttpMethod.Get, $"/api/catalog/items/{item.Id}", "tenant-a"), ct);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_is_denied_with_problem_details()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        var response = await fx.Client.SendAsync(
            Request(HttpMethod.Post, "/api/catalog/items/", tenant: null, new { name = "Anvil" }), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType!.ToString());
    }

    [Fact]
    public async Task Another_tenant_cannot_read_the_item()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        var created = await fx.Client.SendAsync(
            Request(HttpMethod.Post, "/api/catalog/items/", "tenant-a", new { name = "Private" }), ct);
        var item = await created.Content.ReadFromJsonAsync<CatalogItemResponse>(ct);

        var crossTenant = await fx.Client.SendAsync(
            Request(HttpMethod.Get, $"/api/catalog/items/{item!.Id}", "tenant-b"), ct);

        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
    }

    [Fact]
    public async Task Invalid_name_returns_localised_validation_problem()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        var response = await fx.Client.SendAsync(
            Request(HttpMethod.Post, "/api/catalog/items/", "tenant-a", new { name = "" }), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("128 characters or fewer", await response.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task Create_emits_integration_event_and_audit_evidence_with_shared_correlation()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        var created = await fx.Client.SendAsync(
            Request(HttpMethod.Post, "/api/catalog/items/", "tenant-a", new { name = "Traceable" }), ct);
        var item = await created.Content.ReadFromJsonAsync<CatalogItemResponse>(ct);

        // the event now travels through the transactional outbox; wait for the dispatcher
        EventEnvelope? found = null;
        for (var i = 0; i < 100 && found is null; i++)
        {
            found = fx.PublishedIntegrationEvents.FirstOrDefault(
                e => ((CatalogItemCreated)e.Payload).ItemId == item!.Id);
            if (found is null)
            {
                await Task.Delay(100, ct);
            }
        }

        Assert.NotNull(found);
        var envelope = found;
        Assert.Equal("catalog.item.created", envelope.EventType);
        Assert.Equal(1, envelope.SchemaVersion);
        Assert.Equal("tenant-a", envelope.TenantId);

        var store = fx.App!.Services.GetRequiredService<Forge.Auditing.IAuditStore>();
        var records = await store.ReadAllAsync(ct);
        var audit = Assert.Single(records.Select(r => r.Event), a => a.Subject == item!.Id.ToString("N"));
        Assert.Equal("tenant-a", audit.TenantId);
        Assert.Equal(envelope.CorrelationId.ToString(), audit.CorrelationId);
        Assert.Empty(Forge.Auditing.AuditChainVerifier.Verify(records));
    }

    [Fact]
    public async Task Public_contract_reads_through_dto_only()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        var created = await fx.Client.SendAsync(
            Request(HttpMethod.Post, "/api/catalog/items/", "tenant-a", new { name = "ViaContract" }), ct);
        var item = await created.Content.ReadFromJsonAsync<CatalogItemResponse>(ct);

        using var scope = fx.App!.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<ICatalogReader>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<CurrentTenant>();

        currentTenant.SetTenant("tenant-a");
        var dto = await reader.FindAsync(item!.Id, ct);
        Assert.Equal("ViaContract", dto!.Name);

        currentTenant.SetTenant("tenant-b");
        Assert.Null(await reader.FindAsync(item.Id, ct));
    }

    [Fact]
    public async Task OpenApi_document_describes_the_endpoints()
    {
        RequireServer();
        var ct = TestContext.Current.CancellationToken;

        var doc = await fx.Client.GetStringAsync("/openapi/v1.json", ct);

        Assert.Contains("/api/catalog/items", doc);
        Assert.Contains("CreateCatalogItem", doc);
    }

    [Fact]
    public void Module_model_conforms_to_manifest()
    {
        RequireServer();

        using var scope = fx.App!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var manifest = scope.ServiceProvider.GetRequiredService<ModuleCatalog>()
            .Manifests.Single(m => m.Id == "Forge.ReferenceCatalog");

        Assert.Empty(ModuleModelValidator.Validate(db, manifest));
    }
}
