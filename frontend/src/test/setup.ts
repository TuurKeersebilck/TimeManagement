import { afterEach, beforeEach, vi } from "vitest";

beforeEach(() => {
  localStorage.clear();
});

afterEach(() => {
  // Composables keep module-level state, so anything a test leaves behind would
  // otherwise leak into the next one through the shared module instance.
  vi.clearAllMocks();
  localStorage.clear();
});
