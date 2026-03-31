<script setup lang="ts">
import { ref } from "vue";
import { useRouter } from "vue-router";
import Sidebar from "@/components/Sidebar.vue";
import { authService } from "../services/authService";
import { useAuth } from "../composables/useAuth";
import { MenuIcon } from "lucide-vue-next";

const router = useRouter();
const sidebarOpen = ref<boolean>(true);
const { clearUser } = useAuth();

const handleLogout = async (): Promise<void> => {
  clearUser();
  await authService.logout();
  router.push("/login");
};

const toggleSidebar = (): void => {
  sidebarOpen.value = !sidebarOpen.value;
};
</script>

<template>
  <div class="flex h-screen bg-slate-50 dark:bg-slate-950">
    <Sidebar :is-open="sidebarOpen" @toggle="toggleSidebar" @logout="handleLogout" />

    <!-- Main content -->
    <div
      :class="[
        'flex-1 flex flex-col min-w-0 transition-all duration-300 ease-out',
        sidebarOpen ? 'lg:ml-64' : 'lg:ml-16',
      ]"
    >
      <!-- Mobile top bar -->
      <header
        class="lg:hidden flex items-center h-14 px-4 bg-white dark:bg-slate-900 border-b border-slate-200 dark:border-slate-800 shrink-0"
      >
        <button @click="toggleSidebar" class="btn-ghost !px-2 !py-2" aria-label="Open menu">
          <MenuIcon class="size-5" />
        </button>
        <span class="ml-3 text-sm font-semibold text-slate-900 dark:text-slate-100">
          Time Management
        </span>
      </header>

      <!-- Page content -->
      <main class="flex-1 overflow-y-auto">
        <slot />
      </main>
    </div>
  </div>
</template>
