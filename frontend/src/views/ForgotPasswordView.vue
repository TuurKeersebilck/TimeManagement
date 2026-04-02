<script setup lang="ts">
import { ref } from "vue";
import { authService } from "../services/authService";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { Loader2Icon } from "lucide-vue-next";

const email = ref("");
const loading = ref(false);
const sent = ref(false);
const error = ref("");

const submit = async () => {
  error.value = "";
  loading.value = true;
  try {
    await authService.forgotPassword(email.value);
    sent.value = true;
  } catch {
    // Generic message — don't reveal server errors
    error.value = "Something went wrong. Please try again.";
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <div class="min-h-screen flex items-center justify-center py-12 px-4 bg-background">
    <div class="max-w-md w-full">
      <div class="text-center mb-8">
        <h2 class="text-3xl font-bold text-foreground">Forgot password?</h2>
        <p class="mt-2 text-sm text-muted-foreground">
          Enter your email and we'll send you a reset link
        </p>
      </div>

      <div class="bg-card text-card-foreground rounded-xl border border-border shadow-sm p-8">
        <!-- Success state -->
        <div v-if="sent" class="text-center space-y-3">
          <div class="w-12 h-12 rounded-full bg-emerald-100 dark:bg-emerald-950 flex items-center justify-center mx-auto">
            <svg class="size-6 text-emerald-600 dark:text-emerald-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M20 6 9 17l-5-5" />
            </svg>
          </div>
          <p class="text-sm font-medium text-slate-900 dark:text-slate-100">Check your inbox</p>
          <p class="text-sm text-slate-500 dark:text-slate-400">
            If <strong>{{ email }}</strong> is registered, you'll receive a reset link shortly.
            The link expires in 1 hour.
          </p>
          <RouterLink to="/login" class="inline-block mt-2 text-sm font-medium text-primary hover:underline">
            Back to sign in
          </RouterLink>
        </div>

        <!-- Form -->
        <form v-else @submit.prevent="submit" class="space-y-5">
          <div
            v-if="error"
            class="bg-destructive/10 border border-destructive/30 text-destructive text-sm px-4 py-3 rounded-lg"
          >
            {{ error }}
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

          <Button type="submit" :disabled="loading" class="w-full">
            <Loader2Icon v-if="loading" class="size-4 animate-spin" />
            {{ loading ? "Sending…" : "Send reset link" }}
          </Button>

          <div class="text-center">
            <RouterLink to="/login" class="text-sm text-muted-foreground hover:text-foreground">
              Back to sign in
            </RouterLink>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>
