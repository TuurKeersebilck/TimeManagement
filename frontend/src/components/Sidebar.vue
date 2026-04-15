<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRoute } from "vue-router";
import { useAuth } from "../composables/useAuth";
import NotificationBell from "./NotificationBell.vue";
import { LogOutIcon, ChevronDownIcon } from "lucide-vue-next";

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
];

const adminPersonalNav: NavItem[] = [
  { name: "Dashboard", to: "/admin/dashboard" },
  { name: "My Vacations", to: "/vacations" },
  { name: "Team Calendar", to: "/team-calendar" },
  { name: "Account", to: "/account" },
];

const adminSectionNav: NavItem[] = [
  { name: "All Time Logs", to: "/admin/time-logs" },
  { name: "Adjustment Requests", to: "/admin/adjustment-requests" },
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
      <span class="sidebar-wordmark">Time<br>Management</span>
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
      <div v-if="isAdmin">
        <NotificationBell />
      </div>

      <!-- User profile -->
      <div v-if="isLoadingUser" class="h-4 w-3/4 bg-muted rounded animate-pulse" />
      <div v-else-if="currentUser" class="flex items-center justify-between gap-2">
        <div class="flex items-center gap-2 min-w-0">
          <div class="w-6 h-6 user-avatar shrink-0">
            <span class="text-[10px] font-bold text-white">{{ userInitials }}</span>
          </div>
          <span class="text-xs text-muted-foreground truncate">{{ currentUser.fullName }}</span>
        </div>
        <button
          @click="emit('logout')"
          class="text-muted-foreground hover:text-destructive transition-colors cursor-pointer shrink-0"
          title="Sign out"
        >
          <LogOutIcon class="size-3.5" />
        </button>
      </div>
    </div>
  </aside>
</template>

<style scoped>
.sidebar-wordmark {
  display: block;
  font-family: var(--font-data);
  font-size: 0.7rem;
  font-weight: 600;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  line-height: 1.5;
  color: var(--foreground);
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
