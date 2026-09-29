import { useIntervalFn, useNow } from "@vueuse/core";
import type { AdminSession, AdminTimeLog } from "@/services/adminService";

/**
 * The backend only counts closed sessions in totalHours, so an employee who is still
 * clocked in shows 0h. This adds the running time of open sessions (minus breaks)
 * client-side and re-renders every 30s while the component is mounted.
 */
export function useLiveHours() {
  const now = useNow({ scheduler: (cb) => useIntervalFn(cb, 30_000) });

  const openSessionHours = (session: AdminSession) => {
    const nowMs = now.value.getTime();
    const breakMs = session.breaks.reduce((sum, b) => {
      const end = b.breakEnd ? new Date(b.breakEnd).getTime() : nowMs;
      return sum + Math.max(0, end - new Date(b.breakStart).getTime());
    }, 0);
    const elapsedMs = nowMs - new Date(session.clockIn).getTime();
    return Math.max(0, elapsedMs - breakMs) / 3_600_000;
  };

  const liveHours = (log: AdminTimeLog) =>
    (log.totalHours ?? 0) +
    log.sessions.filter((s) => s.status === "Open").reduce((sum, s) => sum + openSessionHours(s), 0);

  const isOnBreak = (log: AdminTimeLog) =>
    log.sessions.some((s) => s.status === "Open" && s.breaks.some((b) => !b.breakEnd));

  return { liveHours, isOnBreak };
}
