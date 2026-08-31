using System.Globalization;
using System.Xml.Linq;
using Forge.Localization;
using Forge.ReferenceCatalog;
using Forge.Settings;
using Forge.Tenancy;
using Forge.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace Forge.LocalizationTests;

/// <summary>Request culture middleware end to end, and the missing-first-party-strings gate (ADR 12).</summary>
public sealed class CultureHostFixture : IAsyncLifetime
{
    public WebApplication App { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddForgeTenancy();
        builder.Services.AddForgeSettings();
        builder.Services.AddForgeLocalization();

        App = builder.Build();
        App.UseForgeTenancy();
        App.UseForgeRequestCulture();
        App.MapGet("/culture", (IStringLocalizer<CatalogResources> localizer) =>
            new { culture = CultureInfo.CurrentUICulture.Name, message = localizer["ItemCreated"].Value });
        await App.StartAsync();
        Client = App.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await App.DisposeAsync();
}

public class RequestCultureTests(CultureHostFixture fx) : IClassFixture<CultureHostFixture>
{
    private async Task<(string Culture, string Message)> GetAsync(string tenant, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/culture");
        request.Headers.Add("X-Tenant", tenant);
        var response = await fx.Client.SendAsync(request, ct);
        var payload = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return (payload.RootElement.GetProperty("culture").GetString()!,
                payload.RootElement.GetProperty("message").GetString()!);
    }

    [Fact]
    public async Task Tenant_culture_flows_to_the_request_and_localised_strings()
    {
        var ct = TestContext.Current.CancellationToken;

        var (defaultCulture, defaultMessage) = await GetAsync("t-default", ct);
        Assert.Equal("en-GB", defaultCulture);
        Assert.Equal("Catalogue item created.", defaultMessage);

        using (var scope = fx.App.Services.CreateScope())
        {
            var tenant = scope.ServiceProvider.GetRequiredService<CurrentTenant>();
            tenant.SetTenant("t-ar");
            await scope.ServiceProvider.GetRequiredService<SettingsService>()
                .SetAsync(LocalizationSettings.Culture, SettingScope.Tenant, null, "ar-SA", ct);
        }

        var (arCulture, arMessage) = await GetAsync("t-ar", ct);
        Assert.Equal("ar-SA", arCulture);
        Assert.Equal("تم إنشاء عنصر الكتالوج.", arMessage); // module resx fallback chain served the ar-SA variant
    }
}

/// <summary>The CI gate for missing first-party strings (ADR 12): every module resx must cover the acceptance cultures.</summary>
public class MissingStringsGateTests
{
    private static readonly string[] AcceptanceCultures = ["ar-SA"];

    [Fact]
    public void Every_module_neutral_resx_has_all_keys_in_every_acceptance_culture()
    {
        var root = FindRepoRoot();
        var failures = new List<string>();

        foreach (var neutral in Directory.EnumerateFiles(Path.Combine(root, "modules"), "*.resx", SearchOption.AllDirectories)
                     .Where(p => !Path.GetFileNameWithoutExtension(p).Contains('.', StringComparison.Ordinal)))
        {
            var neutralKeys = Keys(neutral);
            foreach (var culture in AcceptanceCultures)
            {
                var culturePath = Path.ChangeExtension(neutral, null) + $".{culture}.resx";
                if (!File.Exists(culturePath))
                {
                    failures.Add($"{Path.GetFileName(neutral)}: missing {culture} variant");
                    continue;
                }

                failures.AddRange(neutralKeys.Except(Keys(culturePath))
                    .Select(key => $"{Path.GetFileName(culturePath)}: missing key '{key}'"));
            }
        }

        Assert.Empty(failures);
    }

    private static HashSet<string> Keys(string resxPath) =>
        XDocument.Load(resxPath).Descendants("data")
            .Select(d => d.Attribute("name")!.Value)
            .ToHashSet(StringComparer.Ordinal);

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Forge.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Forge.slnx not found");
    }
}
