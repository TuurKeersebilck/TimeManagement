<script setup>
import { useRouter } from "vue-router";
import { ref } from "vue";
import Sidebar from "@/components/Sidebar.vue";

const router = useRouter();
const sidebarOpen = ref(true);

const handleLogout = () => {
	localStorage.removeItem("token");
	router.push("/login");
};

const toggleSidebar = () => {
	sidebarOpen.value = !sidebarOpen.value;
};

const today = new Date().toLocaleDateString();
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
				'flex-1 flex flex-col min-w-0 transition-all duration-300 ease-out',
				sidebarOpen ? 'lg:ml-72' : 'lg:ml-16',
			]"
		>
			<!-- Mobile header -->
			<div
				class="lg:hidden bg-white/95 backdrop-blur-lg shadow-lg border-b border-slate-200/50 shrink-0"
			>
				<div class="flex items-center justify-between h-16 px-4">
					<button
						@click="toggleSidebar"
						class="p-2 rounded-xl text-slate-500 hover:text-slate-700 hover:bg-slate-100 transition-all duration-200 transform hover:scale-105 touch-manipulation"
					>
						<i class="pi pi-bars text-xl"></i>
					</button>
					<h1
						class="text-lg font-bold bg-linear-to-r from-indigo-600 to-purple-600 bg-clip-text text-transparent"
					>
						TimeFlow
					</h1>
					<div class="w-10"></div>
					<!-- Spacer for centering -->
				</div>
			</div>

			<!-- Page content -->
			<div class="flex-1 overflow-y-auto p-4 sm:p-6 lg:p-8">
				<!-- Header for desktop -->
				<div class="hidden lg:block mb-10">
					<div class="max-w-7xl mx-auto">
						<h1 class="text-4xl font-bold">Time Management Dashboard</h1>
					</div>
				</div>

				<div class="max-w-7xl mx-auto">
					<!-- Quick Stats Cards -->
					<div class="mt-8 grid grid-cols-1 md:grid-cols-2 gap-6">
						<!-- Today's Time -->
						<div
							class="bg-white/70 backdrop-blur-lg rounded-2xl p-6 border border-white/20 shadow-lg hover:shadow-xl transition-all duration-300 transform hover:scale-101"
						>
							<div class="flex items-center">
								<div
									class="p-3 bg-linear-to-r from-blue-500 to-blue-600 rounded-xl"
								>
									<i class="pi pi-clock text-white text-2xl"></i>
								</div>
								<div class="ml-4">
									<p class="text-sm font-medium text-slate-600">
										Hours Today
										<span class="text-xs text-slate-400">{{ today }}</span>
									</p>
									<p class="text-2xl font-bold text-slate-900">
										To be implemented
									</p>
								</div>
							</div>
						</div>

						<!-- This Week -->
						<div
							class="bg-white/70 backdrop-blur-lg rounded-2xl p-6 border border-white/20 shadow-lg hover:shadow-xl transition-all duration-300 transform hover:scale-101"
						>
							<div class="flex items-center">
								<div
									class="p-3 bg-linear-to-r from-purple-500 to-purple-600 rounded-xl"
								>
									<i class="pi pi-chart-bar text-white text-2xl"></i>
								</div>
								<div class="ml-4">
									<p class="text-sm font-medium text-slate-600">Hours Week</p>
									<p class="text-2xl font-bold text-slate-900">
										To be implemented
									</p>
								</div>
							</div>
						</div>
					</div>
				</div>
			</div>
		</div>
	</div>
</template>
