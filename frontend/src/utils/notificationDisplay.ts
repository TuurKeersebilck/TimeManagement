import type { Component } from "vue";
import {
  BellIcon,
  CalendarCheckIcon,
  CalendarClockIcon,
  CalendarDaysIcon,
  CalendarXIcon,
  ClockAlertIcon,
  ClockIcon,
  CircleCheckIcon,
  CircleXIcon,
  WalletIcon,
} from "lucide-vue-next";
import type { Notification, NotificationType } from "@/services/notificationService";

/** Colour family of a notification's icon: green = good news, red = refused, amber = needs attention. */
export type NotificationTone = "success" | "danger" | "warning" | "info" | "neutral";

export interface NotificationMeta {
  title: string;
  icon: Component;
  tone: NotificationTone;
}

const META: Record<NotificationType, NotificationMeta> = {
  Vacation: { title: "Vacation updated", icon: CalendarDaysIcon, tone: "info" },
  VacationRequest: { title: "Vacation request", icon: CalendarClockIcon, tone: "warning" },
  VacationApproved: { title: "Vacation approved", icon: CalendarCheckIcon, tone: "success" },
  VacationRejected: { title: "Vacation denied", icon: CalendarXIcon, tone: "danger" },
  AdjustmentRequest: { title: "Time adjustment request", icon: ClockIcon, tone: "warning" },
  AdjustmentApproved: { title: "Time adjustment approved", icon: CircleCheckIcon, tone: "success" },
  AdjustmentRejected: { title: "Time adjustment rejected", icon: CircleXIcon, tone: "danger" },
  SessionInvalidated: { title: "Session auto-closed", icon: ClockAlertIcon, tone: "warning" },
  MonthlySettlement: { title: "Monthly settlement", icon: WalletIcon, tone: "info" },
  AdminHoursAdjusted: { title: "Hours adjusted", icon: ClockIcon, tone: "info" },
};

const FALLBACK: NotificationMeta = { title: "Notification", icon: BellIcon, tone: "neutral" };

/** Title, icon and tone for a notification type; unknown (newer) types get a plain bell. */
export function notificationMeta(type: NotificationType | string): NotificationMeta {
  return META[type as NotificationType] ?? FALLBACK;
}

/** Icon tile classes per tone, light and dark. */
export const TONE_CLASSES: Record<NotificationTone, string> = {
  success: "bg-emerald-50 text-emerald-600 dark:bg-emerald-950/50 dark:text-emerald-400",
  danger: "bg-red-50 text-red-600 dark:bg-red-950/50 dark:text-red-400",
  warning: "bg-amber-50 text-amber-600 dark:bg-amber-950/50 dark:text-amber-400",
  info: "bg-primary/10 text-primary",
  neutral: "bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-400",
};

const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate());
const DAY_MS = 86_400_000;

/** Whole calendar days between two moments in local time (0 = same day, 1 = yesterday, …). */
function daysBetween(earlier: Date, later: Date): number {
  return Math.round((startOfDay(later).getTime() - startOfDay(earlier).getTime()) / DAY_MS);
}

/**
 * Short, human time for the list: "Just now", "5 min ago", "2 h ago", "Yesterday", a weekday
 * within the last week ("Mon"), then a date ("12 Nov", or "12 Nov 2025" in another year).
 */
export function formatRelativeTime(iso: string, now: Date = new Date()): string {
  const date = new Date(iso);
  const minutes = Math.floor((now.getTime() - date.getTime()) / 60_000);
  const days = daysBetween(date, now);

  if (minutes < 1) return "Just now";
  if (days === 0 && minutes < 60) return `${minutes} min ago`;
  if (days === 0) return `${Math.floor(minutes / 60)} h ago`;
  if (days === 1) return "Yesterday";
  if (days < 7) return date.toLocaleDateString(undefined, { weekday: "short" });
  return date.toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
    ...(date.getFullYear() !== now.getFullYear() ? { year: "numeric" } : {}),
  });
}

/** Full date and time, for the tooltip on the short one. */
export function formatFullTime(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    weekday: "long",
    day: "numeric",
    month: "long",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export interface NotificationGroup {
  label: "Today" | "Earlier";
  items: Notification[];
}

/** Splits a newest-first list into "Today" and "Earlier", leaving out empty groups. */
export function groupNotifications(
  list: Notification[],
  now: Date = new Date()
): NotificationGroup[] {
  const today: Notification[] = [];
  const earlier: Notification[] = [];
  for (const n of list) (daysBetween(new Date(n.createdAt), now) === 0 ? today : earlier).push(n);

  const groups: NotificationGroup[] = [];
  if (today.length) groups.push({ label: "Today", items: today });
  if (earlier.length) groups.push({ label: "Earlier", items: earlier });
  return groups;
}
