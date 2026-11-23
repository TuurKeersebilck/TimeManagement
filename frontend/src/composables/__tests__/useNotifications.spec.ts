import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const notificationService = vi.hoisted(() => ({
  getNotifications: vi.fn(),
  getUnreadCount: vi.fn(),
  markAsRead: vi.fn(),
  markAllAsRead: vi.fn(),
  remove: vi.fn(),
}));
vi.mock("@/services/notificationService", () => ({ notificationService }));

type Module = typeof import("../useNotifications");

const LIST = () => [
  { id: 1, message: "one", type: "Vacation", isRead: false, createdAt: "2026-03-02T09:00:00Z" },
  { id: 2, message: "two", type: "Vacation", isRead: true, createdAt: "2026-03-01T09:00:00Z" },
];

const setVisibility = (state: "visible" | "hidden") => {
  Object.defineProperty(document, "visibilityState", { value: state, configurable: true });
  document.dispatchEvent(new Event("visibilitychange"));
};

describe("useNotifications", () => {
  let useNotifications: Module["useNotifications"];
  let POLL_INTERVAL_MS: Module["POLL_INTERVAL_MS"];

  beforeEach(async () => {
    vi.resetModules();
    setVisibility("visible");
    notificationService.getUnreadCount.mockReset().mockResolvedValue(3);
    notificationService.getNotifications.mockReset().mockResolvedValue(LIST());
    notificationService.markAsRead.mockReset().mockResolvedValue(undefined);
    notificationService.markAllAsRead.mockReset().mockResolvedValue(undefined);
    notificationService.remove.mockReset().mockResolvedValue(undefined);
    ({ useNotifications, POLL_INTERVAL_MS } = await import("../useNotifications"));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // ── Unread count ──────────────────────────────────────────────────────────

  it("asks the server again on every refresh, so new ones show up", async () => {
    const { refreshUnreadCount, unreadCount } = useNotifications();

    await refreshUnreadCount();
    notificationService.getUnreadCount.mockResolvedValue(5);
    await refreshUnreadCount();

    expect(unreadCount.value).toBe(5);
    expect(notificationService.getUnreadCount).toHaveBeenCalledTimes(2);
  });

  it("collapses concurrent refreshes into a single request", async () => {
    // The bell renders in the layout and in the sidebar; both mount at once.
    const { refreshUnreadCount } = useNotifications();

    await Promise.all([refreshUnreadCount(), refreshUnreadCount(), refreshUnreadCount()]);

    expect(notificationService.getUnreadCount).toHaveBeenCalledOnce();
  });

  it("reloads a list that was already shown when something new arrives", async () => {
    const { fetchNotifications, refreshUnreadCount } = useNotifications();
    await fetchNotifications(); // 1 unread
    notificationService.getUnreadCount.mockResolvedValue(2);

    await refreshUnreadCount();

    expect(notificationService.getNotifications).toHaveBeenCalledTimes(2);
  });

  it("doesn't load the list just to update the badge", async () => {
    await useNotifications().refreshUnreadCount();

    expect(notificationService.getNotifications).not.toHaveBeenCalled();
  });

  // ── List and actions ──────────────────────────────────────────────────────

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

    expect(unreadCount.value).toBe(1);
    expect(notificationService.markAsRead).not.toHaveBeenCalled();
  });

  it("never drives the badge below zero", async () => {
    const { unreadCount, markAsRead } = useNotifications();
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

  it("removes one straight away and lowers the badge if it was unread", async () => {
    const { fetchNotifications, notifications, unreadCount, remove } = useNotifications();
    await fetchNotifications();

    await remove(notifications.value[0]); // unread
    await remove(notifications.value[0]); // the read one, now first

    expect(notifications.value).toEqual([]);
    expect(unreadCount.value).toBe(0);
    expect(notificationService.remove).toHaveBeenNthCalledWith(1, 1);
    expect(notificationService.remove).toHaveBeenNthCalledWith(2, 2);
  });

  it("reloads the list when the server refuses a removal", async () => {
    notificationService.remove.mockRejectedValue(new Error("offline"));
    const { fetchNotifications, notifications, remove } = useNotifications();
    await fetchNotifications();

    await remove(notifications.value[0]);

    expect(notifications.value).toHaveLength(2);
  });

  // ── Live updates ──────────────────────────────────────────────────────────

  it("checks right away and then every minute", async () => {
    vi.useFakeTimers();
    const stop = useNotifications().startLiveUpdates();
    await vi.advanceTimersByTimeAsync(0);
    expect(notificationService.getUnreadCount).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);
    expect(notificationService.getUnreadCount).toHaveBeenCalledTimes(2);

    stop();
    await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS * 3);
    expect(notificationService.getUnreadCount).toHaveBeenCalledTimes(2);
  });

  it("shares one poller between bells and stops with the last one", async () => {
    vi.useFakeTimers();
    const { startLiveUpdates } = useNotifications();
    const stopSidebar = startLiveUpdates();
    const stopHeader = startLiveUpdates();
    await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);
    expect(notificationService.getUnreadCount).toHaveBeenCalledTimes(2); // not 4

    stopSidebar();
    stopSidebar(); // stopping twice must not stop the other bell's updates
    await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);
    expect(notificationService.getUnreadCount).toHaveBeenCalledTimes(3);

    stopHeader();
    await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS * 2);
    expect(notificationService.getUnreadCount).toHaveBeenCalledTimes(3);
  });

  it("skips checks while the tab is hidden and catches up when it's back", async () => {
    vi.useFakeTimers();
    const stop = useNotifications().startLiveUpdates();
    await vi.advanceTimersByTimeAsync(0);

    setVisibility("hidden");
    await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS * 3);
    expect(notificationService.getUnreadCount).toHaveBeenCalledTimes(1);

    setVisibility("visible");
    await vi.advanceTimersByTimeAsync(0);
    expect(notificationService.getUnreadCount).toHaveBeenCalledTimes(2);
    stop();
  });

  it("keeps polling after a failed check", async () => {
    vi.useFakeTimers();
    notificationService.getUnreadCount.mockRejectedValueOnce(new Error("offline"));
    const { startLiveUpdates, unreadCount } = useNotifications();
    const stop = startLiveUpdates();
    await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);

    expect(unreadCount.value).toBe(3);
    stop();
  });

  it("reset clears the cache so the next user starts clean", async () => {
    // Module state outlives a logout; without this the next account sees the previous
    // account's notifications.
    const { fetchNotifications, notifications, unreadCount, reset, refreshUnreadCount } =
      useNotifications();
    await fetchNotifications();

    reset();

    expect(notifications.value).toEqual([]);
    expect(unreadCount.value).toBe(0);
    await refreshUnreadCount();
    expect(notificationService.getNotifications).toHaveBeenCalledOnce(); // list not reloaded for the next user
  });
});
