import api from "./api";

export type ClockEventType = "ClockIn" | "BreakStart" | "BreakEnd" | "ClockOut";

export interface ClockEvent {
  id: number;
  type: ClockEventType;
  recordedAt: string; // ISO 8601 UTC string
  description?: string;
}

export interface DaySummary {
  date: string; // "yyyy-MM-dd" local date
  clockIn: string | null; // ISO 8601 UTC string
  breakStart: string | null;
  breakEnd: string | null;
  clockOut: string | null;
  totalHours: number;
  description: string | null;
  isComplete: boolean;
  workedFromHome: boolean;
}

export interface SubmitClockEventPayload {
  type: number; // enum value: 0=ClockIn, 1=BreakStart, 2=BreakEnd, 3=ClockOut
  recordedAt: string; // ISO 8601 UTC string
  localDate: string; // "yyyy-MM-dd" — user's local calendar date
  description?: string;
  workedFromHome?: boolean;
}

export interface UpdateDayPayload {
  description?: string;
  workedFromHome?: boolean;
}

export const CLOCK_EVENT_TYPE_ORDER: ClockEventType[] = [
  "ClockIn",
  "BreakStart",
  "BreakEnd",
  "ClockOut",
];

export const CLOCK_EVENT_LABELS: Record<ClockEventType, string> = {
  ClockIn: "Clock In",
  BreakStart: "Start Break",
  BreakEnd: "End Break",
  ClockOut: "Clock Out",
};

export const CLOCK_EVENT_ENUM: Record<ClockEventType, number> = {
  ClockIn: 0,
  BreakStart: 1,
  BreakEnd: 2,
  ClockOut: 3,
};

export interface MyTarget {
  dailyHours: number | null;
  weeklyHours: number | null;
}

function localDateString(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

export const clockEventService = {
  getTodayEvents(): Promise<ClockEvent[]> {
    return api
      .get("/clockevents/today", { params: { localDate: localDateString(new Date()) } })
      .then((r) => r.data);
  },

  getSummaries(): Promise<DaySummary[]> {
    return api.get("/clockevents/summaries").then((r) => r.data);
  },

  submit(payload: SubmitClockEventPayload): Promise<ClockEvent> {
    return api.post("/clockevents", payload).then((r) => r.data);
  },

  updateDay(date: string, payload: UpdateDayPayload): Promise<DaySummary> {
    return api.patch(`/clockevents/${date}`, payload).then((r) => r.data);
  },

  getMyTarget(): Promise<MyTarget> {
    return api.get("/clockevents/target").then((r) => r.data);
  },
};
