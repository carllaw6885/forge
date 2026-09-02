using Forge.Auditing;
using Forge.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Forge.Audit.Api;

/// <summary>
/// Optional HTTP projection of <see cref="IAuditQueries"/> (ADR 40). Bearer
/// only. Not host scoped by the package: the contract already limits a
/// tenant-scoped caller to its own tenant, and the host decides how the
/// group resolves tenancy (add <c>.WithHostScope()</c> to make it host only).
/// </summary>
public static class AuditApi
{
    public static RouteGroupBuilder MapForgeAuditApi(
        this IEndpointRouteBuilder app, string prefix = "/api/audit", string authenticationScheme = ForgeApi.BearerScheme)
    {
        var api = app.MapGroup(prefix).RequireBearer(authenticationScheme).WithTags("Audit");

        api.MapGet("/", async (IAuditQueries audit, string? actor, string? action, string? subject, string? correlationId,
                long? beforeSequence, int? take, CancellationToken ct) =>
            (await audit.ListAsync(new AuditQuery(actor, action, subject, correlationId, beforeSequence ?? long.MaxValue, take ?? 50), ct)).ToHttpResult());
        api.MapPost("/verify", async (IAuditQueries audit, CancellationToken ct) => (await audit.VerifyAsync(ct)).ToHttpResult());
        api.MapPost("/export", async (IAuditQueries audit, CancellationToken ct) => (await audit.ExportAsync(ct)).ToHttpResult());

        return api;
    }
}
