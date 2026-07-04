using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services.Identity;

/// <summary>
/// Builds the cross-tenant caregiver overview: one item per tenant the subject
/// is an active member of with glucose read permission.
/// </summary>
/// <seealso cref="TenantOverviewService"/>
public interface ITenantOverviewService
{
    /// <summary>
    /// Returns the latest canonical reading, resolved thresholds, status classification,
    /// and active alert summary for every qualifying tenant of <paramref name="subjectId"/>.
    /// Per-tenant access is each membership's effective permissions intersected with
    /// <paramref name="tokenScopes"/> (the auth token's granted scopes), mirroring
    /// <c>MemberScopeMiddleware</c>: a superuser membership bypasses the intersection.
    /// </summary>
    Task<TenantOverviewResponse> GetOverviewAsync(
        Guid subjectId, IReadOnlySet<string> tokenScopes, CancellationToken ct = default);

    /// <summary>
    /// Returns every active tenant the subject may read glucose for: active memberships
    /// (revoked ones are excluded by the global query filter) whose effective permissions,
    /// intersected with <paramref name="tokenScopes"/>, satisfy
    /// <see cref="Core.Models.Authorization.TenantPermissions.GlucoseRead"/>. This is the
    /// authorization core shared by <see cref="GetOverviewAsync"/> and the overview hub.
    /// </summary>
    Task<IReadOnlyList<GlucoseReadTenant>> GetGlucoseReadTenantsAsync(
        Guid subjectId, IReadOnlySet<string> tokenScopes, CancellationToken ct = default);
}

/// <summary>
/// A tenant the subject may read glucose for, with the scopes the membership
/// allows the caller's token on that tenant.
/// </summary>
public sealed record GlucoseReadTenant(TenantEntity Tenant, IReadOnlySet<string> AllowedScopes);
