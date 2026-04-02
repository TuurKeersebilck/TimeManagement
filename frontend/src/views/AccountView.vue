<script setup lang="ts">
import { ref, computed, watch } from "vue";
import { useRouter } from "vue-router";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { authService, type ChangePasswordPayload, type UpdateProfilePayload } from "../services/authService";
import { useAuth } from "@/composables/useAuth";
import { useAppToast } from "@/composables/useAppToast";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Loader2Icon, CheckIcon, KeyRoundIcon, UserIcon } from "lucide-vue-next";

const router = useRouter();
const toast = useAppToast();
const { currentUser, fetchUser } = useAuth();

// ─── Profile ──────────────────────────────────────────────────────────────────

const profile = ref<UpdateProfilePayload>({ fullName: "", email: "" });

watch(
  currentUser,
  (user) => {
    if (user) {
      profile.value = { fullName: user.fullName, email: user.email };
    }
  },
  { immediate: true }
);

const profileSaving = ref(false);
const profileError = ref("");

const emailValid = computed(() => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(profile.value.email));
const profileCanSubmit = computed(() => profile.value.fullName.trim() && emailValid.value);

const saveProfile = async () => {
  profileError.value = "";
  profileSaving.value = true;
  try {
    await authService.updateProfile(profile.value);
    await fetchUser(true);
    toast.success("Profile updated");
  } catch (err: unknown) {
    profileError.value =
      (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
      "Failed to update profile";
  } finally {
    profileSaving.value = false;
  }
};

// ─── Password ─────────────────────────────────────────────────────────────────

const form = ref<ChangePasswordPayload>({
  currentPassword: "",
  newPassword: "",
  confirmPassword: "",
});

const saving = ref(false);
const error = ref("");

const strengthChecks = computed(() => ({
  length:    form.value.newPassword.length >= 8,
  uppercase: /[A-Z]/.test(form.value.newPassword),
  lowercase: /[a-z]/.test(form.value.newPassword),
  number:    /[0-9]/.test(form.value.newPassword),
}));

const strengthScore = computed(() => Object.values(strengthChecks.value).filter(Boolean).length);

const strengthLabel = computed(() => {
  if (!form.value.newPassword) return "";
  if (strengthScore.value <= 1) return "Weak";
  if (strengthScore.value === 2) return "Fair";
  if (strengthScore.value === 3) return "Good";
  return "Strong";
});

const strengthColor = computed(() => {
  if (strengthScore.value <= 1) return "bg-red-500";
  if (strengthScore.value === 2) return "bg-amber-400";
  if (strengthScore.value === 3) return "bg-sky-400";
  return "bg-emerald-500";
});

const passwordsMatch = computed(() =>
  !form.value.confirmPassword || form.value.newPassword === form.value.confirmPassword
);

const canSubmit = computed(() =>
  form.value.currentPassword &&
  strengthScore.value >= 3 &&
  form.value.newPassword === form.value.confirmPassword
);

const submit = async () => {
  error.value = "";
  saving.value = true;
  try {
    await authService.changePassword(form.value);
    toast.success("Password changed — please sign in again");
    authService.clearSession();
    router.push("/login");
  } catch (err: unknown) {
    error.value =
      (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
      "Failed to change password";
  } finally {
    saving.value = false;
  }
};
</script>

<template>
  <AuthenticatedLayout>
    <div class="p-6 lg:p-8">
      <div class="max-w-md mx-auto">
        <!-- Header -->
        <div class="mb-8">
          <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Account</h1>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-0.5">Manage your profile and security settings</p>
        </div>

        <!-- Profile section -->
        <div class="card p-6 space-y-5 mb-6">
          <div class="flex items-center gap-3 pb-1">
            <div class="w-9 h-9 rounded-full bg-indigo-100 dark:bg-indigo-950 flex items-center justify-center shrink-0">
              <UserIcon class="size-4 text-indigo-600 dark:text-indigo-400" />
            </div>
            <div>
              <p class="text-sm font-medium text-slate-900 dark:text-slate-100">Profile</p>
              <p class="text-xs text-slate-500 dark:text-slate-400">Update your name and email address</p>
            </div>
          </div>

          <!-- Error -->
          <div
            v-if="profileError"
            class="bg-destructive/10 border border-destructive/30 text-destructive text-sm px-4 py-3 rounded-lg"
          >
            {{ profileError }}
          </div>

          <!-- Full name -->
          <div class="space-y-1.5">
            <Label>Full name</Label>
            <Input v-model="profile.fullName" type="text" autocomplete="name" placeholder="Jane Doe" />
            <p v-if="!profile.fullName.trim()" class="text-xs text-destructive">Name is required</p>
          </div>

          <!-- Email -->
          <div class="space-y-1.5">
            <Label>Email address</Label>
            <Input
              v-model="profile.email"
              type="email"
              autocomplete="email"
              placeholder="jane@example.com"
              :class="profile.email && !emailValid ? 'border-destructive focus-visible:ring-destructive' : ''"
            />
            <p v-if="profile.email && !emailValid" class="text-xs text-destructive">Enter a valid email address</p>
          </div>

          <Button class="w-full" :disabled="profileSaving || !profileCanSubmit" @click="saveProfile">
            <Loader2Icon v-if="profileSaving" class="size-4 animate-spin" />
            Save profile
          </Button>
        </div>

        <!-- Password section -->
        <div class="card p-6 space-y-5">
          <div class="flex items-center gap-3 pb-1">
            <div class="w-9 h-9 rounded-full bg-indigo-100 dark:bg-indigo-950 flex items-center justify-center shrink-0">
              <KeyRoundIcon class="size-4 text-indigo-600 dark:text-indigo-400" />
            </div>
            <div>
              <p class="text-sm font-medium text-slate-900 dark:text-slate-100">Change password</p>
              <p class="text-xs text-slate-500 dark:text-slate-400">You'll be signed out after changing</p>
            </div>
          </div>

          <!-- Error -->
          <div
            v-if="error"
            class="bg-destructive/10 border border-destructive/30 text-destructive text-sm px-4 py-3 rounded-lg"
          >
            {{ error }}
          </div>

          <!-- Current password -->
          <div class="space-y-1.5">
            <Label>Current password</Label>
            <Input
              v-model="form.currentPassword"
              type="password"
              autocomplete="current-password"
              placeholder="••••••••"
            />
          </div>

          <!-- New password -->
          <div class="space-y-1.5">
            <Label>New password</Label>
            <Input
              v-model="form.newPassword"
              type="password"
              autocomplete="new-password"
              placeholder="••••••••"
            />

            <!-- Strength bar -->
            <div v-if="form.newPassword" class="space-y-2 pt-1">
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
              <!-- Checklist -->
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

          <!-- Confirm password -->
          <div class="space-y-1.5">
            <Label>Confirm new password</Label>
            <Input
              v-model="form.confirmPassword"
              type="password"
              autocomplete="new-password"
              placeholder="••••••••"
              :class="!passwordsMatch ? 'border-destructive focus-visible:ring-destructive' : ''"
            />
            <p v-if="!passwordsMatch" class="text-xs text-destructive">Passwords do not match</p>
          </div>

          <Button class="w-full" :disabled="saving || !canSubmit" @click="submit">
            <Loader2Icon v-if="saving" class="size-4 animate-spin" />
            Change password
          </Button>
        </div>
      </div>
    </div>
  </AuthenticatedLayout>
</template>
