<script setup>
const isDarkMode = defineProps({ isDarkMode: Boolean });
const toggleDarkMode = defineEmits(["toggleDarkMode"]);
</script>

<template>
	<nav class="flex items-center gap-6">
		<RouterLink
			to="/"
			class="px-4 py-2 rounded-lg transition-all duration-200 hover:bg-white/10 font-medium"
			active-class="bg-white/20"
		>
			<i class="pi pi-home mr-2"></i>Dashboard
		</RouterLink>
		<RouterLink
			to="/logs"
			class="px-4 py-2 rounded-lg transition-all duration-200 hover:bg-white/10 font-medium"
			active-class="bg-white/20"
		>
			<i class="pi pi-list mr-2"></i>Uren
		</RouterLink>
		<button
			@click="$emit('toggleDarkMode')"
			class="ml-2 p-2 rounded-lg hover:bg-white/10 transition-all duration-200"
			:title="isDarkMode ? 'Switch to Light Mode' : 'Switch to Dark Mode'"
		>
			<i :class="isDarkMode ? 'pi pi-sun' : 'pi pi-moon'" class="text-xl"></i>
		</button>
	</nav>
</template>
