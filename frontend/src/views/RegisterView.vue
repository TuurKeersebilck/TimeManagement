<script setup lang="ts">
import { ref, computed } from "vue";
import { useRouter } from "vue-router";
import { authService } from "../services/authService";
import { useAuth } from "../composables/useAuth";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { Loader2Icon } from "lucide-vue-next";

const router = useRouter();
const fullname = ref<string>("");
const email = ref<string>("");
const password = ref<string>("");
const confirmPassword = ref<string>("");
const loading = ref<boolean>(false);
const error = ref<string>("");
const { fetchUser } = useAuth();

const passwordsMatch = computed<boolean>(() => {
  return password.value === confirmPassword.value;
});

type PasswordStrength = "weak" | "medium" | "strong" | null;

const passwordStrength = computed<PasswordStrength>(() => {
  const pwd = password.value;
  if (pwd.length === 0) return null;
  if (pwd.length < 8) return "weak";
  if (pwd.length >= 8 && /[A-Z]/.test(pwd) && /[a-z]/.test(pwd) && /[0-9]/.test(pwd)) {
    return "strong";
  }
  return "medium";
});

const handleRegister = async (): Promise<void> => {
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
    const response = await authService.register({
      fullName: fullname.value,
      email: email.value,
      password: password.value,
      confirmPassword: password.value,
    });

    authService.setUserInfo(response.email, response.fullName, response.roles);

    await fetchUser(true);

    router.push("/");
  } catch (err) {
    error.value = (err as Error).message || "Registration failed. Please try again.";
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
        <h2 class="text-3xl font-bold text-foreground">Create your account</h2>
        <p class="mt-2 text-sm text-muted-foreground">Start tracking your time efficiently</p>
      </div>

      <!-- Register Form -->
      <div class="bg-card text-card-foreground rounded-xl border border-border shadow-sm p-8">
        <form @submit.prevent="handleRegister" class="space-y-5">
          <!-- Error Message -->
          <div
            v-if="error"
            class="bg-destructive/10 border border-destructive/30 text-destructive text-sm px-4 py-3 rounded-lg"
          >
            {{ error }}
          </div>

          <!-- Name Field -->
          <div class="space-y-2">
            <Label for="fullname">Full name</Label>
            <Input
              id="fullname"
              v-model="fullname"
              type="text"
              required
              autocomplete="name"
              placeholder="John Doe"
            />
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
            <Label for="password">Password</Label>
            <Input
              id="password"
              v-model="password"
              type="password"
              required
              autocomplete="new-password"
              placeholder="••••••••"
            />
            <!-- Password Strength Indicator -->
            <div v-if="passwordStrength" class="space-y-1">
              <div class="flex items-center gap-2">
                <div class="flex-1 bg-muted rounded-full h-1.5">
                  <div
                    class="h-1.5 rounded-full transition-all duration-300"
                    :class="{
                      'bg-destructive w-1/3': passwordStrength === 'weak',
                      'bg-yellow-500 w-2/3': passwordStrength === 'medium',
                      'bg-emerald-500 w-full': passwordStrength === 'strong',
                    }"
                  />
                </div>
                <span
                  class="text-xs font-medium capitalize"
                  :class="{
                    'text-destructive': passwordStrength === 'weak',
                    'text-yellow-600 dark:text-yellow-400': passwordStrength === 'medium',
                    'text-emerald-600 dark:text-emerald-400': passwordStrength === 'strong',
                  }"
                >
                  {{ passwordStrength }}
                </span>
              </div>
              <p class="text-xs text-muted-foreground">
                Use at least 8 characters with uppercase, lowercase, and numbers
              </p>
            </div>
          </div>

          <!-- Confirm Password Field -->
          <div class="space-y-2">
            <Label for="confirmPassword">Confirm password</Label>
            <Input
              id="confirmPassword"
              v-model="confirmPassword"
              type="password"
              required
              autocomplete="new-password"
              placeholder="••••••••"
              :class="
                confirmPassword && !passwordsMatch
                  ? 'border-destructive focus-visible:ring-destructive/50'
                  : ''
              "
            />
            <p v-if="confirmPassword && !passwordsMatch" class="text-xs text-destructive">
              Passwords do not match
            </p>
          </div>

          <!-- Submit Button -->
          <Button type="submit" :disabled="loading || !passwordsMatch" class="w-full">
            <Loader2Icon v-if="loading" class="size-4 animate-spin" />
            {{ loading ? "Creating account…" : "Create account" }}
          </Button>
        </form>

        <!-- Login Link -->
        <div class="mt-6 text-center">
          <p class="text-sm text-muted-foreground">
            Already have an account?
            <RouterLink to="/login" class="font-medium text-primary hover:underline">
              Sign in
            </RouterLink>
          </p>
        </div>
      </div>
    </div>
  </div>
</template>
