<script setup lang="ts">
interface Props {
  showText?: boolean;
}
withDefaults(defineProps<Props>(), {
  showText: true,
});
</script>

<template>
  <div class="flex items-center">
    <span
      v-if="showText"
      class="text-base font-semibold text-slate-900 dark:text-slate-100 truncate"
    >
      Time Management
    </span>
  </div>
</template>
