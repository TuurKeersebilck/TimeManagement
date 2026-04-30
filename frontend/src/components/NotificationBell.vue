<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRouter } from "vue-router";
import { type NotificationType } from "@/services/notificationService";
import { useNotifications } from "@/composables/useNotifications";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { BellIcon } from "lucide-vue-next";

const router = useRouter();
const open = ref(false);
const { notifications, unreadCount, loading, fetchUnreadCount, fetchNotifications, markAsRead, markAllAsRead } = useNotifications();

const routeForType: Record<NotificationType, string> = {
  Vacation: "team-calendar",
  AdjustmentRequest: "admin-adjustment-requests",
  AdjustmentApproved: "time-tracking",
  AdjustmentRejected: "time-tracking",
};

const onOpen = async (isOpen: boolean) => {
  open.value = isOpen;
  if (isOpen && notifications.value.length === 0) {
    await fetchNotifications();
  }
};

const handleMarkAsRead = async (n: Parameters<typeof markAsRead>[0]) => {
  await markAsRead(n);
  open.value = false;
  router.push({ name: routeForType[n.type] });
};

const formatDate = (iso: string) =>
  new Date(iso).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric" });

onMounted(fetchUnreadCount);
</script>

<template>
  <Popover @update:open="onOpen">
    <PopoverTrigger as-child>
      <button class="relative btn-ghost !px-2 !py-2 shrink-0" title="Notifications">
        <BellIcon class="size-5" />
        <span
          v-if="unreadCount > 0"
          class="absolute -top-0.5 -right-0.5 min-w-[16px] h-4 px-1 rounded-full bg-indigo-600 text-white text-[10px] font-bold flex items-center justify-center leading-none"
        >
          {{ unreadCount > 99 ? "99+" : unreadCount }}
        </span>
      </button>
    </PopoverTrigger>

    <PopoverContent align="start" side="right" class="w-80 p-0 overflow-hidden">
      <!-- Header -->
      <div class="flex items-center justify-between px-4 py-3 border-b border-slate-200 dark:border-slate-700">
        <span class="text-sm font-semibold text-slate-900 dark:text-slate-100">Notifications</span>
        <button
          v-if="unreadCount > 0"
          class="text-xs text-indigo-600 dark:text-indigo-400 hover:underline"
          @click="markAllAsRead"
        >
          Mark all as read
        </button>
      </div>

      <!-- Loading -->
      <div v-if="loading" class="px-4 py-6 text-center text-sm text-slate-400">
        Loading…
      </div>

      <!-- Empty -->
      <div v-else-if="notifications.length === 0" class="px-4 py-6 text-center text-sm text-slate-400 dark:text-slate-500">
        No notifications
      </div>

      <!-- List -->
      <ul v-else class="max-h-80 overflow-y-auto divide-y divide-slate-100 dark:divide-slate-800">
        <li
          v-for="n in notifications"
          :key="n.id"
          :class="[
            'flex items-start gap-3 px-4 py-3 cursor-pointer transition-colors',
            n.isRead
              ? 'bg-white dark:bg-slate-900'
              : 'bg-indigo-50/60 dark:bg-indigo-950/20 hover:bg-indigo-50 dark:hover:bg-indigo-950/40',
          ]"
          @click="handleMarkAsRead(n)"
        >
          <span
            :class="[
              'mt-1.5 size-2 rounded-full shrink-0',
              n.isRead ? 'bg-transparent' : 'bg-indigo-500',
            ]"
          />
          <div class="flex-1 min-w-0">
            <p class="text-sm text-slate-800 dark:text-slate-200 leading-snug">{{ n.message }}</p>
            <p class="text-xs text-slate-400 dark:text-slate-500 mt-0.5">{{ formatDate(n.createdAt) }}</p>
          </div>
        </li>
      </ul>
    </PopoverContent>
  </Popover>
</template>
