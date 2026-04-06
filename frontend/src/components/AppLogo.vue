<script setup lang="ts">
interface Props {
  showText?: boolean;
}
withDefaults(defineProps<Props>(), {
  showText: true,
});
</script>

<template>
  <div class="flex items-center gap-2.5">
    <svg
      class="size-7 shrink-0"
      viewBox="0 0 32 32"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden="true"
    >
      <circle cx="16" cy="16" r="16" fill="#4f46e5" />
      <circle cx="16" cy="16" r="11.5" fill="white" opacity="0.96" />
      <line x1="16" y1="5.8" x2="16" y2="8.2" stroke="#818cf8" stroke-width="1.5" stroke-linecap="round" />
      <line x1="26.2" y1="16" x2="23.8" y2="16" stroke="#818cf8" stroke-width="1.5" stroke-linecap="round" />
      <line x1="16" y1="26.2" x2="16" y2="23.8" stroke="#818cf8" stroke-width="1.5" stroke-linecap="round" />
      <line x1="5.8" y1="16" x2="8.2" y2="16" stroke="#818cf8" stroke-width="1.5" stroke-linecap="round" />
      <line x1="16" y1="16" x2="10.8" y2="11" stroke="#1e1b4b" stroke-width="2.3" stroke-linecap="round" />
      <line x1="16" y1="16" x2="21.5" y2="10.5" stroke="#4f46e5" stroke-width="1.6" stroke-linecap="round" />
      <circle cx="16" cy="16" r="1.6" fill="#1e1b4b" />
    </svg>
    <span
      v-if="showText"
      class="text-base font-semibold text-slate-900 dark:text-slate-100 truncate"
    >
      Time Management
    </span>
  </div>
</template>
