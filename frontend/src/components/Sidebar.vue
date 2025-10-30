<script setup lang="ts">
import { useRouter } from "vue-router";

const router = useRouter();

// Props
interface Props {
	isOpen?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
	isOpen: true,
});

// Emits
const emit = defineEmits<{
	toggle: [];
	logout: [];
}>();

interface NavigationItem {
	name: string;
	to: { name: string };
	icon: string;
}

const navigationItems: NavigationItem[] = [
	{
		name: "Dashboard",
		to: { name: "dashboard" },
		icon: "pi-th-large",
	},
	{
		name: "Time Tracking",
		to: { name: "time-tracking" },
		icon: "pi-clock",
	},
];

const handleToggle = (): void => {
	emit("toggle");
};

const handleLogout = (): void => {
	emit("logout");
};

const handleNavClick = (): void => {
	// Only close sidebar on mobile (screen width < 1024px)
	if (window.innerWidth < 1024) {
		emit("toggle");
	}
};
</script>

<template>
	<!-- Mobile sidebar blur -->
	<Transition
		enter-active-class="transition-opacity duration-300 ease-out"
		leave-active-class="transition-opacity duration-300 ease-in"
		enter-from-class="opacity-0"
		enter-to-class="opacity-100"
		leave-from-class="opacity-100"
		leave-to-class="opacity-0"
	>
		<div
			v-if="isOpen"
			class="fixed inset-0 lg:hidden backdrop-blur-sm"
			@click="handleToggle"
			@touchstart="handleToggle"
		>
			<div class="absolute inset-0 bg-black/30"></div>
		</div>
	</Transition>

	<!-- Sidebar -->
	<div
		:class="[
			'fixed inset-y-0 left-0 bg-white shadow-2xl transform transition-all duration-300 ease-out flex flex-col ',
			isOpen
				? 'w-70 translate-x-0'
				: 'w-0 -translate-x-full lg:translate-x-0 lg:w-16',
		]"
	>
		<!-- Sidebar Header -->
		<div
			class="flex items-center justify-between h-20 bg-linear-to-r from-indigo-600 to-purple-600"
			:class="isOpen ? 'px-6' : 'px-2'"
		>
			<div class="flex items-center">
				<div v-if="isOpen">
					<h2 class="text-xl font-bold text-white">Time management</h2>
				</div>
			</div>
			<div class="space-x-2">
				<!-- Collapse button for desktop -->
				<button
					@click="handleToggle"
					class="hidden lg:block p-2 rounded-lg text-white/70 hover:text-white hover:bg-white/10 transition-colors duration-200 cursor-pointer"
					title="Toggle Sidebar"
				>
					<i
						:class="[
							'pi text-lg transform transition-transform duration-200',
							isOpen ? 'pi-angle-left' : 'pi-angle-right',
						]"
					></i>
				</button>
				<!-- Close button for mobile -->
				<button
					@click="handleToggle"
					class="lg:hidden p-2 rounded-lg text-white/70 hover:text-white hover:bg-white/10 transition-colors duration-200"
				>
					<i class="pi pi-times text-lg"></i>
				</button>
			</div>
		</div>

		<!-- Navigation -->
		<nav class="mt-8 px-1">
			<div class="space-y-2">
				<router-link
					v-for="item in navigationItems"
					:key="item.name"
					:to="item.to"
					@click="handleNavClick"
					:class="[
						'flex items-center text-sm font-medium rounded-xl text-slate-700 hover:text-indigo-700 hover:bg-linear-to-r hover:from-indigo-50 hover:to-purple-50 transition-all duration-200 transform hover:scale-[1.02] relative p-3',
						isOpen ? 'justify-between' : 'justify-center',
					]"
					active-class="bg-linear-to-r from-indigo-500 to-purple-600 text-white shadow-lg shadow-indigo-500/25 hover:from-indigo-600 hover:to-purple-700"
					:title="!isOpen ? item.name : ''"
				>
					<div
						class="flex items-center"
						:class="isOpen ? '' : 'justify-center w-full'"
					>
						<div class="p-2">
							<i :class="['pi text-lg', item.icon]"></i>
						</div>
						<span v-if="isOpen">
							{{ item.name }}
						</span>
					</div>
				</router-link>
			</div>
		</nav>

		<!-- User Profile Section -->
		<div
			v-if="isOpen"
			class="absolute bottom-20 left-0 right-0 px-4 transition-opacity duration-200"
		>
			<div
				class="bg-linear-to-r from-slate-50 to-slate-100 rounded-xl p-4 border border-slate-200"
			>
				<div class="flex items-center space-x-3">
					<div
						class="w-10 h-10 bg-linear-to-r from-indigo-500 to-purple-600 rounded-full flex items-center justify-center shrink-0"
					>
						<span class="text-sm font-bold text-white">TK</span>
					</div>
					<div class="flex-1 min-w-0">
						<p class="text-sm font-medium text-slate-900">Tuur Keersebilck</p>
						<p class="text-xs text-slate-500">tuur@example.com</p>
					</div>
				</div>
			</div>
		</div>

		<!-- Collapsed Profile Icon -->
		<div
			v-if="!isOpen"
			class="absolute bottom-20 left-0 right-0 px-2 lg:flex lg:justify-center hidden"
		>
			<div
				class="w-10 h-10 bg-linear-to-r from-indigo-500 to-purple-600 rounded-full flex items-center justify-center"
			>
				<span class="text-sm font-bold text-white">TK</span>
			</div>
		</div>

		<!-- Logout-->
		<div
			class="absolute bottom-4 left-0 right-0"
			:class="isOpen ? 'px-4' : 'px-2'"
		>
			<button
				@click="handleLogout"
				:class="[
					'w-full flex items-center text-sm font-medium rounded-xl text-red-600 hover:text-red-700 hover:bg-red-50 border border-red-200 transition-all duration-200 transform hover:scale-[1.02] cursor-pointer',
					isOpen
						? 'justify-center px-4 py-3'
						: 'justify-center px-2 py-3 lg:px-3 lg:py-4',
				]"
				:title="!isOpen ? 'Sign Out' : ''"
			>
				<i :class="['pi pi-sign-out text-lg', isOpen ? 'mr-3' : '']"></i>
				<span v-if="isOpen" class="transition-opacity duration-200"
					>Sign Out</span
				>
			</button>
		</div>
	</div>
</template>
