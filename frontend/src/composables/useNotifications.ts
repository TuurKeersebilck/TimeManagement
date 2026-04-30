import { ref } from "vue";
import { notificationService, type Notification } from "@/services/notificationService";

// Module-level state — shared across all component instances
const notifications = ref<Notification[]>([]);
const unreadCount = ref(0);
const loading = ref(false);
const initialized = ref(false);
let inflightUnread: Promise<void> | null = null;

export function useNotifications() {
  const fetchUnreadCount = async () => {
    if (initialized.value) return;
    if (inflightUnread) return inflightUnread;
    inflightUnread = (async () => {
      unreadCount.value = await notificationService.getUnreadCount();
      initialized.value = true;
    })();
    await inflightUnread;
    inflightUnread = null;
  };

  const fetchNotifications = async () => {
    loading.value = true;
    try {
      notifications.value = await notificationService.getNotifications();
      unreadCount.value = notifications.value.filter((n) => !n.isRead).length;
      initialized.value = true;
    } finally {
      loading.value = false;
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

  const reset = () => {
    notifications.value = [];
    unreadCount.value = 0;
    initialized.value = false;
    inflightUnread = null;
  };

  return {
    notifications,
    unreadCount,
    loading,
    fetchUnreadCount,
    fetchNotifications,
    markAsRead,
    markAllAsRead,
    reset,
  };
}
