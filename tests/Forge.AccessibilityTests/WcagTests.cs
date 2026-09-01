using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using Xunit;

namespace Forge.AccessibilityTests;

/// <summary>
/// The automated WCAG 2.2 AA gate (ADR 19): axe scans of the admin journey,
/// keyboard/focus/semantic regression, and RTL rendering. A violation here
/// blocks release (eng/QUALITY_GATES.md).
/// </summary>
public class WcagTests(AdminShellFixture fx) : IClassFixture<AdminShellFixture>
{
    private void RequireServer() =>
        Assert.SkipWhen(fx.UnavailableReason is not null, $"environment unavailable: {fx.UnavailableReason}");

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/users")]
    [InlineData("/admin/roles")]
    [InlineData("/admin/audit")]
    [InlineData("/admin/jobs")]
    [InlineData("/admin/settings")]
    [InlineData("/admin/localisation")]
    public async Task Admin_page_has_no_axe_violations(string path)
    {
        RequireServer();
        var page = await fx.NewPageAsync();

        await page.GotoAsync(fx.BaseUrl + path);
        var results = await page.RunAxe();

        Assert.True(results.Violations.Length == 0,
            $"{path}: " + string.Join("; ", results.Violations.Select(v => $"{v.Id} ({v.Nodes.Length} nodes)")));
    }

    [Theory]
    [InlineData("light", "rgb(247, 247, 245)")]
    [InlineData("dark", "rgb(22, 24, 28)")]
    public async Task Both_themes_pass_axe_contrast_checks(string theme, string expectedBackground)
    {
        RequireServer();
        var page = await fx.NewPageAsync();
        await page.GotoAsync($"{fx.BaseUrl}/admin/theme/{theme}?returnUrl=/admin/users");

        // contrast checks are meaningless against browser defaults: prove the shell's
        // stylesheet resolved and the theme token applied (--forge-bg on body)
        Assert.Equal(theme, await page.EvaluateAsync<string>("document.documentElement.dataset.theme"));
        Assert.Equal(expectedBackground, await page.EvaluateAsync<string>("getComputedStyle(document.body).backgroundColor"));

        var results = await page.RunAxe();

        Assert.True(results.Violations.Length == 0,
            $"theme {theme}: " + string.Join("; ", results.Violations.Select(v => v.Id)));
    }

    [Fact]
    public async Task Skip_link_is_first_tab_stop_and_jumps_to_main()
    {
        RequireServer();
        var page = await fx.NewPageAsync();
        await page.GotoAsync(fx.BaseUrl + "/admin");

        await page.Keyboard.PressAsync("Tab");
        var focused = await page.EvaluateAsync<string>("document.activeElement.className");
        Assert.Equal("skip-link", focused);

        await page.Keyboard.PressAsync("Enter");
        Assert.EndsWith("#main", page.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keyboard_reaches_the_navigation_links_in_order()
    {
        RequireServer();
        var page = await fx.NewPageAsync();
        await page.GotoAsync(fx.BaseUrl + "/admin");

        var seen = new List<string>();
        for (var i = 0; i < 12 && seen.Count < 3; i++)
        {
            await page.Keyboard.PressAsync("Tab");
            var tag = await page.EvaluateAsync<string>("document.activeElement.tagName");
            var href = await page.EvaluateAsync<string?>("document.activeElement.getAttribute('href')");
            if (tag == "A" && href is not null && href.StartsWith("/admin", StringComparison.Ordinal))
            {
                seen.Add(href);
            }
        }

        Assert.True(seen.Count >= 3, $"expected at least 3 admin nav links reachable by keyboard, saw: {string.Join(", ", seen)}");
        // focus must be visible on the focused link
        var outline = await page.EvaluateAsync<string>("getComputedStyle(document.activeElement).outlineStyle");
        Assert.NotEqual("none", outline);
    }

    [Fact]
    public async Task Landmarks_and_context_banner_are_present()
    {
        RequireServer();
        var page = await fx.NewPageAsync();
        await page.GotoAsync(fx.BaseUrl + "/admin");

        Assert.Equal(1, await page.Locator("main#main").CountAsync());
        Assert.Equal(1, await page.Locator("header[role='banner']").CountAsync());
        Assert.True(await page.Locator("nav[aria-label='Admin navigation']").CountAsync() >= 1);
        Assert.Contains("Tenant: tenant-a", await page.Locator("[data-testid='tenant-banner']").InnerTextAsync());
    }

    [Fact]
    public async Task Rtl_tenant_renders_right_to_left()
    {
        RequireServer();
        var page = await fx.NewPageAsync("tenant-ar");
        await page.GotoAsync(fx.BaseUrl + "/admin/localisation");

        Assert.Equal("rtl", await page.EvaluateAsync<string>("document.documentElement.getAttribute('dir')"));
        Assert.Equal("ar-SA", await page.EvaluateAsync<string>("document.documentElement.lang"));
        Assert.Contains("right-to-left", await page.Locator("[data-testid='current-culture']").InnerTextAsync());
    }
}
