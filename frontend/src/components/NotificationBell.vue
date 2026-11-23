<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue";
import { useRouter } from "vue-router";
import { useMediaQuery } from "@vueuse/core";
import { type Notification, type NotificationType } from "@/services/notificationService";
import { useNotifications } from "@/composables/useNotifications";
import {
  TONE_CLASSES,
  formatFullTime,
  formatRelativeTime,
  groupNotifications,
  notificationMeta,
} from "@/utils/notificationDisplay";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { BellIcon, BellOffIcon, CheckCheckIcon, CheckIcon, XIcon } from "lucide-vue-next";

const props = withDefaults(
  defineProps<{
    /** Where the panel opens: beside the sidebar, or below the mobile top bar. */
    side?: "right" | "bottom";
    align?: "start" | "end";
  }>(),
  { side: "right", align: "start" }
);

// Below the sidebar breakpoint the panel always opens downward: opening sideways from the
// sidebar would run off a phone screen, and only a downward panel can shift left to fit.
const isWide = useMediaQuery("(min-width: 1024px)");
const side = computed(() => (isWide.value ? props.side : "bottom"));
const align = computed(() => (isWide.value ? props.align : "end"));

const router = useRouter();
const open = ref(false);
const filter = ref<"all" | "unread">("all");
const {
  notifications,
  unreadCount,
  loading,
  fetchNotifications,
  markAsRead,
  markAllAsRead,
  remove,
  startLiveUpdates,
} = useNotifications();

const routeForType: Record<NotificationType, { name: string; query?: Record<string, string> }> = {
  Vacation: { name: "team-calendar" },
  AdjustmentRequest: { name: "admin-adjustment-requests" },
  AdjustmentApproved: { name: "time-tracking" },
  AdjustmentRejected: { name: "time-tracking" },
  SessionInvalidated: { name: "time-tracking" },
  MonthlySettlement: { name: "time-tracking" },
  AdminHoursAdjusted: { name: "time-tracking" },
  VacationRequest: { name: "admin-vacation-requests" },
  VacationApproved: { name: "vacations" },
  VacationRejected: { name: "vacations" },
};

// "Now" for the relative times, refreshed whenever the panel opens.
const now = ref(new Date());

const visible = computed(() =>
  filter.value === "unread" ? notifications.value.filter((n) => !n.isRead) : notifications.value
);
const groups = computed(() => groupNotifications(visible.value, now.value));
const badge = computed(() => (unreadCount.value > 99 ? "99+" : String(unreadCount.value)));

const onOpenChange = async (isOpen: boolean) => {
  open.value = isOpen;
  if (!isOpen) return;
  now.value = new Date();
  // Always reload: cheap, and the list may have changed since it was last opened.
  await fetchNotifications().catch(() => {});
};

const openNotification = async (n: Notification) => {
  await markAsRead(n);
  open.value = false;
  const target = routeForType[n.type];
  if (target) router.push(target);
};

let stopLiveUpdates: (() => void) | null = null;
onMounted(() => (stopLiveUpdates = startLiveUpdates()));
onUnmounted(() => stopLiveUpdates?.());
</script>

<template>
  <Popover :open="open" @update:open="onOpenChange">
    <PopoverTrigger as-child>
      <button
        class="relative btn-ghost !px-2 !py-2 shrink-0"
        :aria-label="unreadCount > 0 ? `Notifications, ${unreadCount} unread` : 'Notifications'"
        title="Notifications"
      >
        <BellIcon class="size-5" />
        <span
          v-if="unreadCount > 0"
          class="absolute -top-0.5 -right-0.5 min-w-4 h-4 px-1 rounded-full bg-primary text-primary-foreground text-[10px] font-bold flex items-center justify-center leading-none ring-2 ring-background"
        >
          {{ badge }}
        </span>
      </button>
    </PopoverTrigger>

    <PopoverContent
      :side="side"
      :align="align"
      :side-offset="8"
      :collision-padding="12"
      class="w-96 max-w-[calc(100vw-1.5rem)] p-0 overflow-hidden rounded-xl shadow-xl"
    >
      <!-- Header -->
      <div class="px-4 pt-3.5 pb-3 border-b border-border">
        <div class="flex items-center justify-between gap-3">
          <h2 class="text-sm font-semibold text-foreground">Notifications</h2>
          <button
            v-if="unreadCount > 0"
            class="inline-flex items-center gap-1 text-xs font-medium text-muted-foreground hover:text-foreground transition-colors cursor-pointer rounded-sm outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
            @click="markAllAsRead"
          >
            <CheckCheckIcon class="size-3.5" />
            Mark all as read
          </button>
        </div>

        <!-- Filter -->
        <div class="mt-3 inline-flex rounded-lg bg-muted p-0.5 text-xs font-medium" role="tablist">
          <button
            v-for="option in ['all', 'unread'] as const"
            :key="option"
            role="tab"
            :aria-selected="filter === option"
            :class="[
              'px-3 py-1 rounded-md transition-colors cursor-pointer inline-flex items-center gap-1.5 outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50',
              filter === option
                ? 'bg-background text-foreground shadow-sm'
                : 'text-muted-foreground hover:text-foreground',
            ]"
            @click="filter = option"
          >
            {{ option === "all" ? "All" : "Unread" }}
            <span
              v-if="option === 'unread' && unreadCount > 0"
              class="min-w-4 h-4 px-1 rounded-full bg-primary/15 text-primary text-[10px] leading-4 text-center"
              >{{ badge }}</span
            >
          </button>
        </div>
      </div>

      <!-- Body -->
      <div class="max-h-[min(28rem,65svh)] overflow-y-auto overscroll-contain">
        <!-- First load -->
        <div v-if="loading && notifications.length === 0" class="p-4 space-y-4" aria-busy="true">
          <div v-for="i in 3" :key="i" class="flex gap-3">
            <div class="size-9 rounded-lg bg-muted animate-pulse shrink-0" />
            <div class="flex-1 space-y-2 pt-1">
              <div class="h-3 w-1/2 rounded bg-muted animate-pulse" />
              <div class="h-3 w-4/5 rounded bg-muted animate-pulse" />
            </div>
          </div>
        </div>

        <!-- Empty -->
        <div
          v-else-if="groups.length === 0"
          class="flex flex-col items-center justify-center text-center px-6 py-10"
        >
          <div
            class="size-11 rounded-full flex items-center justify-center mb-3"
            :class="filter === 'unread' ? TONE_CLASSES.success : TONE_CLASSES.neutral"
          >
            <CheckIcon v-if="filter === 'unread'" class="size-5" />
            <BellOffIcon v-else class="size-5" />
          </div>
          <p class="text-sm font-medium text-foreground">
            {{ filter === "unread" ? "You're all caught up" : "No notifications yet" }}
          </p>
          <p class="text-xs text-muted-foreground mt-1">
            {{
              filter === "unread"
                ? "Nothing new since you last looked."
                : "Updates about your hours and vacation will show up here."
            }}
          </p>
        </div>

        <!-- Groups -->
        <template v-else>
          <section v-for="group in groups" :key="group.label" class="pb-1">
            <h3
              class="sticky top-0 z-10 bg-popover/95 backdrop-blur px-4 pt-3 pb-1.5 text-[11px] font-semibold uppercase tracking-wider text-muted-foreground"
            >
              {{ group.label }}
            </h3>
            <ul>
              <li
                v-for="n in group.items"
                :key="n.id"
                class="group relative px-4 py-2.5 hover:bg-muted/60 focus-within:bg-muted/60 transition-colors"
              >
                <!-- Main: opens the related page -->
                <button
                  class="flex w-full min-w-0 items-start gap-3 text-left cursor-pointer focus:outline-none"
                  @click="openNotification(n)"
                >
                  <span
                    class="size-9 rounded-lg flex items-center justify-center shrink-0"
                    :class="TONE_CLASSES[notificationMeta(n.type).tone]"
                  >
                    <component :is="notificationMeta(n.type).icon" class="size-4.5" />
                  </span>
                  <span class="flex-1 min-w-0 pr-14">
                    <span class="flex items-center gap-1.5">
                      <span
                        v-if="!n.isRead"
                        class="size-1.5 rounded-full bg-primary shrink-0"
                        aria-label="Unread"
                      />
                      <span
                        :class="[
                          'text-sm truncate',
                          n.isRead
                            ? 'font-medium text-foreground/80'
                            : 'font-semibold text-foreground',
                        ]"
                        >{{ notificationMeta(n.type).title }}</span
                      >
                    </span>
                    <span
                      :class="[
                        'mt-0.5 block text-[13px] leading-snug line-clamp-2',
                        n.isRead ? 'text-muted-foreground' : 'text-foreground/80',
                      ]"
                      >{{ n.message }}</span
                    >
                    <time
                      class="mt-1 block text-[11px] text-muted-foreground tabular-nums"
                      :datetime="n.createdAt"
                      :title="formatFullTime(n.createdAt)"
                      >{{ formatRelativeTime(n.createdAt, now) }}</time
                    >
                  </span>
                </button>

                <!-- Actions: on hover or keyboard focus; always shown on touchscreens -->
                <div
                  class="absolute right-2.5 top-2 flex items-center gap-0.5 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 pointer-coarse:opacity-100 transition-opacity"
                >
                  <button
                    v-if="!n.isRead"
                    class="size-7 rounded-md flex items-center justify-center text-muted-foreground hover:text-foreground hover:bg-background cursor-pointer outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
                    title="Mark as read"
                    aria-label="Mark as read"
                    @click="markAsRead(n)"
                  >
                    <CheckIcon class="size-3.5" />
                  </button>
                  <button
                    class="size-7 rounded-md flex items-center justify-center text-muted-foreground hover:text-destructive hover:bg-background cursor-pointer outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
                    title="Remove"
                    aria-label="Remove"
                    @click="remove(n)"
                  >
                    <XIcon class="size-3.5" />
                  </button>
                </div>
              </li>
            </ul>
          </section>
        </template>
      </div>
    </PopoverContent>
  </Popover>
</template>
