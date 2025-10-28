<script setup>
import { ref, computed } from "vue";
import { useRouter } from "vue-router";

const router = useRouter();
const email = ref("");
const password = ref("");
const confirmPassword = ref("");
const loading = ref(false);
const error = ref("");

const passwordsMatch = computed(() => {
	return password.value === confirmPassword.value;
});

const passwordStrength = computed(() => {
	const pwd = password.value;
	if (pwd.length === 0) return null;
	if (pwd.length < 8) return "weak";
	if (
		pwd.length >= 8 &&
		/[A-Z]/.test(pwd) &&
		/[a-z]/.test(pwd) &&
		/[0-9]/.test(pwd)
	) {
		return "strong";
	}
	return "medium";
});

const handleRegister = async () => {
	error.value = "";

	if (!passwordsMatch.value) {
		error.value = "Passwords do not match";
		return;
	}

	if (password.value.length < 8) {
		error.value = "Password must be at least 8 characters long";
		return;
	}

	loading.value = true;

	try {
		const response = await fetch("https://localhost:7055/api/auth/register", {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify({
				username: email.value.split("@")[0], // Generate username from email
				email: email.value,
				password: password.value,
				confirmPassword: password.value,
			}),
		});

		if (!response.ok) {
			const data = await response.json();
			throw new Error(data.message || "Registration failed");
		}

		const data = await response.json();
		localStorage.setItem("token", data.token);
		router.push("/");
	} catch (err) {
		error.value = err.message || "Registration failed. Please try again.";
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
				<h2 class="text-3xl font-bold text-gray-900">Create your account</h2>
				<p class="mt-2 text-sm text-gray-600">
					Start tracking your time efficiently
				</p>
			</div>

			<!-- Register Form -->
			<div class="bg-white rounded-lg shadow-lg p-8">
				<form @submit.prevent="handleRegister" class="space-y-6">
					<!-- Error Message -->
					<div
						v-if="error"
						class="bg-red-50 border-l-4 border-red-500 p-4 rounded"
					>
						<div class="flex">
							<div class="flex-shrink-0">
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
							class="block text-sm font-medium text-gray-700 mb-2"
						>
							Email address
						</label>
						<input
							id="email"
							v-model="email"
							type="email"
							required
							autocomplete="email"
							class="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent transition"
							placeholder="you@example.com"
						/>
					</div>

					<!-- Password Field -->
					<div>
						<label
							for="password"
							class="block text-sm font-medium text-gray-700 mb-2"
						>
							Password
						</label>
						<input
							id="password"
							v-model="password"
							type="password"
							required
							autocomplete="new-password"
							class="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent transition"
							placeholder="••••••••"
						/>
						<!-- Password Strength Indicator -->
						<div v-if="passwordStrength" class="mt-2">
							<div class="flex items-center space-x-2">
								<div class="flex-1 bg-gray-200 rounded-full h-2">
									<div
										class="h-2 rounded-full transition-all duration-300"
										:class="{
											'bg-red-500 w-1/3': passwordStrength === 'weak',
											'bg-yellow-500 w-2/3': passwordStrength === 'medium',
											'bg-green-500 w-full': passwordStrength === 'strong',
										}"
									></div>
								</div>
								<span
									class="text-xs font-medium"
									:class="{
										'text-red-600': passwordStrength === 'weak',
										'text-yellow-600': passwordStrength === 'medium',
										'text-green-600': passwordStrength === 'strong',
									}"
								>
									{{ passwordStrength }}
								</span>
							</div>
							<p class="text-xs text-gray-500 mt-1">
								Use at least 8 characters with uppercase, lowercase, and numbers
							</p>
						</div>
					</div>

					<!-- Confirm Password Field -->
					<div>
						<label
							for="confirmPassword"
							class="block text-sm font-medium text-gray-700 mb-2"
						>
							Confirm password
						</label>
						<input
							id="confirmPassword"
							v-model="confirmPassword"
							type="password"
							required
							autocomplete="new-password"
							class="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent transition"
							:class="{ 'border-red-500': confirmPassword && !passwordsMatch }"
							placeholder="••••••••"
						/>
						<p
							v-if="confirmPassword && !passwordsMatch"
							class="text-xs text-red-600 mt-1"
						>
							Passwords do not match
						</p>
					</div>

					<!-- Submit Button -->
					<button
						type="submit"
						:disabled="loading || !passwordsMatch"
						class="w-full bg-blue-600 hover:bg-blue-700 text-white font-semibold py-3 px-4 rounded-lg transition duration-200 disabled:opacity-50 disabled:cursor-not-allowed"
					>
						<span v-if="!loading">Create account</span>
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
							Creating account...
						</span>
					</button>
				</form>

				<!-- Login Link -->
				<div class="mt-6 text-center">
					<p class="text-sm text-gray-600">
						Already have an account?
						<RouterLink
							to="/login"
							class="font-medium text-blue-600 hover:text-blue-500"
						>
							Sign in
						</RouterLink>
					</p>
				</div>
			</div>
		</div>
	</div>
</template>
