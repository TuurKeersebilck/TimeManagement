<script setup lang="ts">
import { ref } from "vue";
import { useRouter } from "vue-router";
import { authService } from "../services/authService";
import { useAuth } from "../composables/useAuth";

const router = useRouter();
const email = ref<string>("");
const password = ref<string>("");
const rememberMe = ref<boolean>(false);
const loading = ref<boolean>(false);
const error = ref<string>("");
const { fetchUser } = useAuth();

const handleLogin = async (): Promise<void> => {
	error.value = "";
	loading.value = true;

	try {
		const response = await authService.login({
			email: email.value,
			password: password.value,
			rememberMe: rememberMe.value,
		});

		authService.setToken(response.token);

		// Fetch user data immediately after login
		await fetchUser(true);

		router.push("/");
	} catch (err) {
		error.value = (err as Error).message || "Invalid email or password";
	} finally {
		loading.value = false;
	}
};
</script>

<template>
	<div class="min-h-screen flex items-center justify-center py-12 px-4">
		<div class="max-w-md w-full">
			<!-- Header -->
			<div class="text-center mb-8">
				<h2 class="text-3xl font-bold text-slate-900">Welcome back</h2>
				<p class="mt-2 text-sm text-slate-600">
					Sign in to continue tracking your time
				</p>
			</div>

			<!-- Login Form -->
			<div class="bg-white rounded-lg shadow-lg p-8">
				<form @submit.prevent="handleLogin" class="space-y-6">
					<!-- Error Message -->
					<div
						v-if="error"
						class="bg-red-50 border-l-4 border-red-500 p-4 rounded"
					>
						<div class="flex">
							<div class="shrink-0">
								<svg
									class="h-5 w-5 text-red-400"
									viewBox="0 0 20 20"
									fill="currentColor"
								>
									<path
										fill-rule="evenodd"
										d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
										clip-rule="evenodd"
									/>
								</svg>
							</div>
							<div class="ml-3">
								<p class="text-sm text-red-700">{{ error }}</p>
							</div>
						</div>
					</div>

					<!-- Email Field -->
					<div>
						<label
							for="email"
							class="block text-sm font-medium text-slate-700 mb-2"
						>
							Email address
						</label>
						<input
							id="email"
							v-model="email"
							type="email"
							required
							autocomplete="email"
							class="input-field"
							placeholder="you@example.com"
						/>
					</div>

					<!-- Password Field -->
					<div>
						<label
							for="password"
							class="block text-sm font-medium text-slate-700 mb-2"
						>
							Password
						</label>
						<input
							id="password"
							v-model="password"
							type="password"
							required
							autocomplete="current-password"
							class="input-field"
							placeholder="••••••••"
						/>
					</div>

					<!-- Remember Me -->
					<div class="flex items-center">
						<input
							id="remember-me"
							v-model="rememberMe"
							type="checkbox"
							class="h-4 w-4 text-indigo-600 focus:ring-indigo-500 border-slate-300 rounded"
						/>
						<label for="remember-me" class="ml-2 block text-sm text-slate-700">
							Remember me
						</label>
					</div>

					<!-- Submit Button -->
					<button
						type="submit"
						:disabled="loading"
						class="w-full btn-primary justify-center disabled:opacity-50 disabled:cursor-not-allowed"
					>
						<span v-if="!loading">Sign in</span>
						<span v-else class="flex items-center justify-center">
							<svg
								class="animate-spin -ml-1 mr-3 h-5 w-5 text-white"
								xmlns="http://www.w3.org/2000/svg"
								fill="none"
								viewBox="0 0 24 24"
							>
								<circle
									class="opacity-25"
									cx="12"
									cy="12"
									r="10"
									stroke="currentColor"
									stroke-width="4"
								></circle>
								<path
									class="opacity-75"
									fill="currentColor"
									d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
								></path>
							</svg>
							Signing in...
						</span>
					</button>
				</form>

				<!-- Register Link -->
				<div class="mt-6 text-center">
					<p class="text-sm text-slate-600">
						Don't have an account?
						<RouterLink
							to="/register"
							class="font-medium text-indigo-600 hover:text-indigo-700"
						>
							Sign up
						</RouterLink>
					</p>
				</div>
			</div>
		</div>
	</div>
</template>
