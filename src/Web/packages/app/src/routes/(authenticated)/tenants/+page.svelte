<script lang="ts">
  import { onDestroy, onMount } from "svelte";
  import { getOverview } from "$lib/api/generated/myTenants.generated.remote";
  import TenantOverviewTile from "$lib/components/tenants/TenantOverviewTile.svelte";
  import { Button } from "$lib/components/ui/button";
  import { sortTenantsByUrgency } from "$lib/utils/glucose-status";
  import { parseTenantHost } from "$lib/utils/tenant-host";

  const overview = $derived(getOverview());

  let baseDomain = $state<string | null>(null);

  let refreshInterval: ReturnType<typeof setInterval> | null = null;

  onMount(() => {
    baseDomain = parseTenantHost(window.location.hostname).baseDomain;
    refreshInterval = setInterval(() => {
      // Detached callback: use .refresh(), not a bare await (no reactive context here).
      getOverview().refresh();
    }, 60_000);
  });

  onDestroy(() => {
    if (refreshInterval) clearInterval(refreshInterval);
  });
</script>

<svelte:head>
  <title>Tenants overview - Nocturne</title>
</svelte:head>

<div class="container mx-auto space-y-6 p-4 md:p-6">
  <div>
    <h1 class="text-2xl font-bold">Tenants overview</h1>
    <p class="text-sm text-muted-foreground">
      Latest glucose across the tenants you have access to. Refreshes every
      minute.
    </p>
  </div>

  {#await overview}
    <p class="text-sm text-muted-foreground">Loading…</p>
  {:then data}
    {@const tenants = sortTenantsByUrgency(data.tenants ?? [])}
    {#if tenants.length === 0}
      <p class="text-sm text-muted-foreground">
        No tenants with glucose access.
      </p>
    {:else}
      <div class="grid grid-cols-2 gap-4 lg:grid-cols-3 xl:grid-cols-4">
        {#each tenants as tenant (tenant.tenantId)}
          <TenantOverviewTile {tenant} {baseDomain} />
        {/each}
      </div>
    {/if}
  {:catch}
    <div class="space-y-2">
      <p class="text-sm text-muted-foreground">
        Failed to load the tenants overview.
      </p>
      <Button
        variant="outline"
        size="sm"
        onclick={() => getOverview().refresh()}
      >
        Retry
      </Button>
    </div>
  {/await}
</div>
