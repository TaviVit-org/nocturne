/**
 * Tenant hostname helpers.
 *
 * Tenants are addressed as subdomains of a shared base domain
 * (`{slug}.{base-domain}`). These helpers derive the current slug/base domain
 * from a hostname and build URLs to sibling tenants.
 */

export interface TenantHostInfo {
  /**
   * The tenant slug (first hostname label), or null when not on a tenant
   * subdomain.
   */
  currentSlug: string | null;
  /**
   * The shared base domain (everything after the slug), or null when not
   * derivable.
   */
  baseDomain: string | null;
}

/** Derive the current tenant slug and base domain from a hostname. */
export function parseTenantHost(hostname: string): TenantHostInfo {
  const parts = hostname.split(".");
  // Dev: tenant subdomains of localhost are two-label hosts (alice.localhost).
  if (parts.length === 2 && parts[1] === "localhost") {
    return { currentSlug: parts[0], baseDomain: "localhost" };
  }
  if (parts.length > 2 && hostname !== "localhost") {
    return { currentSlug: parts[0], baseDomain: parts.slice(1).join(".") };
  }
  return { currentSlug: null, baseDomain: null };
}

/** Build the root URL for a tenant subdomain, preserving any non-default port. */
export function tenantUrl(
  slug: string,
  baseDomain: string,
  protocol: string = typeof window !== "undefined"
    ? window.location.protocol
    : "https:",
  port: string = typeof window !== "undefined" ? window.location.port : ""
): string {
  const portSuffix = port ? `:${port}` : "";
  return `${protocol}//${slug}.${baseDomain}${portSuffix}/`;
}
