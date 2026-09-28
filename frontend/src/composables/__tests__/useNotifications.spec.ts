import { beforeEach, describe, expect, it, vi } from "vitest";

const notificationService = vi.hoisted(() => ({
  getNotifications: vi.fn(),
  getUnreadCount: vi.fn(),
  markAsRead: vi.fn(),
  markAllAsRead: vi.fn(),
}));
vi.mock("@/services/notificationService", () => ({ notificationService }));

type Module = typeof import("../useNotifications");

describe("useNotifications", () => {
  let useNotifications: Module["useNotifications"];

  beforeEach(async () => {
    vi.resetModules();
    notificationService.getUnreadCount.mockReset().mockResolvedValue(3);
    notificationService.getNotifications.mockReset().mockResolvedValue([
      { id: 1, message: "one", type: "Vacation", isRead: false, createdAt: "2026-03-02T09:00:00Z" },
      { id: 2, message: "two", type: "Vacation", isRead: true, createdAt: "2026-03-01T09:00:00Z" },
    ]);
    notificationService.markAsRead.mockReset().mockResolvedValue(undefined);
    notificationService.markAllAsRead.mockReset().mockResolvedValue(undefined);
    ({ useNotifications } = await import("../useNotifications"));
  });

  it("loads the unread count once", async () => {
    const { fetchUnreadCount, unreadCount } = useNotifications();

    await fetchUnreadCount();
    await fetchUnreadCount();

    expect(unreadCount.value).toBe(3);
    expect(notificationService.getUnreadCount).toHaveBeenCalledOnce();
  });

  it("collapses concurrent first loads into a single request", async () => {
    // The bell renders in the layout and in the sidebar; both mount at once.
    const { fetchUnreadCount } = useNotifications();

    await Promise.all([fetchUnreadCount(), fetchUnreadCount(), fetchUnreadCount()]);

    expect(notificationService.getUnreadCount).toHaveBeenCalledOnce();
  });

  it("derives the unread count from the fetched list", async () => {
    const { fetchNotifications, notifications, unreadCount } = useNotifications();

    await fetchNotifications();

    expect(notifications.value).toHaveLength(2);
    expect(unreadCount.value).toBe(1);
  });

  it("clears the loading flag even when the fetch fails", async () => {
    notificationService.getNotifications.mockRejectedValue(new Error("offline"));
    const { fetchNotifications, loading } = useNotifications();

    await expect(fetchNotifications()).rejects.toThrow("offline");

    expect(loading.value).toBe(false);
  });

  it("marks one as read optimistically and tells the server", async () => {
    const { fetchNotifications, notifications, unreadCount, markAsRead } = useNotifications();
    await fetchNotifications();

    await markAsRead(notifications.value[0]);

    expect(notifications.value[0].isRead).toBe(true);
    expect(unreadCount.value).toBe(0);
    expect(notificationService.markAsRead).toHaveBeenCalledWith(1);
  });

  it("does nothing when the notification is already read", async () => {
    const { fetchNotifications, notifications, unreadCount, markAsRead } = useNotifications();
    await fetchNotifications();

    await markAsRead(notifications.value[1]);

    expect(unreadCount.value).toBe(1); // unchanged
    expect(notificationService.markAsRead).not.toHaveBeenCalled();
  });

  it("never drives the badge below zero", async () => {
    const { fetchUnreadCount, unreadCount, markAsRead } = useNotifications();
    await fetchUnreadCount();
    unreadCount.value = 0;

    await markAsRead({ id: 5, message: "x", type: "Vacation", isRead: false, createdAt: "" });

    expect(unreadCount.value).toBe(0);
  });

  it("marks everything read in one go", async () => {
    const { fetchNotifications, notifications, unreadCount, markAllAsRead } = useNotifications();
    await fetchNotifications();

    await markAllAsRead();

    expect(notifications.value.every((n) => n.isRead)).toBe(true);
    expect(unreadCount.value).toBe(0);
    expect(notificationService.markAllAsRead).toHaveBeenCalledOnce();
  });

  it("reset clears the cache so the next user starts clean", async () => {
    // Module state outlives a logout; without this the next account sees the previous
    // account's notifications.
    const { fetchNotifications, notifications, unreadCount, reset, fetchUnreadCount } =
      useNotifications();
    await fetchNotifications();

    reset();

    expect(notifications.value).toEqual([]);
    expect(unreadCount.value).toBe(0);
    await fetchUnreadCount();
    expect(notificationService.getUnreadCount).toHaveBeenCalledOnce();
  });
});
