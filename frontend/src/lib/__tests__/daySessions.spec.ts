import { describe, expect, it } from "vitest";
import {
  emptySession,
  toSnapshot,
  validateDaySessions,
  type EditableSession,
} from "../daySessions";

const session = (clockIn: string, clockOut: string, breaks: [string, string][] = []): EditableSession => ({
  clockIn,
  clockOut,
  breaks: breaks.map(([breakStart, breakEnd]) => ({ breakStart, breakEnd })),
});

describe("validateDaySessions", () => {
  it("accepts a normal day with a break", () => {
    expect(validateDaySessions([session("09:00", "17:00", [["12:00", "12:30"]])])).toBeNull();
  });

  it("requires a session unless an empty day is allowed (admin removing hours)", () => {
    expect(validateDaySessions([])).toBe("Add at least one session");
    expect(validateDaySessions([], { allowEmpty: true })).toBeNull();
  });

  it("rejects missing or reversed times", () => {
    expect(validateDaySessions([session("09:00", "")])).toMatch(/clock-in and clock-out/);
    expect(validateDaySessions([session("17:00", "09:00")])).toMatch(/after clock-in/);
  });

  it("rejects breaks outside the session or ending before they start", () => {
    expect(validateDaySessions([session("09:00", "17:00", [["08:30", "09:30"]])])).toMatch(/within the session/);
    expect(validateDaySessions([session("09:00", "17:00", [["13:00", "12:00"]])])).toMatch(/after break start/);
  });

  it("rejects overlapping sessions but allows back-to-back ones", () => {
    expect(validateDaySessions([session("09:00", "13:00"), session("12:00", "17:00")])).toBe(
      "Sessions must not overlap"
    );
    expect(validateDaySessions([session("09:00", "13:00"), session("13:00", "17:00")])).toBeNull();
  });
});

describe("toSnapshot", () => {
  it("keeps ids of existing records so the backend updates them in place", () => {
    const snapshot = toSnapshot("2026-09-07", [
      {
        workSessionId: 12,
        clockIn: "09:00",
        clockOut: "17:00",
        note: "auto-closed",
        breaks: [{ breakRecordId: 34, breakStart: "12:00", breakEnd: "12:30" }],
      },
    ]);

    expect(snapshot.sessions[0].workSessionId).toBe(12);
    expect(snapshot.sessions[0].breaks[0].breakRecordId).toBe(34);
    expect(snapshot.sessions[0]).not.toHaveProperty("note");
    expect(snapshot.sessions[0].clockIn).toMatch(/^2026-09-07T09:00:00[+-]\d{2}:\d{2}$/);
  });

  it("leaves ids out for new sessions and breaks", () => {
    const snapshot = toSnapshot("2026-09-07", [{ ...emptySession(), clockIn: "09:00", clockOut: "17:00" }]);

    expect(snapshot.sessions[0]).not.toHaveProperty("workSessionId");
  });
});
