import { describe, expect, it } from "vitest";
import type { Notification } from "@/services/notificationService";
import {
  TONE_CLASSES,
  formatRelativeTime,
  groupNotifications,
  notificationMeta,
} from "../notificationDisplay";

// Local times, so "today" and "yesterday" mean what they mean on the user's clock.
const NOW = new Date(2026, 10, 23, 15, 0); // Mon 23 Nov 2026, 15:00
const at = (y: number, m: number, d: number, h = 12, min = 0) =>
  new Date(y, m - 1, d, h, min).toISOString();

const n = (id: number, createdAt: string, over: Partial<Notification> = {}): Notification => ({
  id,
  message: `message ${id}`,
  type: "Vacation",
  isRead: false,
  createdAt,
  ...over,
});

describe("notificationMeta", () => {
  it("gives every known type a title, icon and tone", () => {
    expect(notificationMeta("VacationApproved")).toMatchObject({
      title: "Vacation approved",
      tone: "success",
    });
    expect(notificationMeta("VacationRejected")).toMatchObject({
      title: "Vacation denied",
      tone: "danger",
    });
    expect(notificationMeta("SessionInvalidated")).toMatchObject({
      title: "Session auto-closed",
      tone: "warning",
    });
    expect(notificationMeta("AdjustmentRequest").icon).toBeTruthy();
  });

  it("falls back to a plain bell for a type the frontend doesn't know yet", () => {
    expect(notificationMeta("SomethingNew")).toMatchObject({
      title: "Notification",
      tone: "neutral",
    });
  });

  it("has light and dark classes for every tone", () => {
    for (const classes of Object.values(TONE_CLASSES)) expect(classes).toMatch(/\S/);
  });
});

describe("formatRelativeTime", () => {
  it.each([
    ["a minute ago", at(2026, 11, 23, 14, 59), "1 min ago"],
    ["moments ago", new Date(NOW.getTime() - 20_000).toISOString(), "Just now"],
    ["earlier this hour", at(2026, 11, 23, 14, 35), "25 min ago"],
    ["earlier today", at(2026, 11, 23, 9, 10), "5 h ago"],
    ["yesterday late", at(2026, 11, 22, 23, 50), "Yesterday"],
  ])("%s", (_, iso, expected) => {
    expect(formatRelativeTime(iso, NOW)).toBe(expected);
  });

  it("uses the weekday within the last week", () => {
    const thursday = at(2026, 11, 19);
    expect(formatRelativeTime(thursday, NOW)).toBe(
      new Date(thursday).toLocaleDateString(undefined, { weekday: "short" })
    );
  });

  it("uses a date after a week, with the year only when it differs", () => {
    const sameYear = at(2026, 11, 2);
    const lastYear = at(2025, 12, 30);
    expect(formatRelativeTime(sameYear, NOW)).toBe(
      new Date(sameYear).toLocaleDateString(undefined, { day: "numeric", month: "short" })
    );
    expect(formatRelativeTime(lastYear, NOW)).toContain("2025");
  });
});

describe("groupNotifications", () => {
  it("splits into Today and Earlier, keeping the order", () => {
    const list = [
      n(3, at(2026, 11, 23, 14)),
      n(2, at(2026, 11, 23, 8)),
      n(1, at(2026, 11, 22, 23)),
    ];

    const groups = groupNotifications(list, NOW);

    expect(groups.map((g) => g.label)).toEqual(["Today", "Earlier"]);
    expect(groups[0].items.map((x) => x.id)).toEqual([3, 2]);
    expect(groups[1].items.map((x) => x.id)).toEqual([1]);
  });

  it("leaves out empty groups", () => {
    expect(groupNotifications([n(1, at(2026, 11, 1))], NOW).map((g) => g.label)).toEqual([
      "Earlier",
    ]);
    expect(groupNotifications([], NOW)).toEqual([]);
  });
});
