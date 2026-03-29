<script setup lang="ts">
// useTheme applies the dark class to <html> on module load — importing it here
// ensures it runs before the first render, even on non-sidebar pages (login/register).
import { useTheme } from "./composables/useTheme";
useTheme();
</script>

<template>
	<div class="min-h-screen bg-slate-50 dark:bg-slate-950">
		<RouterView />
	</div>
</template>
