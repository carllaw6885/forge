using System.Globalization;
using System.Security.Claims;
using Forge.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Forge.Localization;

/// <summary>Localisation options (ADR 12): the application default; tenant and user override via settings.</summary>
public sealed record ForgeLocalizationOptions
{
    public string DefaultCulture { get; init; } = "en-GB";
    public string DefaultTimeZone { get; init; } = "UTC";
}

/// <summary>Setting keys the resolution chain reads; culture and time zone stay distinct concepts (ADR 12).</summary>
public static class LocalizationSettings
{
    public static readonly SettingDefinition<string?> Culture = new("Localization:Culture", null);
    public static readonly SettingDefinition<string?> TimeZone = new("Localization:TimeZone", null);
}

/// <summary>
/// Resolves culture and time zone with application → tenant → user precedence
/// (ADR 12), riding the settings precedence chain.
/// </summary>
public sealed class CultureResolver(SettingsService settings, ForgeLocalizationOptions options)
{
    public async Task<CultureInfo> ResolveCultureAsync(string? userId, CancellationToken cancellationToken)
    {
        var name = await settings.GetAsync(LocalizationSettings.Culture, userId, cancellationToken)
            ?? options.DefaultCulture;
        return CultureInfo.GetCultureInfo(name);
    }

    public async Task<TimeZoneInfo> ResolveTimeZoneAsync(string? userId, CancellationToken cancellationToken)
    {
        var id = await settings.GetAsync(LocalizationSettings.TimeZone, userId, cancellationToken)
            ?? options.DefaultTimeZone;
        return TimeZoneInfo.FindSystemTimeZoneById(id);
    }
}

/// <summary>Deterministic UTC → display conversion (ADR 12): storage stays UTC, display is explicit.</summary>
public static class ForgeTime
{
    public static DateTimeOffset ToDisplay(DateTimeOffset utc, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTime(utc, timeZone);
}

/// <summary>Composition surface for request-scoped culture resolution.</summary>
public static class LocalizationExtensions
{
    public static IServiceCollection AddForgeLocalization(
        this IServiceCollection services, ForgeLocalizationOptions? options = null)
    {
        services.AddLocalization();
        services.TryAddSingleton(options ?? new ForgeLocalizationOptions());
        services.TryAddScoped<CultureResolver>();
        return services;
    }

    /// <summary>
    /// Place after tenant resolution: sets CurrentCulture/CurrentUICulture from
    /// the resolved chain so IStringLocalizer and formatting follow it.
    /// </summary>
    public static IApplicationBuilder UseForgeRequestCulture(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var resolver = context.RequestServices.GetRequiredService<CultureResolver>();
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.Identity?.Name;
            var culture = await resolver.ResolveCultureAsync(userId, context.RequestAborted);

            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            await next(context);
        });
}
