<script setup lang="ts">
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import TeamVacationCalendar from "@/components/TeamVacationCalendar.vue";
</script>

<template>
  <AuthenticatedLayout>
    <div class="p-6 lg:p-8">
      <div class="mb-6">
        <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Team Calendar</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">See when everyone is off</p>
      </div>
      <TeamVacationCalendar />
    </div>
  </AuthenticatedLayout>
</template>
