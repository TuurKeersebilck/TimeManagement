import { beforeEach, describe, expect, it, vi } from "vitest";

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
}));
vi.mock("../api", () => ({ default: api }));

import { OUTCOME_LABELS, STATUS_LABELS, settlementService } from "../settlementService";

describe("settlementService", () => {
  beforeEach(() => {
    api.get.mockResolvedValue({ data: [] });
    api.post.mockResolvedValue({ data: [] });
  });

  it("lists a month's settlements", async () => {
    await settlementService.getAll(2026, 3);

    expect(api.get).toHaveBeenCalledWith("/settlements", { params: { year: 2026, month: 3 } });
  });

  it("sends the generate month as query parameters with no body", async () => {
    await settlementService.generate(2026, 3);

    expect(api.post).toHaveBeenCalledWith("/settlements/generate", null, {
      params: { year: 2026, month: 3 },
    });
  });

  it("posts the full allocation when confirming", async () => {
    const payload = { paidOutHours: 4, carryForwardHours: 2, notes: "Paid with March payroll" };

    await settlementService.confirm(12, payload);

    expect(api.post).toHaveBeenCalledWith("/settlements/12/confirm", payload);
  });

  it("resolves confirm to nothing rather than leaking the raw response", async () => {
    await expect(settlementService.confirm(12, { paidOutHours: 0, carryForwardHours: 0 }))
      .resolves.toBeUndefined();
  });

  it("reads one employee's settlement history", async () => {
    await settlementService.getEmployeeHistory("u1");

    expect(api.get).toHaveBeenCalledWith("/settlements/employee/u1");
  });
});

describe("settlement labels", () => {
  it("has a label for every outcome the API can return", () => {
    // LeaveDeducted is legacy but still present on historical rows, so it must render.
    expect(Object.keys(OUTCOME_LABELS).sort()).toEqual(["LeaveDeducted", "Paid", "Unpaid"]);
    expect(OUTCOME_LABELS.LeaveDeducted).toBe("Leave Deducted");
  });

  it("has a label for every status", () => {
    expect(STATUS_LABELS.PendingReview).toBe("Pending Review");
    expect(STATUS_LABELS.Settled).toBe("Settled");
  });
});
