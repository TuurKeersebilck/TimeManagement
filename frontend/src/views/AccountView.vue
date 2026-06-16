<script setup lang="ts">
import { ref, computed, watch } from "vue";
import { useRouter } from "vue-router";
import AuthenticatedLayout from "@/layouts/AuthenticatedLayout.vue";
import { authService, type ChangePasswordPayload, type UpdateProfilePayload } from "../services/authService";
import { calendarFeedService } from "../services/calendarFeedService";
import { useAuth } from "@/composables/useAuth";
import { useApiCall } from "@/composables/useApiCall";
import { useConfirmDialog } from "@/composables/useConfirmDialog";
import { useAppToast } from "@/composables/useAppToast";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { useTheme } from "@/composables/useTheme";
import { Loader2Icon, CheckIcon, KeyRoundIcon, UserIcon, SwatchBookIcon, CalendarIcon, CopyIcon, RefreshCwIcon, ExternalLinkIcon } from "lucide-vue-next";

const { isDark, toggleTheme, palette, togglePalette } = useTheme();
const { confirm } = useConfirmDialog();
const toast = useAppToast();

const router = useRouter();
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

const emailValid = computed(() => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(profile.value.email));
const profileCanSubmit = computed(() => profile.value.fullName.trim() && emailValid.value);

const {
  execute: saveProfile,
  loading: profileSaving,
  error: profileError,
} = useApiCall(() => authService.updateProfile(profile.value), {
  successMessage: "Profile updated",
  errorToast: true,
  onSuccess: () => fetchUser(true),
});

// ─── Password ─────────────────────────────────────────────────────────────────

const form = ref<ChangePasswordPayload>({
  currentPassword: "",
  newPassword: "",
  confirmPassword: "",
});

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

const {
  execute: submit,
  loading: saving,
  error,
} = useApiCall(() => authService.changePassword(form.value), {
  successMessage: "Password changed — please sign in again",
  errorToast: true,
  onSuccess: () => {
    authService.clearSession();
    router.push("/login");
  },
});

// ─── Calendar feed ────────────────────────────────────────────────────────────
// The raw token is only available at generation time (only the hash is stored server-side).
// We persist the feed URL in localStorage so the user can always see it without regenerating.

const calendarLoading = ref(false);
const calendarHasToken = ref(false);
const calendarExpiresAt = ref<string | null>(null);
const calendarFeedUrl = ref<string | null>(null);
const calendarUrlLost = ref(false);
const regenerating = ref(false);

function calendarStorageKey() {
  return `calendar_feed_url_${currentUser.value?.id ?? ""}`;
}

async function initCalendar() {
  calendarLoading.value = true;
  try {
    const info = await calendarFeedService.getTokenInfo();
    calendarHasToken.value = info.hasToken;
    calendarExpiresAt.value = info.expiresAt;

    if (info.hasToken) {
      const stored = localStorage.getItem(calendarStorageKey());
      if (stored) {
        calendarFeedUrl.value = stored;
      } else {
        calendarUrlLost.value = true;
      }
    }
  } catch {
    // silently ignore — feed section just stays hidden
  } finally {
    calendarLoading.value = false;
  }
}

watch(currentUser, (user) => { if (user) initCalendar(); }, { immediate: true });

async function copyFeedUrl() {
  if (!calendarFeedUrl.value) return;
  await navigator.clipboard.writeText(calendarFeedUrl.value);
  toast.success("Feed URL copied");
}

async function generateOrRegenerate(skipConfirm = false) {
  const run = async () => {
    regenerating.value = true;
    try {
      const result = await calendarFeedService.regenerateToken();
      calendarFeedUrl.value = result.feedUrl;
      calendarExpiresAt.value = result.expiresAt;
      calendarHasToken.value = true;
      calendarUrlLost.value = false;
      localStorage.setItem(calendarStorageKey(), result.feedUrl);
      toast.success(calendarHasToken.value ? "Calendar feed URL regenerated" : "Calendar feed URL generated");
    } catch {
      toast.error("Failed to generate calendar feed URL");
    } finally {
      regenerating.value = false;
    }
  };

  if (skipConfirm || !calendarHasToken.value) {
    await run();
    return;
  }

  confirm({
    title: "Regenerate calendar feed URL?",
    message:
      "Your current feed URL will stop working immediately. Any calendar app using it will need to be updated with the new URL.",
    confirmLabel: "Regenerate",
    cancelLabel: "Cancel",
    variant: "destructive",
    onConfirm: run,
  });
}

function formatExpiry(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: "numeric", month: "long", day: "numeric" });
}

function webcalUrl(feedUrl: string) {
  return feedUrl.replace(/^https?:\/\//, "webcal://");
}
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
        <!-- Appearance section -->
        <div class="card p-6 space-y-5 mt-6">
          <div class="flex items-center gap-3 pb-1">
            <div class="w-9 h-9 rounded-full bg-primary/10 flex items-center justify-center shrink-0">
              <SwatchBookIcon class="size-4 text-primary" />
            </div>
            <div>
              <p class="text-sm font-medium text-slate-900 dark:text-slate-100">Appearance</p>
              <p class="text-xs text-slate-500 dark:text-slate-400">Customise how the app looks</p>
            </div>
          </div>

          <div class="space-y-4">
            <div class="flex items-center justify-between">
              <div>
                <p class="text-sm font-medium text-foreground">Dark mode</p>
                <p class="text-xs text-muted-foreground mt-0.5">Switch between light and dark</p>
              </div>
              <Switch :model-value="isDark" @update:model-value="toggleTheme" />
            </div>

            <div class="border-t border-border" />

            <div class="flex items-center justify-between">
              <div>
                <p class="text-sm font-medium text-foreground">Palette</p>
                <p class="text-xs text-muted-foreground mt-0.5">
                  {{ palette === 'warm' ? 'Warm — amber & beige' : 'Slate — indigo & grey' }}
                </p>
              </div>
              <Switch :model-value="palette === 'slate'" @update:model-value="togglePalette" />
            </div>
          </div>
        </div>

        <!-- Calendar subscription section -->
        <div class="card p-6 space-y-5 mt-6">
          <div class="flex items-center gap-3 pb-1">
            <div class="w-9 h-9 rounded-full bg-indigo-100 dark:bg-indigo-950 flex items-center justify-center shrink-0">
              <CalendarIcon class="size-4 text-indigo-600 dark:text-indigo-400" />
            </div>
            <div>
              <p class="text-sm font-medium text-slate-900 dark:text-slate-100">Calendar subscription</p>
              <p class="text-xs text-slate-500 dark:text-slate-400">Subscribe to your vacation days in iOS, Google, or Outlook Calendar</p>
            </div>
          </div>

          <!-- Loading -->
          <div v-if="calendarLoading" class="flex items-center gap-2 text-sm text-slate-500 dark:text-slate-400">
            <Loader2Icon class="size-4 animate-spin" />
            Checking calendar feed…
          </div>

          <template v-else>
            <!-- No token yet -->
            <template v-if="!calendarHasToken">
              <p class="text-sm text-slate-500 dark:text-slate-400">
                Generate a private feed URL to subscribe to your vacation days in any calendar app.
              </p>
              <Button class="w-full" :disabled="regenerating" @click="generateOrRegenerate(true)">
                <Loader2Icon v-if="regenerating" class="size-4 animate-spin" />
                <CalendarIcon v-else class="size-4" />
                Get feed URL
              </Button>
            </template>

            <!-- Token exists but URL not in localStorage -->
            <template v-else-if="calendarUrlLost">
              <p class="text-sm text-slate-500 dark:text-slate-400">
                A feed URL is active but wasn't saved in this browser. Regenerate to get a new one (your calendar app will need to re-subscribe).
              </p>
              <p v-if="calendarExpiresAt" class="text-xs text-slate-500 dark:text-slate-400">
                Expires <span class="font-medium text-slate-700 dark:text-slate-300">{{ formatExpiry(calendarExpiresAt) }}</span>
              </p>
              <Button variant="outline" class="w-full" :disabled="regenerating" @click="generateOrRegenerate(false)">
                <Loader2Icon v-if="regenerating" class="size-4 animate-spin" />
                <RefreshCwIcon v-else class="size-4" />
                Regenerate URL
              </Button>
            </template>

            <!-- Token + URL known -->
            <template v-else-if="calendarFeedUrl">
              <!-- Feed URL -->
              <div class="space-y-1.5">
                <Label>Feed URL</Label>
                <div class="flex gap-2">
                  <Input :model-value="calendarFeedUrl" readonly class="font-mono text-xs" />
                  <Button variant="outline" size="icon" @click="copyFeedUrl" title="Copy URL">
                    <CopyIcon class="size-4" />
                  </Button>
                </div>
              </div>

              <!-- Subscribe link + expiry -->
              <div class="flex flex-col gap-2 text-sm">
                <a
                  :href="webcalUrl(calendarFeedUrl)"
                  class="inline-flex items-center gap-1.5 text-indigo-600 dark:text-indigo-400 hover:underline"
                >
                  <ExternalLinkIcon class="size-3.5" />
                  Subscribe in calendar app
                </a>
                <p v-if="calendarExpiresAt" class="text-xs text-slate-500 dark:text-slate-400">
                  Expires <span class="font-medium text-slate-700 dark:text-slate-300">{{ formatExpiry(calendarExpiresAt) }}</span> — re-subscribe when prompted
                </p>
              </div>

              <!-- Regenerate -->
              <Button variant="outline" :disabled="regenerating" @click="generateOrRegenerate(false)" class="w-full">
                <Loader2Icon v-if="regenerating" class="size-4 animate-spin" />
                <RefreshCwIcon v-else class="size-4" />
                Regenerate URL
              </Button>
            </template>
          </template>
        </div>

      </div>
    </div>
  </AuthenticatedLayout>
</template>
