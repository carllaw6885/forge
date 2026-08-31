using System.Globalization;
using System.Threading.RateLimiting;
using Forge.Core.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Forge.Web;

internal sealed class WebSecurityConfigurationValidator(IConfiguration config) : IProductionConfigurationValidator
{
    public IEnumerable<string> Validate()
    {
        var origins = config.GetSection("Security:Cors:Origins").Get<string[]>() ?? [];
        if (origins.Contains("*"))
        {
            yield return "Security:Cors:Origins must not contain '*' in production — name the allowed origins";
        }

        if (!config.GetValue("Security:RequireHttps", true))
        {
            yield return "Security:RequireHttps must not be disabled in production";
        }

        if (config.GetValue("DetailedErrors", false))
        {
            yield return "DetailedErrors must not be enabled in production";
        }
    }
}

/// <summary>
/// Hardened ASP.NET defaults (ADR 18): HSTS in production, secure cookies,
/// antiforgery, deny-by-default CORS, request size limits, rate-limit hooks and
/// a CSP-ready header shell. Unsafe production configuration refuses startup.
/// </summary>
public static class SecurityDefaults
{
    public const string CorsPolicyName = "forge-default";

    public static IServiceCollection AddForgeSecurityDefaults(this IServiceCollection services, IConfiguration config)
    {
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
        });

        services.Configure<CookiePolicyOptions>(options =>
        {
            options.Secure = CookieSecurePolicy.Always;
            options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
            options.MinimumSameSitePolicy = SameSiteMode.Lax;
        });

        services.AddAntiforgery();

        // deny-by-default CORS: no configured origins means no cross-origin access
        var origins = config.GetSection("Security:Cors:Origins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
        {
            if (origins.Length > 0)
            {
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
            }
        }));

        // request limits (Kestrel + multipart forms), configurable, safe default 10 MB
        var maxBody = config.GetValue("Security:MaxRequestBodyBytes", 10 * 1024 * 1024L);
        services.Configure<KestrelServerOptions>(options => options.Limits.MaxRequestBodySize = maxBody);
        services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
            options.MultipartBodyLengthLimit = maxBody);

        // rate-limit hook: a global fixed window when configured, otherwise inert
        var permitLimit = config.GetValue("Security:RateLimit:PermitLimit", 0);
        if (permitLimit > 0)
        {
            var window = TimeSpan.FromSeconds(config.GetValue("Security:RateLimit:WindowSeconds", 60));
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window }));
            });
        }

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IProductionConfigurationValidator, WebSecurityConfigurationValidator>());
        return services;
    }

    /// <summary>Applies the hardened pipeline. In production, unsafe configuration refuses startup here.</summary>
    public static IApplicationBuilder UseForgeSecurityDefaults(this IApplicationBuilder app)
    {
        var env = app.ApplicationServices.GetRequiredService<IHostEnvironment>();
        var config = app.ApplicationServices.GetRequiredService<IConfiguration>();

        if (env.IsProduction())
        {
            var failures = app.ApplicationServices.GetServices<IProductionConfigurationValidator>()
                .SelectMany(v => v.Validate())
                .ToList();
            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    "unsafe production configuration: " + string.Join("; ", failures));
            }

            app.UseHsts();
        }

        app.UseCookiePolicy();
        app.UseCors(CorsPolicyName);

        // CSP-ready shell + standard hardening headers
        var csp = config.GetValue("Security:ContentSecurityPolicy", "default-src 'self'");
        app.Use(async (context, next) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            if (!string.IsNullOrEmpty(csp))
            {
                context.Response.Headers.ContentSecurityPolicy = csp;
            }

            await next(context);
        });

        if (config.GetValue("Security:RateLimit:PermitLimit", 0) > 0)
        {
            app.UseRateLimiter();
        }

        return app;
    }
}
