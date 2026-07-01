<script setup lang="ts">
import { ref, computed, onMounted } from "vue";

declare const __APP_VERSION__: string;
const appVersion = __APP_VERSION__;
import { useRoute } from "vue-router";
import { useAuth } from "../composables/useAuth";
import NotificationBell from "./NotificationBell.vue";
import ChangelogModal, { type ChangelogEntry, type ChangelogCategory } from "./ChangelogModal.vue";
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
  { name: "Dashboard", to: "/" },
  { name: "Clock In/Out", to: "/time-tracking" },
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
  { name: "Adjustment Requests", to: "/admin/adjustment-requests" },
  { name: "Settlements", to: "/admin/settlements" },
  { name: "Vacation Types", to: "/admin/vacation-types" },
  { name: "Employees", to: "/admin/employees" },
  { name: "Payroll Export", to: "/admin/export" },
  { name: "App Settings", to: "/admin/settings" },
];

const navigationItems = computed(() => (isAdmin.value ? adminPersonalNav : employeeNav));

const adminSectionOpen = ref(route.path.startsWith("/admin/") && route.path !== "/admin/dashboard");

const handleNavClick = () => {
  if (window.innerWidth < 1024) emit("toggle");
};

const CHANGELOG_KEY = "changelog-last-seen";
const changelogOpen = ref(false);
const hasUnread = ref(false);
const changelogEntries = ref<ChangelogEntry[]>([]);

function parseChangelog(md: string): ChangelogEntry[] {
  const entries: ChangelogEntry[] = [];
  const sections = md.split(/\n(?=## \[)/);
  for (const section of sections) {
    const vMatch = section.match(/^## \[(v[\d.]+)\] - (\d{4}-\d{2}-\d{2})/);
    if (!vMatch) continue;
    const [, version, date] = vMatch;
    const categories: ChangelogCategory[] = [];
    const catBlocks = section.split(/\n(?=### )/);
    for (const block of catBlocks.slice(1)) {
      const catMatch = block.match(/^### (.+)/);
      if (!catMatch) continue;
      const items = block
        .split("\n")
        .filter((l) => l.startsWith("- "))
        .map((l) => l.slice(2));
      if (items.length) categories.push({ name: catMatch[1], items });
    }
    entries.push({ version, date, categories });
  }
  return entries;
}

function openChangelog() {
  changelogOpen.value = true;
  if (changelogEntries.value.length) {
    localStorage.setItem(CHANGELOG_KEY, changelogEntries.value[0].version);
    hasUnread.value = false;
  }
}

onMounted(async () => {
  fetchUser();
  try {
    const res = await fetch("/CHANGELOG.md");
    const text = await res.text();
    changelogEntries.value = parseChangelog(text);
    if (changelogEntries.value.length) {
      hasUnread.value =
        localStorage.getItem(CHANGELOG_KEY) !== changelogEntries.value[0].version;
    }
  } catch {
    /* non-critical, silently ignore */
  }
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
            <span>Manage</span>
            <ChevronDownIcon
              :class="[
                'size-3 transition-transform duration-200',
                adminSectionOpen && 'rotate-180',
              ]"
            />
          </button>
        </div>

        <ul v-show="adminSectionOpen" class="space-y-1">
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
</style>
