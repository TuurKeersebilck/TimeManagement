<script setup lang="ts">
import { ref, computed, onMounted, type Component } from "vue";
import { useRoute } from "vue-router";
import { useAuth } from "../composables/useAuth";
import { useTheme } from "../composables/useTheme";
import NotificationBell from "./NotificationBell.vue";
import {
  LayoutDashboardIcon,
  ClockIcon,
  CalendarIcon,
  CalendarDaysIcon,
  TagIcon,
  UsersIcon,
  SunIcon,
  MoonIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  ChevronDownIcon,
  LogOutIcon,
  KeyRoundIcon,
  SettingsIcon,
  DownloadIcon,
} from "lucide-vue-next";

interface Props {
  isOpen?: boolean;
}

withDefaults(defineProps<Props>(), {
  isOpen: true,
});

const emit = defineEmits<{
  toggle: [];
  logout: [];
}>();

const route = useRoute();
const { currentUser, isLoadingUser, isAdmin, userInitials, fetchUser } = useAuth();
const { isDark, toggleTheme } = useTheme();

const isActive = (path: string) =>
  path === "/" ? route.path === "/" : route.path.startsWith(path);

interface NavItem {
  name: string;
  to: string;
  icon: Component;
}

const employeeNav: NavItem[] = [
  { name: "Dashboard", to: "/", icon: LayoutDashboardIcon },
  { name: "My Time Logs", to: "/time-tracking", icon: ClockIcon },
  { name: "My Vacations", to: "/vacations", icon: CalendarIcon },
  { name: "Team Calendar", to: "/team-calendar", icon: CalendarDaysIcon },
  { name: "Account", to: "/account", icon: KeyRoundIcon },
];

const adminPersonalNav: NavItem[] = [
  { name: "Dashboard", to: "/admin/dashboard", icon: LayoutDashboardIcon },
  { name: "My Vacations", to: "/vacations", icon: CalendarIcon },
  { name: "Team Calendar", to: "/team-calendar", icon: CalendarDaysIcon },
];

const adminSectionNav: NavItem[] = [
  { name: "All Time Logs", to: "/admin/time-logs", icon: ClockIcon },
  { name: "Vacation Types", to: "/admin/vacation-types", icon: TagIcon },
  { name: "Employees", to: "/admin/employees", icon: UsersIcon },
  { name: "Payroll Export", to: "/admin/export", icon: DownloadIcon },
  { name: "App Settings", to: "/admin/settings", icon: SettingsIcon },
];

const navigationItems = computed(() => (isAdmin.value ? adminPersonalNav : employeeNav));

const adminSectionOpen = ref(route.path.startsWith("/admin/") && route.path !== "/admin/dashboard");

const handleNavClick = () => {
  if (window.innerWidth < 1024) emit("toggle");
};

onMounted(() => fetchUser());
</script>

<template>
  <!-- Mobile backdrop -->
  <Transition
    enter-active-class="transition-opacity duration-300 ease-out"
    leave-active-class="transition-opacity duration-300 ease-in"
    enter-from-class="opacity-0"
    enter-to-class="opacity-100"
    leave-from-class="opacity-100"
    leave-to-class="opacity-0"
  >
    <div
      v-if="isOpen"
      class="fixed inset-0 z-40 lg:hidden bg-black/40 backdrop-blur-sm"
      @click="emit('toggle')"
    />
  </Transition>

  <!-- Sidebar -->
  <aside
    :class="[
      'fixed inset-y-0 left-0 z-50 flex flex-col bg-white dark:bg-slate-900 border-r border-slate-200 dark:border-slate-800 transition-all duration-300 ease-out overflow-hidden',
      isOpen ? 'w-64 translate-x-0' : 'w-0 -translate-x-full lg:w-16 lg:translate-x-0',
    ]"
  >
    <!-- Header -->
    <div
      class="flex items-center h-16 px-4 border-b border-slate-200 dark:border-slate-800 shrink-0"
    >
      <span
        v-if="isOpen"
        class="flex-1 text-base font-semibold text-slate-900 dark:text-slate-100 truncate"
      >
        Time Management
      </span>
      <NotificationBell v-if="isAdmin" />
      <button
        @click="emit('toggle')"
        class="btn-ghost !px-2 !py-2 shrink-0"
        :title="isOpen ? 'Collapse sidebar' : 'Expand sidebar'"
      >
        <ChevronLeftIcon v-if="isOpen" class="size-5" />
        <ChevronRightIcon v-else class="size-5" />
      </button>
    </div>

    <!-- Navigation -->
    <nav class="flex-1 overflow-y-auto py-4 px-2">
      <!-- Role label -->
      <p
        v-if="isOpen"
        class="px-3 mb-2 text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500"
      >
        {{ isAdmin ? "Admin" : "Employee" }}
      </p>

      <!-- Personal nav links (all roles) -->
      <div class="space-y-0.5">
        <router-link
          v-for="item in navigationItems"
          :key="item.name"
          :to="item.to"
          @click="handleNavClick"
          :class="[
            'nav-link',
            !isOpen && 'lg:justify-center lg:!px-0',
            isActive(item.to) && 'nav-link-active',
          ]"
          :title="!isOpen ? item.name : undefined"
        >
          <component :is="item.icon" class="size-[18px] shrink-0" />
          <span v-if="isOpen" class="truncate">{{ item.name }}</span>
        </router-link>
      </div>

      <!-- Administration section (admin only) -->
      <template v-if="isAdmin">
        <!-- Section divider + toggle -->
        <div :class="['mt-4 mb-1', !isOpen && 'lg:hidden']">
          <button
            class="w-full flex items-center justify-between px-3 py-1 text-xs font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500 hover:text-slate-600 dark:hover:text-slate-300 transition-colors cursor-pointer"
            @click="adminSectionOpen = !adminSectionOpen"
          >
            <span>Manage</span>
            <ChevronDownIcon
              :class="[
                'size-3.5 transition-transform duration-200',
                adminSectionOpen && 'rotate-180',
              ]"
            />
          </button>
        </div>
        <!-- Collapsed sidebar: just show icons with a faint divider -->
        <div
          v-if="!isOpen"
          class="hidden lg:block mt-3 mb-1 mx-3 border-t border-slate-200 dark:border-slate-700"
        />

        <div v-show="adminSectionOpen || !isOpen" class="space-y-0.5">
          <router-link
            v-for="item in adminSectionNav"
            :key="item.name"
            :to="item.to"
            @click="handleNavClick"
            :class="[
              'nav-link',
              !isOpen && 'lg:justify-center lg:!px-0',
              isActive(item.to) && 'nav-link-active',
            ]"
            :title="!isOpen ? item.name : undefined"
          >
            <component :is="item.icon" class="size-[18px] shrink-0" />
            <span v-if="isOpen" class="truncate">{{ item.name }}</span>
          </router-link>
        </div>
      </template>
    </nav>

    <!-- Bottom section -->
    <div class="shrink-0 border-t border-slate-200 dark:border-slate-800">
      <!-- Theme toggle -->
      <div class="px-2 pt-3 pb-1">
        <button
          @click="toggleTheme"
          :class="['nav-link w-full', !isOpen && 'lg:justify-center lg:!px-0']"
          :title="isDark ? 'Switch to light mode' : 'Switch to dark mode'"
        >
          <SunIcon v-if="isDark" class="size-[18px] shrink-0" />
          <MoonIcon v-else class="size-[18px] shrink-0" />
          <span v-if="isOpen">{{ isDark ? "Light mode" : "Dark mode" }}</span>
        </button>
      </div>

      <!-- User profile -->
      <div class="px-2 pb-2">
        <!-- Loading skeleton -->
        <div v-if="isLoadingUser" class="flex items-center gap-3 px-3 py-2">
          <div class="w-8 h-8 rounded-full bg-slate-200 dark:bg-slate-700 animate-pulse shrink-0" />
          <div v-if="isOpen" class="flex-1 space-y-1.5">
            <div class="h-3 bg-slate-200 dark:bg-slate-700 rounded animate-pulse" />
            <div class="h-2.5 bg-slate-200 dark:bg-slate-700 rounded w-3/4 animate-pulse" />
          </div>
        </div>

        <!-- User info + logout -->
        <div v-else-if="currentUser">
          <div
            :class="[
              'flex items-center gap-3 px-3 py-2 rounded-lg',
              isOpen ? '' : 'lg:justify-center',
            ]"
          >
            <div
              class="w-8 h-8 user-avatar shrink-0"
              :title="!isOpen ? currentUser.fullName : undefined"
            >
              <span class="text-xs font-bold text-white">{{ userInitials }}</span>
            </div>
            <div v-if="isOpen" class="flex-1 min-w-0">
              <p class="text-sm font-medium text-slate-900 dark:text-slate-100 truncate">
                {{ currentUser.fullName }}
              </p>
              <p class="text-xs text-slate-500 dark:text-slate-400 truncate">
                {{ currentUser.email }}
              </p>
            </div>
            <button
              v-if="isOpen"
              @click="emit('logout')"
              class="btn-ghost !px-2 !py-1.5 shrink-0 text-slate-400 hover:text-red-500 dark:hover:text-red-400"
              title="Sign out"
            >
              <LogOutIcon class="size-4" />
            </button>
          </div>
          <!-- Logout when collapsed -->
          <button
            v-if="!isOpen"
            @click="emit('logout')"
            class="nav-link w-full lg:justify-center lg:!px-0 text-slate-400 hover:text-red-500 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-950"
            title="Sign out"
          >
            <LogOutIcon class="size-[18px]" />
          </button>
        </div>
      </div>
    </div>
  </aside>
</template>
