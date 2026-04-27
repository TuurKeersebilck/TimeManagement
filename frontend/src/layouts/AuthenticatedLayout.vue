<script setup lang="ts">
import { useRouter } from "vue-router";
import Sidebar from "@/components/Sidebar.vue";
import AppLogo from "@/components/AppLogo.vue";
import NotificationBell from "@/components/NotificationBell.vue";
import { authService } from "../services/authService";
import { useAuth } from "../composables/useAuth";
import { useSidebar } from "../composables/useSidebar";
import { MenuIcon } from "lucide-vue-next";

const router = useRouter();
const { sidebarOpen, toggle: toggleSidebar } = useSidebar();
const { clearUser, isAdmin } = useAuth();

const handleLogout = async (): Promise<void> => {
  clearUser();
  await authService.logout();
  router.push("/login");
};
</script>

<template>
  <div class="flex h-screen bg-background">
    <Sidebar :is-open="sidebarOpen" @toggle="toggleSidebar" @logout="handleLogout" />

    <!-- Main content -->
    <div
      :class="[
        'flex-1 flex flex-col min-w-0 transition-all duration-300 ease-out',
        sidebarOpen ? 'lg:ml-52' : 'lg:ml-0',
      ]"
    >
      <!-- Mobile top bar -->
      <header
        class="lg:hidden flex items-center h-14 px-4 bg-card border-b border-border shrink-0"
      >
        <button @click="toggleSidebar" class="btn-ghost !px-2 !py-2" aria-label="Open menu">
          <MenuIcon class="size-5" />
        </button>
        <AppLogo class="ml-3 flex-1 min-w-0" />
        <NotificationBell />
      </header>

      <!-- Page content -->
      <main class="flex-1 overflow-y-auto">
        <slot />
      </main>

    </div>
  </div>
</template>
