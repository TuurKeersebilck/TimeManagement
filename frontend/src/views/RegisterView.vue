<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRouter, useRoute } from "vue-router";
import { inviteService } from "../services/inviteService";
import { authService } from "../services/authService";
import { useAuth } from "../composables/useAuth";
import { extractApiError } from "@/utils/apiError";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { Loader2Icon, EyeIcon, EyeOffIcon } from "lucide-vue-next";

const router = useRouter();
const route = useRoute();
const { fetchUser } = useAuth();

const token = ref<string>("");
const inviteEmail = ref<string>("");
const tokenValid = ref<boolean | null>(null); // null = loading
const tokenError = ref<string>("");

const fullName = ref<string>("");
const password = ref<string>("");
const confirmPassword = ref<string>("");
const loading = ref<boolean>(false);
const error = ref<string>("");
const showPassword = ref(false);
const showConfirmPassword = ref(false);

const passwordsMatch = computed(() => password.value === confirmPassword.value);

type PasswordStrength = "weak" | "medium" | "strong" | null;
const passwordStrength = computed<PasswordStrength>(() => {
  const pwd = password.value;
  if (pwd.length === 0) return null;
  if (pwd.length < 8) return "weak";
  if (pwd.length >= 8 && /[A-Z]/.test(pwd) && /[a-z]/.test(pwd) && /[0-9]/.test(pwd)) return "strong";
  return "medium";
});

onMounted(async () => {
  const rawToken = route.query.token as string | undefined;
  if (!rawToken) {
    tokenValid.value = false;
    tokenError.value = "No invitation token found. Please use the link from your invitation email.";
    return;
  }
  token.value = rawToken;
  try {
    const result = await inviteService.validateToken(rawToken);
    inviteEmail.value = result.email;
    tokenValid.value = true;
  } catch {
    tokenValid.value = false;
    tokenError.value = "This invitation link is invalid or has expired. Please ask your admin to send a new invite.";
  }
});

const handleAccept = async (): Promise<void> => {
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
    const response = await inviteService.acceptInvite({
      token: token.value,
      fullName: fullName.value,
      password: password.value,
      confirmPassword: confirmPassword.value,
    }) as { email: string; fullName: string; roles: string[] };

    authService.setUserInfo(response.email, response.fullName, response.roles);
    await fetchUser(true);
    router.push("/");
  } catch (err) {
    error.value = extractApiError(err, "Failed to create account. Please try again.");
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
        <p class="mt-2 text-sm text-muted-foreground">Complete your invitation to get started</p>
      </div>

      <div class="bg-card text-card-foreground rounded-xl border border-border shadow-sm p-8">
        <!-- Loading -->
        <div v-if="tokenValid === null" class="flex justify-center py-8">
          <Loader2Icon class="size-6 animate-spin text-muted-foreground" />
        </div>

        <!-- Invalid token -->
        <div v-else-if="tokenValid === false" class="text-center py-4 space-y-4">
          <p class="text-destructive text-sm">{{ tokenError }}</p>
          <RouterLink to="/login" class="text-sm font-medium text-primary hover:underline">
            Back to sign in
          </RouterLink>
        </div>

        <!-- Registration form -->
        <form v-else @submit.prevent="handleAccept" class="space-y-5">
          <!-- Error message -->
          <div
            v-if="error"
            class="bg-destructive/10 border border-destructive/30 text-destructive text-sm px-4 py-3 rounded-lg"
          >
            {{ error }}
          </div>

          <!-- Email (read-only) -->
          <div class="space-y-2">
            <Label for="email">Email address</Label>
            <Input
              id="email"
              :value="inviteEmail"
              type="email"
              disabled
              class="bg-muted cursor-not-allowed"
            />
          </div>

          <!-- Full name -->
          <div class="space-y-2">
            <Label for="fullname">Full name</Label>
            <Input
              id="fullname"
              v-model="fullName"
              type="text"
              required
              autocomplete="name"
              placeholder="John Doe"
            />
          </div>

          <!-- Password -->
          <div class="space-y-2">
            <Label for="password">Password</Label>
            <div class="relative">
              <Input
                id="password"
                v-model="password"
                :type="showPassword ? 'text' : 'password'"
                required
                autocomplete="new-password"
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

          <!-- Confirm password -->
          <div class="space-y-2">
            <Label for="confirmPassword">Confirm password</Label>
            <div class="relative">
              <Input
                id="confirmPassword"
                v-model="confirmPassword"
                :type="showConfirmPassword ? 'text' : 'password'"
                required
                autocomplete="new-password"
                placeholder="••••••••"
                class="pr-10"
                :class="confirmPassword && !passwordsMatch ? 'border-destructive focus-visible:ring-destructive/50' : ''"
              />
              <button
                type="button"
                class="absolute inset-y-0 right-0 flex items-center px-3 text-muted-foreground hover:text-foreground transition-colors cursor-pointer"
                @click="showConfirmPassword = !showConfirmPassword"
                :aria-label="showConfirmPassword ? 'Hide password' : 'Show password'"
              >
                <EyeOffIcon v-if="showConfirmPassword" class="size-4" />
                <EyeIcon v-else class="size-4" />
              </button>
            </div>
            <p v-if="confirmPassword && !passwordsMatch" class="text-xs text-destructive">
              Passwords do not match
            </p>
          </div>

          <Button type="submit" :disabled="loading || !passwordsMatch" class="w-full">
            <Loader2Icon v-if="loading" class="size-4 animate-spin" />
            {{ loading ? "Creating account…" : "Create account" }}
          </Button>
        </form>

        <div class="mt-6 text-center">
          <p class="text-sm text-muted-foreground">
            Already have an account?
            <RouterLink to="/login" class="font-medium text-primary hover:underline">Sign in</RouterLink>
          </p>
        </div>
      </div>
    </div>
  </div>
</template>
