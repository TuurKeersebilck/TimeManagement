<script setup lang="ts">
declare const __APP_VERSION__: string;
const version = __APP_VERSION__;
</script>

<template>
  <div class="flex items-baseline gap-2">
    <span class="app-logo-name">TEMPO</span>
    <span class="app-logo-version">v.{{ version }}</span>
  </div>
</template>

<style scoped>
.app-logo-name {
  font-family: var(--font-logo);
  font-size: 1.25rem;
  font-weight: 700;
  letter-spacing: 0.02em;
  color: var(--foreground);
  line-height: 1;
}

.app-logo-version {
  font-family: var(--font-body);
  font-size: 0.65rem;
  font-weight: 400;
  letter-spacing: 0.05em;
  color: var(--muted-foreground);
}
</style>
