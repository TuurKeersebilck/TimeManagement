<script setup lang="ts">
import { ref, computed, onMounted, watch } from "vue";

declare const __APP_VERSION__: string;
const appVersion = __APP_VERSION__;
import { useRoute } from "vue-router";
import { useAuth } from "../composables/useAuth";
import { useChangelogStore } from "../composables/useChangelogStore";
import { usePendingAdjustments } from "../composables/usePendingAdjustments";
import { useAutoRefresh } from "../composables/useAutoRefresh";
import NotificationBell from "./NotificationBell.vue";
import ChangelogModal from "./ChangelogModal.vue";
import { LogOutIcon, ChevronDownIcon, GithubIcon, ScrollTextIcon } from "lucide-vue-next";

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

const isActive = (path: string) =>
  path === "/" ? route.path === "/" : route.path.startsWith(path);

interface NavItem {
  name: string;
  to: string;
}

const employeeNav: NavItem[] = [
  { name: "Clock In/Out", to: "/" },
  { name: "My Vacations", to: "/vacations" },
  { name: "Team Calendar", to: "/team-calendar" },
  { name: "Account", to: "/account" },
  { name: "Help", to: "/help" },
];

const adminPersonalNav: NavItem[] = [
  { name: "Dashboard", to: "/admin/dashboard" },
  { name: "My Vacations", to: "/vacations" },
  { name: "Team Calendar", to: "/team-calendar" },
  { name: "Account", to: "/account" },
  { name: "Help", to: "/help" },
];

const adminSectionNav: NavItem[] = [
  { name: "All Time Logs", to: "/admin/time-logs" },
  { name: "Settlements", to: "/admin/settlements" },
  { name: "Payroll Export", to: "/admin/export" },
];

// Less frequently used admin pages, tucked into a collapsed submenu.
const ADJUSTMENT_REQUESTS_PATH = "/admin/adjustment-requests";
const adminSettingsNav: NavItem[] = [
  { name: "Adjustment Requests", to: ADJUSTMENT_REQUESTS_PATH },
  { name: "Employees", to: "/admin/employees" },
  { name: "Vacation Types", to: "/admin/vacation-types" },
  { name: "App Settings", to: "/admin/settings" },
];

const navigationItems = computed(() => (isAdmin.value ? adminPersonalNav : employeeNav));

const adminSectionOpen = ref(true);

// Opens by itself when you land on one of its pages, so the active item is never hidden.
const settingsOpen = ref(false);
watch(
  () => route.path,
  () => {
    if (adminSettingsNav.some((item) => isActive(item.to))) settingsOpen.value = true;
  },
  { immediate: true }
);

// Pending adjustment requests stay visible even with the submenu collapsed.
const { pendingCount, refreshPendingCount } = usePendingAdjustments();
watch(
  isAdmin,
  (admin) => {
    if (admin) refreshPendingCount();
  },
  { immediate: true }
);
useAutoRefresh(async () => {
  if (isAdmin.value) await refreshPendingCount();
}, 120_000);

const handleNavClick = () => {
  if (window.innerWidth < 1024) emit("toggle");
};

const changelogOpen = ref(false);
const { entries: changelogEntries, hasUnread, fetchChangelog, markSeen } = useChangelogStore();

function openChangelog() {
  changelogOpen.value = true;
  markSeen();
}

onMounted(() => {
  fetchUser();
  fetchChangelog();
});
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
    <div v-if="isOpen" class="fixed inset-0 z-40 lg:hidden bg-black/30" @click="emit('toggle')" />
  </Transition>

  <!-- Sidebar -->
  <aside
    :class="[
      'fixed inset-y-0 left-0 z-50 flex flex-col bg-background border-r border-border transition-all duration-300 ease-out overflow-hidden',
      isOpen ? 'w-52 translate-x-0' : 'w-0 -translate-x-full lg:w-0 lg:-translate-x-full',
    ]"
  >
    <!-- App name -->
    <div class="shrink-0 px-6 pt-8 pb-2">
      <div class="flex items-start justify-between">
        <div>
          <span class="sidebar-wordmark">LOGR</span>
          <span class="sidebar-version">v.{{ appVersion }}</span>
        </div>
        <NotificationBell class="mt-1" />
      </div>
    </div>

    <!-- Navigation -->
    <nav class="flex-1 overflow-y-auto py-8 px-6">
      <!-- Personal nav links -->
      <ul class="space-y-1">
        <li v-for="item in navigationItems" :key="item.name">
          <router-link
            :to="item.to"
            @click="handleNavClick"
            :class="['sidebar-nav-link', isActive(item.to) && 'sidebar-nav-link-active']"
          >
            {{ item.name }}
          </router-link>
        </li>
      </ul>

      <!-- Administration section (admin only) -->
      <template v-if="isAdmin">
        <div class="mt-8 mb-3">
          <button
            class="w-full flex items-center justify-between text-xs uppercase tracking-widest text-muted-foreground hover:text-foreground transition-colors cursor-pointer"
            @click="adminSectionOpen = !adminSectionOpen"
          >
            <span>Admin</span>
            <ChevronDownIcon
              :class="[
                'size-3 transition-transform duration-200',
                adminSectionOpen && 'rotate-180',
              ]"
            />
          </button>
        </div>

        <div v-show="adminSectionOpen">
          <ul class="space-y-1">
            <li v-for="item in adminSectionNav" :key="item.name">
              <router-link
                :to="item.to"
                @click="handleNavClick"
                :class="['sidebar-nav-link', isActive(item.to) && 'sidebar-nav-link-active']"
              >
                {{ item.name }}
              </router-link>
            </li>
          </ul>

          <!-- Settings submenu -->
          <button
            class="sidebar-nav-link sidebar-nav-toggle mt-1"
            :aria-expanded="settingsOpen"
            @click="settingsOpen = !settingsOpen"
          >
            <span class="flex items-center justify-between gap-2">
              <span class="flex items-center gap-2">
                Settings
                <span
                  v-if="!settingsOpen && pendingCount > 0"
                  class="sidebar-badge"
                  :title="`${pendingCount} adjustment request(s) waiting for review`"
                  >{{ pendingCount }}</span
                >
              </span>
              <ChevronDownIcon
                :class="['size-3.5 transition-transform duration-200', settingsOpen && 'rotate-180']"
              />
            </span>
          </button>
          <ul v-show="settingsOpen" class="mt-1 ml-1 pl-3 border-l border-border space-y-0.5">
            <li v-for="item in adminSettingsNav" :key="item.name">
              <router-link
                :to="item.to"
                @click="handleNavClick"
                :class="[
                  'sidebar-nav-link sidebar-nav-sublink',
                  isActive(item.to) && 'sidebar-nav-link-active',
                ]"
              >
                <span class="flex items-center justify-between gap-2">
                  <span class="truncate">{{ item.name }}</span>
                  <span
                    v-if="item.to === ADJUSTMENT_REQUESTS_PATH && pendingCount > 0"
                    class="sidebar-badge"
                    :title="`${pendingCount} waiting for review`"
                    >{{ pendingCount }}</span
                  >
                </span>
              </router-link>
            </li>
          </ul>
        </div>
      </template>
    </nav>

    <!-- Bottom section -->
    <div class="shrink-0 px-6 pt-4 pb-6 border-t border-border space-y-4">
      <!-- User profile -->
      <div v-if="isLoadingUser" class="h-4 w-3/4 bg-muted rounded animate-pulse" />
      <div v-else-if="currentUser" class="flex items-center justify-between gap-2">
        <div class="flex items-center gap-2 min-w-0">
          <div class="w-6 h-6 user-avatar shrink-0">
            <span class="text-[10px] font-bold text-white">{{ userInitials }}</span>
          </div>
          <span class="text-xs text-muted-foreground truncate">{{ currentUser.fullName }}</span>
        </div>
        <div class="flex items-center gap-2 shrink-0">
          <button
            @click="openChangelog"
            class="relative text-muted-foreground hover:text-foreground transition-colors cursor-pointer"
            title="What's new"
          >
            <ScrollTextIcon class="size-3.5" />
            <span
              v-if="hasUnread"
              class="absolute -top-0.5 -right-0.5 size-1.5 rounded-full bg-primary"
            />
          </button>
          <a
            href="https://github.com/TuurKeersebilck/TimeManagement/issues"
            target="_blank"
            rel="noopener noreferrer"
            class="text-muted-foreground hover:text-foreground transition-colors"
            title="Report a bug or request a feature"
          >
            <GithubIcon class="size-3.5" />
          </a>
          <button
            @click="emit('logout')"
            class="text-muted-foreground hover:text-destructive transition-colors cursor-pointer"
            title="Sign out"
          >
            <LogOutIcon class="size-3.5" />
          </button>
        </div>
      </div>
    </div>
  </aside>

  <ChangelogModal v-model:open="changelogOpen" :entries="changelogEntries" />
</template>

<style scoped>
.sidebar-wordmark {
  display: block;
  font-family: var(--font-logo);
  font-size: 1.75rem;
  font-weight: 700;
  letter-spacing: 0.02em;
  line-height: 1.1;
  color: var(--foreground);
}

.sidebar-version {
  display: block;
  font-family: var(--font-body);
  font-size: 0.7rem;
  font-weight: 400;
  letter-spacing: 0.05em;
  color: var(--muted-foreground);
  margin-top: 0.1rem;
}

.sidebar-nav-link {
  display: block;
  font-family: var(--font-data);
  font-size: 1.0625rem;
  font-weight: 400;
  letter-spacing: 0.01em;
  color: var(--muted-foreground);
  padding: 0.35rem 0;
  border-left: 2px solid transparent;
  padding-left: 0.75rem;
  margin-left: -0.75rem;
  transition:
    color 0.15s,
    border-color 0.15s;
  cursor: pointer;
}

.sidebar-nav-link:hover {
  color: var(--foreground);
}

.sidebar-nav-link-active {
  color: var(--primary);
  border-left-color: var(--primary);
  font-weight: 600;
}

.sidebar-nav-sublink {
  font-size: 0.9375rem;
}

/* A <button> doesn't stretch like the block links do; match their width (incl. the
   negative left margin) and reset native button styling. */
.sidebar-nav-toggle {
  width: calc(100% + 0.75rem);
  text-align: left;
  background: none;
}

.sidebar-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 1.125rem;
  height: 1.125rem;
  padding: 0 0.3rem;
  border-radius: 9999px;
  background: var(--primary);
  color: var(--primary-foreground);
  font-family: var(--font-body);
  font-size: 0.6875rem;
  font-weight: 600;
  line-height: 1;
}
</style>
