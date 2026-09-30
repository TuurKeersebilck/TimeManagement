import { beforeEach, describe, expect, it, vi } from "vitest";

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
}));
vi.mock("../api", () => ({ default: api }));

import { adjustmentRequestService } from "../adjustmentRequestService";
import { calendarFeedService } from "../calendarFeedService";
import { holidayService } from "../holidayService";
import { inviteService } from "../inviteService";
import { notificationService } from "../notificationService";
import { setupService } from "../setupService";
import { vacationTypeService } from "../vacationTypeService";

beforeEach(() => {
  api.get.mockResolvedValue({ data: [] });
  api.post.mockResolvedValue({ data: {} });
  api.put.mockResolvedValue({ data: {} });
  api.patch.mockResolvedValue({ data: {} });
  api.delete.mockResolvedValue({ data: {} });
});

describe("adjustmentRequestService", () => {
  it("separates the employee's own list from the admin list", async () => {
    await adjustmentRequestService.getMine();
    await adjustmentRequestService.getAll();

    expect(api.get.mock.calls[0][0]).toBe("/timeadjustmentrequests/mine");
    expect(api.get.mock.calls[1][0]).toBe("/timeadjustmentrequests");
  });

  it("posts approve and reject to the request's own routes", async () => {
    await adjustmentRequestService.approve(5);
    await adjustmentRequestService.reject(6);

    expect(api.post.mock.calls[0][0]).toBe("/timeadjustmentrequests/5/approve");
    expect(api.post.mock.calls[1][0]).toBe("/timeadjustmentrequests/6/reject");
  });

  it("posts an admin's direct day edit to its own route", async () => {
    const payload = { userId: "u1", date: "2026-09-07", desiredDaySnapshot: { sessions: [] } };

    await adjustmentRequestService.adminEditDay(payload);

    expect(api.post).toHaveBeenCalledWith("/timeadjustmentrequests/admin-edit", payload);
  });

  it("submits the snapshot payload unchanged", async () => {
    const payload = {
      date: "2026-03-02",
      reason: "Forgot to clock out",
      desiredDaySnapshot: { sessions: [] },
    } as never;

    await adjustmentRequestService.create(payload);

    expect(api.post).toHaveBeenCalledWith("/timeadjustmentrequests", payload);
  });
});

describe("calendarFeedService", () => {
  it("reads and regenerates the feed token", async () => {
    api.get.mockResolvedValue({ data: { hasToken: true } });
    api.post.mockResolvedValue({ data: { feedUrl: "https://example.test/feed.ics" } });

    await expect(calendarFeedService.getTokenInfo()).resolves.toEqual({ hasToken: true });
    await expect(calendarFeedService.regenerateToken()).resolves.toMatchObject({
      feedUrl: "https://example.test/feed.ics",
    });
    expect(api.get).toHaveBeenCalledWith("/calendar/token");
    expect(api.post).toHaveBeenCalledWith("/calendar/token/regenerate");
  });
});

describe("holidayService", () => {
  it("reads the employee-visible holidays from the shared route", async () => {
    await holidayService.getHolidays(2026);

    expect(api.get).toHaveBeenCalledWith("/publicholidays/2026");
  });

  it("reads the admin holiday list from the settings route", async () => {
    // Employees and admins see different lists; swapping these would 403 for employees.
    await holidayService.getAdminHolidays(2026);

    expect(api.get).toHaveBeenCalledWith("/admin/settings/holidays/2026");
  });

  it("refreshes holidays for a year", async () => {
    await holidayService.refreshHolidays(2026);

    expect(api.post).toHaveBeenCalledWith("/admin/settings/holidays/refresh/2026");
  });

  it("adds a custom holiday from its date and name", async () => {
    await holidayService.addCustomHoliday("2026-03-02", "Company Day");

    expect(api.post).toHaveBeenCalledWith("/admin/settings/holidays", {
      date: "2026-03-02",
      name: "Company Day",
    });
  });

  it("patches the working-day flag on one holiday", async () => {
    await holidayService.setIsWorkingDay(4, true);

    expect(api.patch).toHaveBeenCalledWith("/admin/settings/holidays/4/is-working-day", {
      isWorkingDay: true,
    });
  });

  it("sets the holiday country", async () => {
    await holidayService.setCountry("BE");

    expect(api.put).toHaveBeenCalledWith("/admin/settings/country", { countryCode: "BE" });
  });
});

describe("inviteService", () => {
  it("validates a token as a query parameter, not a path segment", async () => {
    await inviteService.validateToken("raw-token");

    expect(api.get).toHaveBeenCalledWith("/invites/validate", { params: { token: "raw-token" } });
  });

  it("creates and cancels invites", async () => {
    await inviteService.createInvite("new@example.test");
    await inviteService.cancelInvite(3);

    expect(api.post).toHaveBeenCalledWith("/invites", { email: "new@example.test" });
    expect(api.delete).toHaveBeenCalledWith("/invites/3");
  });
});

describe("notificationService", () => {
  it("unwraps the unread count from its envelope", async () => {
    api.get.mockResolvedValue({ data: { count: 4 } });

    await expect(notificationService.getUnreadCount()).resolves.toBe(4);
  });

  it("marks one or all notifications read", async () => {
    await notificationService.markAsRead(9);
    await notificationService.markAllAsRead();

    expect(api.put.mock.calls[0][0]).toBe("/notifications/9/read");
    expect(api.put.mock.calls[1][0]).toBe("/notifications/read-all");
  });
});

describe("setupService", () => {
  it("caches the setup status so every route guard does not re-query it", async () => {
    api.get.mockResolvedValue({ data: { setupRequired: true } });

    await expect(setupService.isSetupRequired()).resolves.toBe(true);
    await expect(setupService.isSetupRequired()).resolves.toBe(true);

    expect(api.get).toHaveBeenCalledTimes(1);
  });

  it("stops reporting setup as required once it completes", async () => {
    // The cached true from the previous test must not outlive completing setup.
    await setupService.complete({
      fullName: "Adam Admin",
      email: "adam@example.test",
      password: "Password1",
      confirmPassword: "Password1",
    });

    expect(api.post).toHaveBeenCalledWith("/setup/complete", expect.objectContaining({
      email: "adam@example.test",
    }));
    await expect(setupService.isSetupRequired()).resolves.toBe(false);
  });
});

describe("vacationTypeService", () => {
  it("manages vacation types under the admin route", async () => {
    await vacationTypeService.getAll();
    await vacationTypeService.create({ name: "Study Leave" });
    await vacationTypeService.delete(2);

    expect(api.get).toHaveBeenCalledWith("/admin/vacation-types");
    expect(api.post).toHaveBeenCalledWith("/admin/vacation-types", { name: "Study Leave" });
    expect(api.delete).toHaveBeenCalledWith("/admin/vacation-types/2");
  });

  it("reads an employee's balances", async () => {
    await vacationTypeService.getEmployeeBalances("u1");

    expect(api.get).toHaveBeenCalledWith("/admin/employees/u1/vacation-balances");
  });
});
