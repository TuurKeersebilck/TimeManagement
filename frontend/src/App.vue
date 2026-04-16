<script setup lang="ts">
// Imported for its module-level side effects: applies the stored dark/light
// class to <html> before first render, including on guest pages (login/register)
// that don't mount the Sidebar.
import { useTheme } from "./composables/useTheme";
import { Toaster as Sonner } from "@/components/ui/sonner";
import AppConfirmDialog from "@/components/AppConfirmDialog.vue";

const { isDark } = useTheme();
</script>

<template>
  <div class="min-h-screen bg-slate-50 dark:bg-slate-950">
    <RouterView />
    <Sonner :theme="isDark ? 'dark' : 'light'" position="top-center" rich-colors />
    <AppConfirmDialog />
  </div>
</template>
