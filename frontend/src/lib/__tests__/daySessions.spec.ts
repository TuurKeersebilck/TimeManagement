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

  it("rejects missing or reversed times, naming the session and the times", () => {
    expect(validateDaySessions([session("09:00", "")])).toBe("Session 1: fill in both clock-in and clock-out");
    expect(validateDaySessions([session("17:00", "09:00")])).toBe(
      "Session 1: clock-out (09:00) must be after clock-in (17:00)"
    );
  });

  it("names the break that ends after its session's clock-out", () => {
    // The reported case: lunch entered as a break in the morning session that ends at noon.
    const day = [session("09:00", "12:00", [["12:00", "12:30"]]), session("12:30", "17:30")];

    expect(validateDaySessions(day)).toBe(
      "Session 1, break 1: the break (12:00–12:30) ends after clock-out (12:00)"
    );
  });

  it("names breaks that start too early, are reversed or incomplete", () => {
    expect(validateDaySessions([session("09:00", "17:00", [["08:30", "09:30"]])])).toBe(
      "Session 1, break 1: the break (08:30–09:30) starts before clock-in (09:00)"
    );
    expect(validateDaySessions([session("09:00", "17:00", [["12:00", "12:30"], ["13:00", "12:00"]])])).toBe(
      "Session 1, break 2: break end (12:00) must be after break start (13:00)"
    );
    expect(validateDaySessions([session("09:00", "17:00", [["12:00", ""]])])).toBe(
      "Session 1, break 1: fill in both break start and break end"
    );
  });

  it("names overlapping sessions by their number in the form, whatever their order", () => {
    expect(validateDaySessions([session("12:00", "17:00"), session("09:00", "13:00")])).toBe(
      "Session 2 (09:00–13:00) overlaps session 1 (12:00–17:00)"
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
