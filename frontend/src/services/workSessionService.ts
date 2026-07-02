import api from "./api";

// ── Enums ──────────────────────────────────────────────────────────────────────

export type WorkSessionStatus = "Open" | "Closed" | "Invalidated";

// ── Response DTOs ──────────────────────────────────────────────────────────────

export interface BreakRecordDto {
  id: number;
  breakStart: string; // UTC ISO
  breakStartServerStamp: string;
  breakEnd: string | null;
  breakEndServerStamp: string | null;
}

export interface WorkSessionDto {
  id: number;
  date: string; // "yyyy-MM-dd"
  clockIn: string; // UTC ISO
  clockInServerStamp: string;
  clockOut: string | null;
  clockOutServerStamp: string | null;
  status: WorkSessionStatus;
  breaks: BreakRecordDto[];
}

export interface WorkDayDto {
  date: string;
  workedFromHome: boolean;
  description: string | null;
}

export interface TodayStatusDto {
  openSession: WorkSessionDto | null;
  closedSessions: WorkSessionDto[];
  workDay: WorkDayDto | null;
}

export interface TodayLiveDto {
  sessionId: number;
  clockIn: string; // UTC ISO
  elapsedMinutes: number;
  isOnBreak: boolean;
  breakStartedAt: string | null;
}

export interface WorkDaySummaryDto {
  date: string; // "yyyy-MM-dd"
  totalWorkedHours: number;
  hasOpenSession: boolean;
  workDay: WorkDayDto | null;
  sessions: WorkSessionDto[];
  vacationAmount: number | null;
  vacationTypeName: string | null;
}

export interface WorkdayTargetEntry {
  dayOfWeek: number; // 0=Sun…6=Sat
  hours: number;
}

export interface WorkScheduleDto {
  workdayTargets: WorkdayTargetEntry[];
  minimumBreakMinutes: number | null;
  dailyOvertimeAllowanceHours: number | null;
  weeklyOvertimeAllowanceHours: number | null;
}

// ── Overtime ───────────────────────────────────────────────────────────────────

export interface PerDayOvertimeDto {
  date: string;
  workedHours: number;
  targetHours: number;
  flexDelta: number;
}

export interface OvertimeResultDto {
  year: number;
  month: number;
  perDay: PerDayOvertimeDto[];
  runningBalanceHours: number;
  complianceFlags: Array<{
    date: string;
    type: number;
    hoursWorked: number;
    threshold: number;
  }>;
}

// ── Request payloads ──────────────────────────────────────────────────────────

export interface ClockInPayload {
  recordedAt?: string; // UTC ISO — optional, server falls back to its own stamp
  workedFromHome?: boolean;
  timeZoneId?: string;
}

export interface ClockOutPayload {
  recordedAt?: string;
  description?: string;
}

export interface EndBreakPayload {
  recordedAt?: string;
}

export interface UpdateWorkDayPayload {
  description?: string;
  workedFromHome?: boolean;
}

// ── Service ──────────────────────────────────────────────────────────────────

function recordedAt(offsetMinutes = 0): string {
  const d = new Date();
  d.setMinutes(d.getMinutes() + offsetMinutes);
  d.setSeconds(0);
  d.setMilliseconds(0);
  return d.toISOString();
}

export const workSessionService = {
  clockIn(offsetMinutes = 0, workedFromHome = false): Promise<WorkSessionDto> {
    const payload: ClockInPayload = {
      recordedAt: recordedAt(offsetMinutes),
      workedFromHome,
      timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone,
    };
    return api.post("/work-sessions/clock-in", payload).then((r) => r.data);
  },

  clockOut(offsetMinutes = 0, description?: string): Promise<WorkSessionDto> {
    const payload: ClockOutPayload = {
      recordedAt: recordedAt(offsetMinutes),
      description: description?.trim() || undefined,
    };
    return api.post("/work-sessions/clock-out", payload).then((r) => r.data);
  },

  startBreak(): Promise<BreakRecordDto> {
    return api.post("/work-sessions/break/start").then((r) => r.data);
  },

  endBreak(offsetMinutes = 0): Promise<BreakRecordDto> {
    const payload: EndBreakPayload = { recordedAt: recordedAt(offsetMinutes) };
    return api.post("/work-sessions/break/end", payload).then((r) => r.data);
  },

  getToday(): Promise<TodayStatusDto> {
    return api.get("/work-sessions/today").then((r) => r.data);
  },

  getTodayLive(): Promise<TodayLiveDto | null> {
    return api.get("/work-sessions/today-live").then((r) => r.data ?? null);
  },

  getSummaries(dateFrom?: string, dateTo?: string): Promise<WorkDaySummaryDto[]> {
    return api
      .get("/work-sessions/summaries", { params: { dateFrom, dateTo } })
      .then((r) => r.data);
  },

  getMySchedule(): Promise<WorkScheduleDto> {
    return api.get("/work-sessions/my-schedule").then((r) => r.data);
  },

  getOvertime(year?: number, month?: number): Promise<OvertimeResultDto> {
    const now = new Date();
    return api
      .get("/work-sessions/overtime", {
        params: { year: year ?? now.getFullYear(), month: month ?? now.getMonth() + 1 },
      })
      .then((r) => r.data);
  },

  updateDay(date: string, payload: UpdateWorkDayPayload): Promise<WorkDayDto> {
    return api.patch(`/work-sessions/${date}`, payload).then((r) => r.data);
  },
};
