/**
 * The notation for hours on admin pages, the same as the payroll export: decimal hours
 * with at most two decimals and no trailing zeros — 8h, 8.5h, 7.67h (40 min), 0.08h (5 min).
 * Timers that tick (the running clock, a break countdown) keep their clock notation.
 */

function roundHours(hours: number): number {
  // Round the magnitude so -0.004 doesn't turn into "-0h".
  const rounded = Math.round(Math.abs(hours) * 100) / 100;
  return hours < 0 ? -rounded : rounded;
}

/** An amount of hours: "8h", "8.5h", "7.67h". Negative amounts keep their minus sign. */
export function formatHours(hours: number): string {
  const rounded = roundHours(hours);
  return `${rounded === 0 ? 0 : rounded}h`;
}

/** A balance or difference, always signed: "+3.98h", "-1.33h", "0h". */
export function formatSignedHours(hours: number): string {
  const rounded = roundHours(hours);
  if (rounded === 0) return "0h";
  return `${rounded > 0 ? "+" : ""}${rounded}h`;
}

/*
 * The employee's own Time Tracking page reads hours as time instead: hours and minutes,
 * 7h36m rather than 7.6h. Admin pages and the export keep the decimal notation above.
 */

function splitMinutes(hours: number): { h: number; m: number } {
  // Round to whole minutes first so 7h59.6m becomes 8h00m, not 7h60m.
  const totalMinutes = Math.round(Math.abs(hours) * 60);
  return { h: Math.floor(totalMinutes / 60), m: totalMinutes % 60 };
}

/** An amount of time: "8h00m", "7h36m", "0h05m". Negative amounts keep their minus sign. */
export function formatDuration(hours: number): string {
  const { h, m } = splitMinutes(hours);
  const sign = hours < 0 && h + m > 0 ? "-" : "";
  return `${sign}${h}h${m.toString().padStart(2, "0")}m`;
}

/** A balance or difference in time, always signed: "+3h59m", "-1h20m", "0h00m". */
export function formatSignedDuration(hours: number): string {
  const { h, m } = splitMinutes(hours);
  const sign = h + m === 0 ? "" : hours < 0 ? "-" : "+";
  return `${sign}${h}h${m.toString().padStart(2, "0")}m`;
}
