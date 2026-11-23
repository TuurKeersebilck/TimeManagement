import { beforeEach, describe, expect, it, vi } from "vitest";

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
}));
vi.mock("../api", () => ({ default: api }));

import { vacationRequestService } from "../vacationRequestService";

describe("vacationRequestService", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.get.mockResolvedValue({ data: [] });
    api.post.mockResolvedValue({ data: {} });
  });

  it("posts a new request as given", async () => {
    const payload = {
      vacationTypeId: 3,
      startDate: "2026-03-02",
      endDate: "2026-03-06",
      amount: 1,
    };

    await vacationRequestService.create(payload);

    expect(api.post).toHaveBeenCalledWith("/vacationrequests", payload);
  });

  it("reads the pending count out of its wrapper", async () => {
    api.get.mockResolvedValueOnce({ data: { count: 4 } });

    await expect(vacationRequestService.getPendingCount()).resolves.toBe(4);
    expect(api.get).toHaveBeenCalledWith("/vacationrequests/pending-count");
  });

  it("returns the booked days when approving", async () => {
    api.post.mockResolvedValueOnce({ data: { created: [{ id: 1 }], skippedWeekends: 2 } });

    const result = await vacationRequestService.approve(7);

    expect(api.post).toHaveBeenCalledWith("/vacationrequests/7/approve");
    expect(result.created).toHaveLength(1);
  });

  it("sends a trimmed reason when denying, and none when it is blank", async () => {
    await vacationRequestService.reject(7, "  Busy week  ");
    await vacationRequestService.reject(8, "   ");

    expect(api.post).toHaveBeenNthCalledWith(1, "/vacationrequests/7/reject", {
      reason: "Busy week",
    });
    expect(api.post).toHaveBeenNthCalledWith(2, "/vacationrequests/8/reject", {
      reason: undefined,
    });
  });

  it("deletes a draft and submits all drafts through their own endpoints", async () => {
    api.post.mockResolvedValueOnce({ data: [{ id: 1, status: "Pending" }] });

    await vacationRequestService.deleteDraft(4);
    const submitted = await vacationRequestService.submit();

    expect(api.delete).toHaveBeenCalledWith("/vacationrequests/4");
    expect(api.post).toHaveBeenCalledWith("/vacationrequests/submit");
    expect(submitted).toEqual([{ id: 1, status: "Pending" }]);
  });

  it("cancels through its own endpoint", async () => {
    await vacationRequestService.cancel(5);

    expect(api.post).toHaveBeenCalledWith("/vacationrequests/5/cancel");
  });
});
