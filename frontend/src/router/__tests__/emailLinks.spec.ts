import { describe, expect, it, vi } from "vitest";

vi.mock("@/services/setupService", () => ({
  setupService: { isSetupRequired: vi.fn(async () => false) },
}));
vi.mock("@/services/authService", () => ({
  authService: { isAuthenticated: () => true, getRoles: () => ["Admin"] },
}));

import router from "../index";

// Paths the backend puts in emails. If a page moves, its email link must move with it
// (see VacationRequestService.ReviewPagePath and the other email senders).
describe("pages linked from emails", () => {
  it.each([
    ["/admin/vacation-requests", "admin-vacation-requests"],
    ["/admin/settlements", "admin-settlements"],
    ["/register?token=abc", "register"],
    ["/reset-password?token=abc", "reset-password"],
  ])("%s exists", (path, name) => {
    expect(router.resolve(path).name).toBe(name);
  });
});
