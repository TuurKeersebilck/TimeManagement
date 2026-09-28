import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
}));
vi.mock("../api", () => ({ default: api }));

import { workSessionService } from "../workSessionService";

describe("workSessionService", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-03-02T09:17:43.512Z"));
    api.get.mockResolvedValue({ data: {} });
    api.post.mockResolvedValue({ data: {} });
    api.put.mockResolvedValue({ data: {} });
    api.patch.mockResolvedValue({ data: {} });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  const payloadOf = (mock: typeof api.post) => mock.mock.calls[0][1];

  it("clocks in with a whole-minute timestamp and the browser time zone", async () => {
    await workSessionService.clockIn();

    const [url, payload] = api.post.mock.calls[0];
    expect(url).toBe("/worksessions/clock-in");
    // The backend truncates to the minute; sending seconds would guarantee a mismatch
    // against its own stamp and silently fall back to server time.
    expect(payload.recordedAt).toBe("2026-03-02T09:17:00.000Z");
    expect(payload.workedFromHome).toBe(false);
    expect(payload.timeZoneId).toBeTruthy();
  });

  it("applies the manual minute offset when clocking in", async () => {
    await workSessionService.clockIn(-5, true);

    const payload = payloadOf(api.post);
    expect(payload.recordedAt).toBe("2026-03-02T09:12:00.000Z");
    expect(payload.workedFromHome).toBe(true);
  });

  it("applies the manual minute offset when clocking out", async () => {
    await workSessionService.clockOut(-3, "Finished the report");

    const [url, payload] = api.post.mock.calls[0];
    expect(url).toBe("/worksessions/clock-out");
    expect(payload.recordedAt).toBe("2026-03-02T09:14:00.000Z");
    expect(payload.description).toBe("Finished the report");
  });

  it("omits a description that is only whitespace", async () => {
    // Sending "   " would overwrite a real description with blanks.
    await workSessionService.clockOut(0, "   ");

    expect(payloadOf(api.post).description).toBeUndefined();
  });

  it("omits the description when none is given", async () => {
    await workSessionService.clockOut();

    expect(payloadOf(api.post).description).toBeUndefined();
  });

  it("trims a description before sending it", async () => {
    await workSessionService.clockOut(0, "  tidy  ");

    expect(payloadOf(api.post).description).toBe("tidy");
  });

  it("posts breaks to their own endpoints", async () => {
    await workSessionService.startBreak();
    await workSessionService.endBreak(2);

    expect(api.post.mock.calls[0][0]).toBe("/worksessions/break/start");
    expect(api.post.mock.calls[1][0]).toBe("/worksessions/break/end");
    expect(api.post.mock.calls[1][1].recordedAt).toBe("2026-03-02T09:19:00.000Z");
  });

  it("defaults the overtime query to the current month", async () => {
    await workSessionService.getOvertime();

    expect(api.get).toHaveBeenCalledWith("/worksessions/overtime", {
      params: { year: 2026, month: 3 },
    });
  });

  it("honours an explicit overtime month", async () => {
    await workSessionService.getOvertime(2025, 11);

    expect(api.get).toHaveBeenCalledWith("/worksessions/overtime", {
      params: { year: 2025, month: 11 },
    });
  });

  it("passes the date range through to the summaries endpoint", async () => {
    await workSessionService.getSummaries("2026-03-01", "2026-03-31");

    expect(api.get).toHaveBeenCalledWith("/worksessions/summaries", {
      params: { dateFrom: "2026-03-01", dateTo: "2026-03-31" },
    });
  });

  it("patches a single day by its date", async () => {
    await workSessionService.updateDay("2026-03-02", { workedFromHome: true });

    expect(api.patch).toHaveBeenCalledWith("/worksessions/2026-03-02", { workedFromHome: true });
  });

  it("wraps the weekday list when saving default home-working days", async () => {
    await workSessionService.setDefaultWfhWeekdays([1, 4]);

    expect(api.put).toHaveBeenCalledWith("/worksessions/default-wfh-weekdays", {
      weekdays: [1, 4],
    });
  });

  it("unwraps the response body for reads", async () => {
    api.get.mockResolvedValue({ data: { openSession: null, closedSessions: [], workDay: null } });

    await expect(workSessionService.getToday()).resolves.toEqual({
      openSession: null,
      closedSessions: [],
      workDay: null,
    });
  });
});
