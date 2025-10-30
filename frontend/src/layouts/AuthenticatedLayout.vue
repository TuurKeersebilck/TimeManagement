<script setup lang="ts">
import { ref } from "vue";
import { useRouter } from "vue-router";
import Sidebar from "@/components/Sidebar.vue";
import { authService } from "../services/authService";

const router = useRouter();
const sidebarOpen = ref<boolean>(true);

const handleLogout = (): void => {
	authService.logout();
	router.push("/login");
};

const toggleSidebar = (): void => {
	sidebarOpen.value = !sidebarOpen.value;
};
</script>

<template>
	<div
		class="flex h-screen bg-linear-to-br from-slate-50 via-blue-50 to-indigo-100"
	>
		<!-- Sidebar Component -->
		<Sidebar
			:is-open="sidebarOpen"
			@toggle="toggleSidebar"
			@logout="handleLogout"
		/>

		<!-- Main content -->
		<div
			:class="[
				'flex-1 flex flex-col min-w-0 transition-all duration-300 ease-out relative',
				sidebarOpen ? 'lg:ml-72' : 'lg:ml-16',
			]"
		>
			<!-- Mobile header -->
			<div
				class="lg:hidden bg-white/95 backdrop-blur-lg shadow-lg border-b border-slate-200/50 shrink-0 relative z-10"
			>
				<div class="flex items-center justify-between h-16 px-4">
					<button
						@click="toggleSidebar"
						class="p-2 rounded-xl text-slate-500 hover:text-slate-700 hover:bg-slate-100 transition-all duration-200 transform hover:scale-105 touch-manipulation"
					>
						<i class="pi pi-bars text-xl"></i>
					</button>
				</div>
			</div>

			<!-- Page content  -->
			<div class="flex-1 overflow-y-auto relative z-0">
				<slot />
			</div>
		</div>
	</div>
</template>
