<script setup lang="ts">
import VacationRequestsPanel from "@/components/VacationRequestsPanel.vue";
</script>

<template>
  <div class="p-6 lg:p-8">
    <div class="max-w-6xl mx-auto">
      <div class="mb-6">
        <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Vacation Requests</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">
          Vacation employees asked for, waiting for your approval
        </p>
      </div>
      <VacationRequestsPanel />
    </div>
  </div>
</template>
