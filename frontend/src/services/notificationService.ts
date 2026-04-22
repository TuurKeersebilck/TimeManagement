import apiClient from "./api";

export type NotificationType = "Vacation" | "AdjustmentRequest";

export interface Notification {
  id: number;
  message: string;
  type: NotificationType;
  isRead: boolean;
  createdAt: string;
}

export const notificationService = {
  async getNotifications(): Promise<Notification[]> {
    const res = await apiClient.get<Notification[]>("/notifications");
    return res.data;
  },

  async getUnreadCount(): Promise<number> {
    const res = await apiClient.get<{ count: number }>("/notifications/unread-count");
    return res.data.count;
  },

  async markAsRead(id: number): Promise<void> {
    await apiClient.put(`/notifications/${id}/read`);
  },

  async markAllAsRead(): Promise<void> {
    await apiClient.put("/notifications/read-all");
  },
};
