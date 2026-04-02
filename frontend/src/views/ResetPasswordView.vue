<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { authService } from "../services/authService";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { Loader2Icon, CheckIcon } from "lucide-vue-next";

const route = useRoute();
const router = useRouter();

const token = ref("");
const newPassword = ref("");
const confirmPassword = ref("");
const loading = ref(false);
const done = ref(false);
const error = ref("");
const missingToken = ref(false);

onMounted(() => {
  const t = route.query.token as string | undefined;
  if (!t) {
    missingToken.value = true;
  } else {
    token.value = t;
  }
});

// ─── Strength ─────────────────────────────────────────────────────────────────

const strengthChecks = computed(() => ({
  length:    newPassword.value.length >= 8,
  uppercase: /[A-Z]/.test(newPassword.value),
  lowercase: /[a-z]/.test(newPassword.value),
  number:    /[0-9]/.test(newPassword.value),
}));

const strengthScore = computed(() => Object.values(strengthChecks.value).filter(Boolean).length);

const strengthColor = computed(() => {
  if (strengthScore.value <= 1) return "bg-red-500";
  if (strengthScore.value === 2) return "bg-amber-400";
  if (strengthScore.value === 3) return "bg-sky-400";
  return "bg-emerald-500";
});

const strengthLabel = computed(() => {
  if (!newPassword.value) return "";
  if (strengthScore.value <= 1) return "Weak";
  if (strengthScore.value === 2) return "Fair";
  if (strengthScore.value === 3) return "Good";
  return "Strong";
});

const passwordsMatch = computed(() =>
  !confirmPassword.value || newPassword.value === confirmPassword.value
);

const canSubmit = computed(() =>
  strengthScore.value >= 2 && newPassword.value === confirmPassword.value
);

// ─── Submit ───────────────────────────────────────────────────────────────────

const submit = async () => {
  error.value = "";
  loading.value = true;
  try {
    await authService.resetPassword(token.value, newPassword.value, confirmPassword.value);
    done.value = true;
    setTimeout(() => router.push("/login"), 3000);
  } catch (err: unknown) {
    error.value =
      (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
      "This reset link is invalid or has expired.";
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <div class="min-h-screen flex items-center justify-center py-12 px-4 bg-background">
    <div class="max-w-md w-full">
      <div class="text-center mb-8">
        <h2 class="text-3xl font-bold text-foreground">Set new password</h2>
        <p class="mt-2 text-sm text-muted-foreground">Choose a strong password for your account</p>
      </div>

      <div class="bg-card text-card-foreground rounded-xl border border-border shadow-sm p-8">
        <!-- Missing token -->
        <div v-if="missingToken" class="text-center space-y-3">
          <p class="text-sm text-slate-500 dark:text-slate-400">
            This link is invalid. Please request a new one.
          </p>
          <RouterLink to="/forgot-password" class="text-sm font-medium text-primary hover:underline">
            Request reset link
          </RouterLink>
        </div>

        <!-- Success -->
        <div v-else-if="done" class="text-center space-y-3">
          <div class="w-12 h-12 rounded-full bg-emerald-100 dark:bg-emerald-950 flex items-center justify-center mx-auto">
            <svg class="size-6 text-emerald-600 dark:text-emerald-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M20 6 9 17l-5-5" />
            </svg>
          </div>
          <p class="text-sm font-medium text-slate-900 dark:text-slate-100">Password updated!</p>
          <p class="text-sm text-slate-500 dark:text-slate-400">Redirecting you to sign in…</p>
        </div>

        <!-- Form -->
        <form v-else @submit.prevent="submit" class="space-y-5">
          <div
            v-if="error"
            class="bg-destructive/10 border border-destructive/30 text-destructive text-sm px-4 py-3 rounded-lg"
          >
            {{ error }}
            <RouterLink to="/forgot-password" class="block mt-1 font-medium hover:underline">
              Request a new link
            </RouterLink>
          </div>

          <!-- New password -->
          <div class="space-y-1.5">
            <Label>New password</Label>
            <Input
              v-model="newPassword"
              type="password"
              autocomplete="new-password"
              placeholder="••••••••"
            />

            <div v-if="newPassword" class="space-y-2 pt-1">
              <div class="flex gap-1">
                <div
                  v-for="i in 4"
                  :key="i"
                  :class="[
                    'h-1 flex-1 rounded-full transition-all duration-300',
                    i <= strengthScore ? strengthColor : 'bg-slate-200 dark:bg-slate-700',
                  ]"
                />
              </div>
              <p class="text-xs text-slate-500 dark:text-slate-400">
                Strength: <span class="font-medium text-slate-700 dark:text-slate-300">{{ strengthLabel }}</span>
              </p>
              <ul class="space-y-1">
                <li
                  v-for="[key, label] in [
                    ['length',    'At least 8 characters'],
                    ['uppercase', 'Uppercase letter'],
                    ['lowercase', 'Lowercase letter'],
                    ['number',    'Number'],
                  ]"
                  :key="key"
                  :class="[
                    'flex items-center gap-1.5 text-xs',
                    strengthChecks[key as keyof typeof strengthChecks]
                      ? 'text-emerald-600 dark:text-emerald-400'
                      : 'text-slate-400 dark:text-slate-500',
                  ]"
                >
                  <CheckIcon class="size-3 shrink-0" />
                  {{ label }}
                </li>
              </ul>
            </div>
          </div>

          <!-- Confirm -->
          <div class="space-y-1.5">
            <Label>Confirm password</Label>
            <Input
              v-model="confirmPassword"
              type="password"
              autocomplete="new-password"
              placeholder="••••••••"
              :class="!passwordsMatch ? 'border-destructive focus-visible:ring-destructive' : ''"
            />
            <p v-if="!passwordsMatch" class="text-xs text-destructive">Passwords do not match</p>
          </div>

          <Button type="submit" :disabled="loading || !canSubmit" class="w-full">
            <Loader2Icon v-if="loading" class="size-4 animate-spin" />
            {{ loading ? "Saving…" : "Set new password" }}
          </Button>
        </form>
      </div>
    </div>
  </div>
</template>
