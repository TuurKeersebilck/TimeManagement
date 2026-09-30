import { beforeEach, describe, expect, it, vi } from "vitest";

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
}));
vi.mock("../api", () => ({ default: api }));

import { vacationService } from "../vacationService";

/**
 * Every write here has a "me" form and an "on behalf of an employee" admin form that differ
 * only by URL. Sending an admin edit to the employee route would silently rewrite the admin's
 * own leave instead, so each pair is asserted explicitly.
 */
describe("vacationService", () => {
  beforeEach(() => {
    api.get.mockResolvedValue({ data: [], status: 200 });
    api.post.mockResolvedValue({ data: {} });
    api.put.mockResolvedValue({ data: {} });
    api.delete.mockResolvedValue({ data: {} });
  });

  const day = { vacationTypeId: 1, date: "2026-03-02", amount: 1 };

  it("reads the caller's own balances by default", async () => {
    await vacationService.getBalances(2026);

    expect(api.get).toHaveBeenCalledWith("/vacations/balances", { params: { year: 2026 } });
  });

  it("reads an employee's balances through the admin route", async () => {
    await vacationService.getBalances(2026, "user-1");

    expect(api.get).toHaveBeenCalledWith("/vacations/employees/user-1/balances", {
      params: { year: 2026 },
    });
  });

  it("omits the year parameter entirely when none is given", async () => {
    await vacationService.getBalances();

    expect(api.get).toHaveBeenCalledWith("/vacations/balances", { params: undefined });
  });

  it("creates leave for the caller or for an employee", async () => {
    await vacationService.create(day);
    await vacationService.create(day, "user-1");

    expect(api.post.mock.calls[0][0]).toBe("/vacations");
    expect(api.post.mock.calls[1][0]).toBe("/vacations/employees/user-1");
  });

  it("updates leave for the caller or for an employee", async () => {
    await vacationService.update(7, day);
    await vacationService.update(7, day, "user-1");

    expect(api.put.mock.calls[0][0]).toBe("/vacations/7");
    expect(api.put.mock.calls[1][0]).toBe("/vacations/employees/user-1/7");
  });

  it("deletes leave for the caller or for an employee", async () => {
    await vacationService.delete(7);
    await vacationService.delete(7, "user-1");

    expect(api.delete.mock.calls[0][0]).toBe("/vacations/7");
    expect(api.delete.mock.calls[1][0]).toBe("/vacations/employees/user-1/7");
  });

  it("books a range for the caller or for an employee", async () => {
    const range = { vacationTypeId: 1, startDate: "2026-03-02", endDate: "2026-03-06", amount: 1 };

    await vacationService.createRange(range);
    await vacationService.createRange(range, "user-1");

    expect(api.post.mock.calls[0][0]).toBe("/vacations/range");
    expect(api.post.mock.calls[1][0]).toBe("/vacations/employees/user-1/range");
  });

  it("reads the team calendar with its filters", async () => {
    await vacationService.getTeamVacationDays({ year: 2026, month: 3 });

    expect(api.get).toHaveBeenCalledWith("/vacations/team", { params: { year: 2026, month: 3 } });
  });

  it("treats an empty 204 response as no leave booked that day", async () => {
    // The API answers 204 rather than 404 when a date simply has nothing on it.
    api.get.mockResolvedValue({ status: 204, data: "" });

    await expect(vacationService.getVacationForDate("2026-03-02")).resolves.toBeNull();
  });

  it("returns the booking when the day has one", async () => {
    api.get.mockResolvedValue({ status: 200, data: { id: 3, amount: 0.5 } });

    await expect(vacationService.getVacationForDate("2026-03-02")).resolves.toMatchObject({ id: 3 });
  });
});
