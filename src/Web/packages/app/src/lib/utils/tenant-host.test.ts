import { describe, it, expect } from "vitest";
import { parseTenantHost, tenantUrl } from "./tenant-host";

describe("parseTenantHost", () => {
  it("returns nulls for bare localhost", () => {
    expect(parseTenantHost("localhost")).toEqual({
      currentSlug: null,
      baseDomain: null,
    });
  });

  it("treats localhost as the base domain for dev tenant subdomains", () => {
    expect(parseTenantHost("alice.localhost")).toEqual({
      currentSlug: "alice",
      baseDomain: "localhost",
    });
  });

  it("splits a three-label host into slug and base domain", () => {
    expect(parseTenantHost("alice.example.com")).toEqual({
      currentSlug: "alice",
      baseDomain: "example.com",
    });
  });

  it("returns nulls for a two-label apex domain", () => {
    expect(parseTenantHost("example.com")).toEqual({
      currentSlug: null,
      baseDomain: null,
    });
  });
});

describe("tenantUrl", () => {
  it("builds a default-port URL", () => {
    expect(tenantUrl("alice", "example.com", "https:", "")).toBe(
      "https://alice.example.com/"
    );
  });

  it("preserves a non-default port", () => {
    expect(tenantUrl("alice", "example.com", "https:", "8443")).toBe(
      "https://alice.example.com:8443/"
    );
  });

  it("builds local Aspire URLs with the gateway port", () => {
    expect(tenantUrl("alice", "localhost", "https:", "1612")).toBe(
      "https://alice.localhost:1612/"
    );
  });
});
