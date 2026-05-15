<script setup lang="ts">
import { ref } from "vue";
import { useRouter } from "vue-router";
import { setupService } from "@/services/setupService";
import { extractApiError } from "@/utils/apiError";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { Loader2Icon, EyeIcon, EyeOffIcon } from "lucide-vue-next";

const router = useRouter();

const fullName = ref("");
const email = ref("");
const password = ref("");
const confirmPassword = ref("");
const showPassword = ref(false);
const loading = ref(false);
const error = ref("");

const handleSetup = async () => {
  error.value = "";

  if (password.value !== confirmPassword.value) {
    error.value = "Passwords do not match";
    return;
  }

  loading.value = true;
  try {
    await setupService.complete({
      fullName: fullName.value,
      email: email.value,
      password: password.value,
      confirmPassword: confirmPassword.value,
    });
    router.push("/login");
  } catch (err) {
    error.value = extractApiError(err, "Setup failed. Please try again.");
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <div class="min-h-screen flex items-center justify-center py-12 px-4 bg-background">
    <div class="max-w-md w-full">
      <div class="text-center mb-8">
        <h2 class="text-3xl font-bold text-foreground">Welcome</h2>
        <p class="mt-2 text-sm text-muted-foreground">
          Create your admin account to get started
        </p>
      </div>

      <div class="bg-card text-card-foreground rounded-xl border border-border shadow-sm p-8">
        <form @submit.prevent="handleSetup" class="space-y-5">
          <div
            v-if="error"
            class="bg-destructive/10 border border-destructive/30 text-destructive text-sm px-4 py-3 rounded-lg"
          >
            {{ error }}
          </div>

          <div class="space-y-2">
            <Label for="fullName">Full name</Label>
            <Input
              id="fullName"
              v-model="fullName"
              type="text"
              required
              autocomplete="name"
              placeholder="Jane Smith"
            />
          </div>

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
          </div>

          <div class="space-y-2">
            <Label for="confirmPassword">Confirm password</Label>
            <Input
              id="confirmPassword"
              v-model="confirmPassword"
              :type="showPassword ? 'text' : 'password'"
              required
              autocomplete="new-password"
              placeholder="••••••••"
            />
          </div>

          <Button type="submit" :disabled="loading" class="w-full">
            <Loader2Icon v-if="loading" class="size-4 animate-spin" />
            {{ loading ? "Creating account…" : "Create admin account" }}
          </Button>
        </form>
      </div>
    </div>
  </div>
</template>
