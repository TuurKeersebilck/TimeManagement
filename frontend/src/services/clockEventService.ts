import api from "./api";

export type ClockEventType = "ClockIn" | "BreakStart" | "BreakEnd" | "ClockOut";

export interface ClockEvent {
  id: number;
  date: string;
  type: ClockEventType;
  recordedTime: string; // "HH:mm:ss"
  description?: string;
}

export interface DaySummary {
  date: string; // "yyyy-MM-dd"
  clockIn: string | null;
  breakStart: string | null;
  breakEnd: string | null;
  clockOut: string | null;
  totalHours: number;
  description: string | null;
  isComplete: boolean;
}

export interface SubmitClockEventPayload {
  type: number; // enum value: 0=ClockIn, 1=BreakStart, 2=BreakEnd, 3=ClockOut
  recordedTime: string; // "HH:mm:ss"
  description?: string;
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

export const clockEventService = {
  getTodayEvents(): Promise<ClockEvent[]> {
    return api.get("/clockevents/today").then((r) => r.data);
  },

  getSummaries(): Promise<DaySummary[]> {
    return api.get("/clockevents/summaries").then((r) => r.data);
  },

  submit(payload: SubmitClockEventPayload): Promise<ClockEvent> {
    return api.post("/clockevents", payload).then((r) => r.data);
  },

  getMyTarget(): Promise<MyTarget> {
    return api.get("/clockevents/target").then((r) => r.data);
  },
};
