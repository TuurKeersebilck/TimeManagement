import { describe, expect, it } from "vitest";
import { formatDuration, formatHours, formatSignedDuration, formatSignedHours } from "../hours";

describe("formatHours", () => {
  it("writes decimal hours like the payroll export: two decimals at most, no trailing zeros", () => {
    expect(formatHours(8)).toBe("8h");
    expect(formatHours(8.5)).toBe("8.5h");
    expect(formatHours(7 + 40 / 60)).toBe("7.67h");
    expect(formatHours(5 / 60)).toBe("0.08h");
    expect(formatHours(172.05)).toBe("172.05h");
  });

  it("keeps the sign of a negative amount and never shows -0", () => {
    expect(formatHours(-2)).toBe("-2h");
    expect(formatHours(-0.001)).toBe("0h");
  });
});

describe("formatSignedHours", () => {
  it("always signs a balance and shows zero plainly", () => {
    expect(formatSignedHours(3 + 59 / 60)).toBe("+3.98h");
    expect(formatSignedHours(-(1 + 20 / 60))).toBe("-1.33h");
    expect(formatSignedHours(0)).toBe("0h");
    expect(formatSignedHours(-0.004)).toBe("0h");
  });

  it("matches the dashboard example: 2h57m is 2.95h", () => {
    expect(formatSignedHours(2 + 57 / 60)).toBe("+2.95h");
  });
});

describe("formatDuration", () => {
  it("writes hours and minutes, rounded to the minute", () => {
    expect(formatDuration(8)).toBe("8h00m");
    expect(formatDuration(7 + 36 / 60)).toBe("7h36m");
    expect(formatDuration(5 / 60)).toBe("0h05m");
    expect(formatDuration(7 + 59.6 / 60)).toBe("8h00m");
  });

  it("keeps the sign of a negative amount and never shows -0", () => {
    expect(formatDuration(-2.5)).toBe("-2h30m");
    expect(formatDuration(-0.001)).toBe("0h00m");
  });
});

describe("formatSignedDuration", () => {
  it("always signs a balance and shows zero plainly", () => {
    expect(formatSignedDuration(3 + 59 / 60)).toBe("+3h59m");
    expect(formatSignedDuration(-(1 + 20 / 60))).toBe("-1h20m");
    expect(formatSignedDuration(0)).toBe("0h00m");
    expect(formatSignedDuration(-0.004)).toBe("0h00m");
  });
});
