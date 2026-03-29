<script setup lang="ts">
// Imported for its module-level side effects: applies the stored dark/light
// class to <html> before first render, including on guest pages (login/register)
// that don't mount the Sidebar.
import "./composables/useTheme";
</script>

<template>
	<div class="min-h-screen bg-slate-50 dark:bg-slate-950">
		<RouterView />
	</div>
</template>
