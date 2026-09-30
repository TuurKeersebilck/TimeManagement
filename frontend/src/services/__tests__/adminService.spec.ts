import { beforeEach, describe, expect, it, vi } from "vitest";

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
}));
vi.mock("../api", () => ({ default: api }));

import { adminService } from "../adminService";

describe("adminService", () => {
  beforeEach(() => {
    api.get.mockResolvedValue({ data: [] });
    api.post.mockResolvedValue({ data: {} });
    api.put.mockResolvedValue({ data: {} });
    api.delete.mockResolvedValue({ data: {} });
  });

  it("passes the time-log filters through", async () => {
    await adminService.getAllTimeLogs({ userId: "u1", dateFrom: "2026-03-01", dateTo: "2026-03-31" });

    expect(api.get).toHaveBeenCalledWith("/admin/timelogs", {
      params: { userId: "u1", dateFrom: "2026-03-01", dateTo: "2026-03-31" },
    });
  });

  it("omits the role filter when listing everyone", async () => {
    await adminService.getEmployees();

    expect(api.get).toHaveBeenCalledWith("/admin/employees", { params: undefined });
  });

  it("filters employees by role when asked", async () => {
    await adminService.getEmployees("Admin");

    expect(api.get).toHaveBeenCalledWith("/admin/employees", { params: { role: "Admin" } });
  });

  it("uses distinct endpoints for disable, enable and delete", async () => {
    await adminService.disableEmployee("u1");
    await adminService.enableEmployee("u1");
    await adminService.deleteEmployee("u1");

    expect(api.put.mock.calls[0][0]).toBe("/admin/employees/u1/disable");
    expect(api.put.mock.calls[1][0]).toBe("/admin/employees/u1/enable");
    expect(api.delete).toHaveBeenCalledWith("/admin/employees/u1");
  });

  it("wraps the workday schedule in a targets object", async () => {
    const targets = [{ dayOfWeek: 1, hours: 8 }];

    await adminService.setEmployeeWorkdayTargets("u1", targets);

    expect(api.put).toHaveBeenCalledWith("/admin/employees/u1/workday-targets", { targets });
  });

  it("defaults the weekly summary to eight weeks", async () => {
    await adminService.getWeeklySummary("u1");

    expect(api.get).toHaveBeenCalledWith("/admin/employees/u1/weekly-summary", {
      params: { weeks: 8 },
    });
  });

  it("creates and deletes time bank adjustments on their own routes", async () => {
    await adminService.createTimeBankAdjustment("u1", {
      effectiveDate: "2026-03-01",
      hours: -2,
      reason: "correction",
    });
    await adminService.deleteTimeBankAdjustment(9);

    expect(api.post).toHaveBeenCalledWith("/admin/employees/u1/time-bank-adjustments", {
      effectiveDate: "2026-03-01",
      hours: -2,
      reason: "correction",
    });
    expect(api.delete).toHaveBeenCalledWith("/admin/time-bank-adjustments/9");
  });
});

describe("payroll export download", () => {
  beforeEach(() => {
    api.get.mockResolvedValue({ data: "Date,Employee\n" });
    globalThis.URL.createObjectURL = vi.fn(() => "blob:payroll");
    globalThis.URL.revokeObjectURL = vi.fn();
  });

  it("names the file with a zero-padded month", async () => {
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});

    await adminService.downloadDailyPayrollExport(2026, 3);

    const anchor = click.mock.instances[0] as HTMLAnchorElement;
    expect(anchor.download).toBe("hours_2026_03.csv");
  });

  it("requests the export as a blob", async () => {
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});

    await adminService.downloadDailyPayrollExport(2026, 9, "u1");

    expect(api.get).toHaveBeenCalledWith("/admin/export/daily", {
      params: { year: 2026, month: 9, userId: "u1" },
      responseType: "blob",
    });
  });

  it("adds a filename-safe employee name to a per-employee export", async () => {
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});

    await adminService.downloadDailyPayrollExport(2026, 9, "u1", "Élise De Smet-Dubois");

    const anchor = click.mock.instances[0] as HTMLAnchorElement;
    expect(anchor.download).toBe("hours_2026_09_elise-de-smet-dubois.csv");
  });

  it("releases the object URL after triggering the download", async () => {
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});

    await adminService.downloadDailyPayrollExport(2026, 12);

    expect(globalThis.URL.revokeObjectURL).toHaveBeenCalledWith("blob:payroll");
  });

  it("drops an empty employee filter rather than sending a blank id", async () => {
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});

    await adminService.downloadDailyPayrollExport(2026, 3, "");

    expect(api.get.mock.calls[0][1].params.userId).toBeUndefined();
  });
});
