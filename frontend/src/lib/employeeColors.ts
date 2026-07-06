const SLOT_COUNT = 8;

/** FNV-1a hash so an employee keeps the same color slot across sessions and views. */
function hashUserId(userId: string): number {
  let hash = 0x811c9dc5;
  for (let i = 0; i < userId.length; i++) {
    hash ^= userId.charCodeAt(i);
    hash = Math.imul(hash, 0x01000193);
  }
  return hash >>> 0;
}

/** Solid identity color for an employee (theme-aware via CSS variables). */
export function employeeColor(userId: string): string {
  return `var(--employee-color-${(hashUserId(userId) % SLOT_COUNT) + 1})`;
}

/** Translucent wash of the employee's color, for chip backgrounds. */
export function employeeColorWash(userId: string): string {
  return `color-mix(in srgb, ${employeeColor(userId)} 16%, transparent)`;
}
