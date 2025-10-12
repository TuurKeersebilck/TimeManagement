<script setup>
import { ref } from "vue";
import Header from "./components/Header.vue";
import Nav from "./components/Nav.vue";
import Footer from "./components/Footer.vue";

const isDarkMode = ref(false);

const toggleDarkMode = () => {
	isDarkMode.value = !isDarkMode.value;
	document.documentElement.classList.toggle("dark-mode");
};
</script>

<template>
	<div class="min-h-screen flex flex-col bg-surface-0">
		<Header>
			<template #nav>
				<Nav :isDarkMode="isDarkMode" @toggleDarkMode="toggleDarkMode" />
			</template>
		</Header>

		<main class="flex-1 bg-surface-50">
			<div class="container mx-auto px-6 py-8">
				<RouterView />
			</div>
		</main>

		<Footer />
	</div>
</template>

<style scoped>
/* Smooth transitions for route changes */
.container {
	max-width: 1200px;
}

/* Active link styling */
a.router-link-active {
	font-weight: 600;
}

/* Dark mode surface colors */
:global(.dark-mode) {
	background-color: #0f172a;
}

:global(.dark-mode) .bg-surface-0 {
	background-color: #0f172a;
}

:global(.dark-mode) .bg-surface-50 {
	background-color: #1e293b;
}

:global(.dark-mode) .border-surface-200 {
	border-color: #334155;
}

:global(.dark-mode) .text-surface-600 {
	color: #94a3b8;
}

/* Smooth transitions */
* {
	transition: background-color 0.3s ease, color 0.3s ease;
}
</style>
