using System.Collections.Concurrent;
using Forge.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Forge.Web;

/// <summary>A captured response for replay on duplicate use of an idempotency key (ADR 16).</summary>
public sealed record IdempotentResponse(int StatusCode, string? ContentType, byte[] Body);

/// <summary>
/// Stores idempotency keys and their first response. Keys arrive already
/// tenant-scoped — one tenant can never replay another's response.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>Claims the key. False means it is already claimed (in flight or completed).</summary>
    Task<bool> TryBeginAsync(string scopedKey, CancellationToken cancellationToken);

    Task CompleteAsync(string scopedKey, IdempotentResponse response, CancellationToken cancellationToken);

    /// <summary>The stored response, or null while the first request is still in flight.</summary>
    Task<IdempotentResponse?> FindAsync(string scopedKey, CancellationToken cancellationToken);
}

// ponytail: in-memory reference — sufficient for v0.1 single-instance execution;
// a distributed store (SQL/Redis) arrives with multi-instance support.
internal sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotentResponse?> _entries = new(StringComparer.Ordinal);

    public Task<bool> TryBeginAsync(string scopedKey, CancellationToken ct) =>
        Task.FromResult(_entries.TryAdd(scopedKey, null));

    public Task CompleteAsync(string scopedKey, IdempotentResponse response, CancellationToken ct)
    {
        _entries[scopedKey] = response;
        return Task.CompletedTask;
    }

    public Task<IdempotentResponse?> FindAsync(string scopedKey, CancellationToken ct) =>
        Task.FromResult(_entries.GetValueOrDefault(scopedKey));
}

/// <summary>Endpoint metadata marking a command as idempotency-capable (opt-in, ADR 16).</summary>
public sealed class IdempotencyMetadata
{
    public static readonly IdempotencyMetadata Instance = new();
}

/// <summary>Idempotency for opted-in commands via the Idempotency-Key header.</summary>
public static class IdempotencyExtensions
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotency-Replayed";

    public static IServiceCollection AddForgeIdempotency(this IServiceCollection services)
    {
        services.TryAddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        return services;
    }

    /// <summary>Opts an endpoint (or group) into idempotency-key handling.</summary>
    public static TBuilder WithIdempotency<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(IdempotencyMetadata.Instance);
        return builder;
    }

    /// <summary>
    /// Place after routing and tenant resolution. A request carrying an
    /// Idempotency-Key to an opted-in endpoint executes once; duplicates replay
    /// the first response, and a concurrent duplicate gets 409.
    /// </summary>
    public static IApplicationBuilder UseForgeIdempotency(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var optedIn = context.GetEndpoint()?.Metadata.GetMetadata<IdempotencyMetadata>() is not null;
            if (!optedIn
                || !context.Request.Headers.TryGetValue(HeaderName, out var keyValues)
                || string.IsNullOrWhiteSpace(keyValues))
            {
                await next(context);
                return;
            }

            var tenant = context.RequestServices.GetRequiredService<ICurrentTenant>();
            var store = context.RequestServices.GetRequiredService<IIdempotencyStore>();
            var scopedKey = TenantCacheKey.For(tenant, $"idem:{keyValues}");

            if (!await store.TryBeginAsync(scopedKey, context.RequestAborted))
            {
                var stored = await store.FindAsync(scopedKey, context.RequestAborted);
                if (stored is null)
                {
                    await Results.Problem(
                            statusCode: StatusCodes.Status409Conflict,
                            title: "Request in flight",
                            detail: "A request with this Idempotency-Key is still being processed.")
                        .ExecuteAsync(context);
                    return;
                }

                context.Response.StatusCode = stored.StatusCode;
                context.Response.Headers[ReplayedHeaderName] = "true";
                if (stored.ContentType is not null)
                {
                    context.Response.ContentType = stored.ContentType;
                }

                await context.Response.Body.WriteAsync(stored.Body, context.RequestAborted);
                return;
            }

            // first use: buffer the response so it can be stored for replay
            var original = context.Response.Body;
            using var buffer = new MemoryStream();
            context.Response.Body = buffer;
            try
            {
                await next(context);
                await store.CompleteAsync(
                    scopedKey,
                    new IdempotentResponse(context.Response.StatusCode, context.Response.ContentType, buffer.ToArray()),
                    context.RequestAborted);
            }
            finally
            {
                context.Response.Body = original;
                buffer.Position = 0;
                await buffer.CopyToAsync(original, context.RequestAborted);
            }
        });
}
