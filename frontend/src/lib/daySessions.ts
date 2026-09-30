import type { DesiredDaySnapshot } from "@/services/adjustmentRequestService";

/**
 * Editable form model for one day's sessions, shared by the employee's adjustment request and
 * the admin's direct edit. Times are local "HH:MM" strings; ids point at existing records so the
 * backend updates them in place instead of replacing them.
 */
export interface EditableBreak {
  breakRecordId?: number;
  breakStart: string;
  breakEnd: string;
}

export interface EditableSession {
  workSessionId?: number;
  clockIn: string;
  clockOut: string;
  breaks: EditableBreak[];
  /** Display-only hint next to the session title, e.g. "prefilled" or "auto-closed". */
  note?: string;
}

export function emptySession(): EditableSession {
  return { clockIn: "", clockOut: "", breaks: [] };
}

/** An ISO timestamp as a local "HH:MM" time for a time input. */
export function toTimeString(iso: string | null | undefined): string {
  if (!iso) return "";
  const d = new Date(iso);
  return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
}

/** A local date and "HH:MM" time as an ISO string with the browser's UTC offset. */
export function toLocalIso(date: string, time: string): string {
  const dt = new Date(`${date}T${time}:00`);
  const offsetMin = -dt.getTimezoneOffset();
  const sign = offsetMin >= 0 ? "+" : "-";
  const abs = Math.abs(offsetMin);
  const oh = String(Math.floor(abs / 60)).padStart(2, "0");
  const om = String(abs % 60).padStart(2, "0");
  const [y, mo, d, h, m] = [
    dt.getFullYear(),
    String(dt.getMonth() + 1).padStart(2, "0"),
    String(dt.getDate()).padStart(2, "0"),
    String(dt.getHours()).padStart(2, "0"),
    String(dt.getMinutes()).padStart(2, "0"),
  ];
  return `${y}-${mo}-${d}T${h}:${m}:00${sign}${oh}:${om}`;
}

/**
 * Client-side checks mirroring the backend's snapshot validation, so mistakes are caught before
 * sending. Returns the first problem as a message, or null when the day is valid.
 */
export function validateDaySessions(
  sessions: EditableSession[],
  { allowEmpty = false }: { allowEmpty?: boolean } = {}
): string | null {
  if (sessions.length === 0) return allowEmpty ? null : "Add at least one session";

  for (const s of sessions) {
    if (!s.clockIn || !s.clockOut) return "All sessions must have clock-in and clock-out times";
    if (s.clockIn >= s.clockOut) return "Clock-out must be after clock-in for all sessions";
    for (const b of s.breaks) {
      if (!b.breakStart || !b.breakEnd) return "All breaks must have start and end times";
      if (b.breakStart >= b.breakEnd) return "Break end must be after break start";
      if (b.breakStart < s.clockIn || b.breakEnd > s.clockOut)
        return "Breaks must fall within the session's clock-in and clock-out times";
    }
  }

  const sorted = [...sessions].sort((a, b) => a.clockIn.localeCompare(b.clockIn));
  for (let i = 0; i < sorted.length - 1; i++) {
    if (sorted[i].clockOut > sorted[i + 1].clockIn) return "Sessions must not overlap";
  }

  return null;
}

/** The form model as the snapshot the backend expects for the given local date. */
export function toSnapshot(date: string, sessions: EditableSession[]): DesiredDaySnapshot {
  return {
    sessions: sessions.map((s) => ({
      ...(s.workSessionId !== undefined && { workSessionId: s.workSessionId }),
      clockIn: toLocalIso(date, s.clockIn),
      clockOut: toLocalIso(date, s.clockOut),
      breaks: s.breaks
        .filter((b) => b.breakStart && b.breakEnd)
        .map((b) => ({
          ...(b.breakRecordId !== undefined && { breakRecordId: b.breakRecordId }),
          breakStart: toLocalIso(date, b.breakStart),
          breakEnd: toLocalIso(date, b.breakEnd),
        })),
    })),
  };
}
