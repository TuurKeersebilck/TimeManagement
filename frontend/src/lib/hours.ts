/**
 * One notation for hours everywhere in the app, the same as the payroll export: decimal hours
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
