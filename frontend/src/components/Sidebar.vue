<script setup lang="ts">
import { onMounted } from "vue";
import { useAuth } from "../composables/useAuth";

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

// Use auth composable with shared state
const { currentUser, isLoadingUser, userInitials, fetchUser } = useAuth();

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

onMounted(() => {
	// This will only fetch if not already fetched
	fetchUser();
});
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
			class="fixed inset-0 z-40 lg:hidden backdrop-blur-sm"
			@click="handleToggle"
			@touchstart="handleToggle"
		>
			<div class="absolute inset-0 bg-black/30"></div>
		</div>
	</Transition>

	<!-- Sidebar -->
	<div
		:class="[
			'fixed inset-y-0 left-0 z-50 bg-white shadow-2xl transform transition-all duration-300 ease-out flex flex-col overflow-hidden',
			isOpen
				? 'w-72 translate-x-0'
				: 'w-0 -translate-x-full lg:translate-x-0 lg:w-16',
		]"
	>
		<!-- Sidebar Header -->
		<div
			class="flex items-center justify-between h-20 sidebar-gradient"
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
					:class="['nav-link', isOpen ? 'justify-between' : 'justify-center']"
					active-class="nav-link-active"
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
			<div class="user-profile-card p-4">
				<div v-if="isLoadingUser" class="flex items-center space-x-3">
					<div
						class="w-10 h-10 bg-slate-200 rounded-full animate-pulse shrink-0"
					></div>
					<div class="flex-1 min-w-0 space-y-2">
						<div class="h-4 bg-slate-200 rounded animate-pulse"></div>
						<div class="h-3 bg-slate-200 rounded w-3/4 animate-pulse"></div>
					</div>
				</div>
				<div v-else-if="currentUser" class="flex items-center space-x-3">
					<div class="w-10 h-10 user-avatar shrink-0">
						<span class="text-sm font-bold text-white">{{ userInitials }}</span>
					</div>
					<div class="flex-1 min-w-0">
						<p class="text-sm font-medium text-slate-900 truncate">
							{{ currentUser.fullName }}
						</p>
						<p class="text-xs text-slate-500 truncate">
							{{ currentUser.email }}
						</p>
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
				v-if="currentUser"
				class="w-10 h-10 user-avatar"
				:title="currentUser.fullName"
			>
				<span class="text-sm font-bold text-white">{{ userInitials }}</span>
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
					'w-full btn-danger',
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
