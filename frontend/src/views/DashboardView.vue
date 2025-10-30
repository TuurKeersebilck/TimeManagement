<script setup lang="ts">
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";

const today: string = new Date().toLocaleDateString();
</script>

<template>
	<AuthenticatedLayout>
		<div class="p-4 sm:p-6 lg:p-8">
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
	</AuthenticatedLayout>
</template>
