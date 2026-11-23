import { ref } from "vue";
import { notificationService, type Notification } from "@/services/notificationService";

/** How often the bell checks for new notifications while the tab is visible. */
export const POLL_INTERVAL_MS = 60_000;

// Module-level state — shared across all component instances (the bell renders in the
// sidebar and in the mobile header at the same time).
const notifications = ref<Notification[]>([]);
const unreadCount = ref(0);
const loading = ref(false);
const listLoaded = ref(false);
let inflightUnread: Promise<void> | null = null;

// One shared poller, however many bells are mounted.
let subscribers = 0;
let timer: ReturnType<typeof setInterval> | null = null;

export function useNotifications() {
  const fetchNotifications = async () => {
    loading.value = true;
    try {
      notifications.value = await notificationService.getNotifications();
      unreadCount.value = notifications.value.filter((n) => !n.isRead).length;
      listLoaded.value = true;
    } finally {
      loading.value = false;
    }
  };

  /** Fetches the unread count; calls made while one is in flight share it. */
  const refreshUnreadCount = async () => {
    if (inflightUnread) return inflightUnread;
    inflightUnread = (async () => {
      const previous = unreadCount.value;
      unreadCount.value = await notificationService.getUnreadCount();
      // Something new arrived: refresh the list too if it was already shown once.
      if (listLoaded.value && unreadCount.value > previous) await fetchNotifications();
    })();
    try {
      await inflightUnread;
    } finally {
      inflightUnread = null;
    }
  };

  const markAsRead = async (n: Notification) => {
    if (!n.isRead) {
      n.isRead = true;
      unreadCount.value = Math.max(0, unreadCount.value - 1);
      await notificationService.markAsRead(n.id);
    }
  };

  const markAllAsRead = async () => {
    notifications.value.forEach((n) => (n.isRead = true));
    unreadCount.value = 0;
    await notificationService.markAllAsRead();
  };

  /** Removes one from the list straight away; if the server refuses, the list is reloaded. */
  const remove = async (n: Notification) => {
    notifications.value = notifications.value.filter((x) => x.id !== n.id);
    if (!n.isRead) unreadCount.value = Math.max(0, unreadCount.value - 1);
    try {
      await notificationService.remove(n.id);
    } catch {
      await fetchNotifications().catch(() => {});
    }
  };

  const poll = () => {
    if (typeof document !== "undefined" && document.visibilityState === "hidden") return;
    refreshUnreadCount().catch(() => {
      // A missed check is retried on the next tick; the badge keeps its last value.
    });
  };

  const onVisibilityChange = () => {
    if (document.visibilityState === "visible") poll();
  };

  /**
   * Keeps the unread count (and a shown list) up to date: checks now, every minute while the
   * tab is visible, and as soon as the tab comes back. Returns a stop function; the shared
   * poller stops when the last bell unmounts.
   */
  const startLiveUpdates = () => {
    subscribers++;
    if (subscribers === 1) {
      poll();
      timer = setInterval(poll, POLL_INTERVAL_MS);
      document.addEventListener("visibilitychange", onVisibilityChange);
    }
    let stopped = false;
    return () => {
      if (stopped) return;
      stopped = true;
      subscribers = Math.max(0, subscribers - 1);
      if (subscribers === 0) {
        if (timer) clearInterval(timer);
        timer = null;
        document.removeEventListener("visibilitychange", onVisibilityChange);
      }
    };
  };

  const reset = () => {
    notifications.value = [];
    unreadCount.value = 0;
    listLoaded.value = false;
    inflightUnread = null;
  };

  return {
    notifications,
    unreadCount,
    loading,
    fetchNotifications,
    refreshUnreadCount,
    markAsRead,
    markAllAsRead,
    remove,
    startLiveUpdates,
    reset,
  };
}
