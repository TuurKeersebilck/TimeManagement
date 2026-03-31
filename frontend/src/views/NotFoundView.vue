<script setup lang="ts">
import { RouterLink } from "vue-router";
</script>

<template>
  <div class="flex flex-col items-center justify-center min-h-[80vh] text-center px-4">
    <h1 class="text-9xl font-bold text-gray-800 dark:text-gray-200">404</h1>
    <h2 class="text-2xl font-semibold mt-4 text-gray-700 dark:text-gray-300">Page Not Found</h2>
    <p class="text-gray-600 dark:text-gray-400 mt-2 mb-8">
      The page you're looking for doesn't exist or has been moved.
    </p>
    <RouterLink
      to="/"
      class="px-6 py-3 bg-gray-800 text-white rounded-lg hover:bg-gray-700 transition-colors duration-300"
    >
      Return to Dashboard
    </RouterLink>
  </div>
</template>
