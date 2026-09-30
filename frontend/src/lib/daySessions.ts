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

  // Messages name the session and break by the number shown in the form, with the times
  // involved, so it's clear what to change.
  for (const [si, s] of sessions.entries()) {
    const session = `Session ${si + 1}`;
    if (!s.clockIn || !s.clockOut) return `${session}: fill in both clock-in and clock-out`;
    if (s.clockIn >= s.clockOut)
      return `${session}: clock-out (${s.clockOut}) must be after clock-in (${s.clockIn})`;

    for (const [bi, b] of s.breaks.entries()) {
      const where = `${session}, break ${bi + 1}`;
      if (!b.breakStart || !b.breakEnd) return `${where}: fill in both break start and break end`;
      if (b.breakStart >= b.breakEnd)
        return `${where}: break end (${b.breakEnd}) must be after break start (${b.breakStart})`;
      if (b.breakStart < s.clockIn)
        return `${where}: the break (${b.breakStart}–${b.breakEnd}) starts before clock-in (${s.clockIn})`;
      if (b.breakEnd > s.clockOut)
        return `${where}: the break (${b.breakStart}–${b.breakEnd}) ends after clock-out (${s.clockOut})`;
    }
  }

  const ordered = sessions
    .map((s, i) => ({ ...s, number: i + 1 }))
    .sort((a, b) => a.clockIn.localeCompare(b.clockIn));
  for (let i = 0; i < ordered.length - 1; i++) {
    const [a, b] = [ordered[i], ordered[i + 1]];
    if (a.clockOut > b.clockIn)
      return `Session ${a.number} (${a.clockIn}–${a.clockOut}) overlaps session ${b.number} (${b.clockIn}–${b.clockOut})`;
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
