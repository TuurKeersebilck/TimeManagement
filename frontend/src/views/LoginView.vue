<script setup lang="ts">
import { ref } from "vue";
import { useRouter } from "vue-router";
import { authService } from "../services/authService";
import { extractApiError } from "@/utils/apiError";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { Loader2Icon, EyeIcon, EyeOffIcon } from "lucide-vue-next";

const router = useRouter();
const email = ref<string>("");
const password = ref<string>("");
const rememberMe = ref<boolean>(false);
const loading = ref<boolean>(false);
const error = ref<string>("");
const showPassword = ref(false);

const handleLogin = async (): Promise<void> => {
  error.value = "";
  loading.value = true;

  try {
    const response = await authService.login({
      email: email.value,
      password: password.value,
      rememberMe: rememberMe.value,
    });

    authService.setUserInfo(response.email, response.fullName, response.roles);
    router.push(response.roles.includes("Admin") ? "/admin/dashboard" : "/");
  } catch (err) {
    error.value = extractApiError(err, "Invalid email or password");
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <div class="min-h-screen flex items-center justify-center py-12 px-4 bg-background">
    <div class="max-w-md w-full">
      <!-- Header -->
      <div class="text-center mb-8">
        <h2 class="text-3xl font-bold text-foreground">Welcome back</h2>
        <p class="mt-2 text-sm text-muted-foreground">Sign in to continue tracking your time</p>
      </div>

      <!-- Login Form -->
      <div class="bg-card text-card-foreground rounded-xl border border-border shadow-sm p-8">
        <form @submit.prevent="handleLogin" class="space-y-5">
          <!-- Error Message -->
          <div
            v-if="error"
            class="bg-destructive/10 border border-destructive/30 text-destructive text-sm px-4 py-3 rounded-lg"
          >
            {{ error }}
          </div>

          <!-- Email Field -->
          <div class="space-y-2">
            <Label for="email">Email address</Label>
            <Input
              id="email"
              v-model="email"
              type="email"
              required
              autocomplete="email"
              placeholder="you@example.com"
            />
          </div>

          <!-- Password Field -->
          <div class="space-y-2">
            <div class="flex items-center justify-between">
              <Label for="password">Password</Label>
              <RouterLink to="/forgot-password" class="text-xs text-muted-foreground hover:text-foreground">
                Forgot password?
              </RouterLink>
            </div>
            <div class="relative">
              <Input
                id="password"
                v-model="password"
                :type="showPassword ? 'text' : 'password'"
                required
                autocomplete="current-password"
                placeholder="••••••••"
                class="pr-10"
              />
              <button
                type="button"
                class="absolute inset-y-0 right-0 flex items-center px-3 text-muted-foreground hover:text-foreground transition-colors cursor-pointer"
                @click="showPassword = !showPassword"
                :aria-label="showPassword ? 'Hide password' : 'Show password'"
              >
                <EyeOffIcon v-if="showPassword" class="size-4" />
                <EyeIcon v-else class="size-4" />
              </button>
            </div>
          </div>

          <!-- Remember Me -->
          <div class="flex items-center gap-2">
            <input
              id="remember-me"
              v-model="rememberMe"
              type="checkbox"
              class="h-4 w-4 rounded border-border text-primary focus:ring-primary"
            />
            <Label for="remember-me" class="font-normal cursor-pointer">Remember me</Label>
          </div>

          <!-- Submit Button -->
          <Button type="submit" :disabled="loading" class="w-full">
            <Loader2Icon v-if="loading" class="size-4 animate-spin" />
            {{ loading ? "Signing in…" : "Sign in" }}
          </Button>
        </form>

      </div>
    </div>
  </div>
</template>
