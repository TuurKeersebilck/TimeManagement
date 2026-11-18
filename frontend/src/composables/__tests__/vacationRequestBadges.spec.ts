import { describe, expect, it, vi } from "vitest";

const requests = vi.hoisted(() => ({ getMine: vi.fn(), getPendingCount: vi.fn() }));
vi.mock("@/services/vacationRequestService", () => ({ vacationRequestService: requests }));

import { useDraftRequests } from "../useDraftRequests";
import { usePendingVacationRequests } from "../usePendingVacationRequests";

describe("useDraftRequests", () => {
  it("counts only the employee's unsent drafts", async () => {
    requests.getMine.mockResolvedValueOnce([
      { status: "Draft" },
      { status: "Draft" },
      { status: "Pending" },
      { status: "Approved" },
    ]);
    const { draftCount, refreshDraftCount } = useDraftRequests();

    await refreshDraftCount();

    expect(draftCount.value).toBe(2);
  });

  it("keeps the last count when the request fails", async () => {
    const { draftCount, refreshDraftCount } = useDraftRequests();
    draftCount.value = 3;
    requests.getMine.mockRejectedValueOnce(new Error("offline"));

    await refreshDraftCount();

    expect(draftCount.value).toBe(3);
  });
});

describe("usePendingVacationRequests", () => {
  it("shares one count between every caller", async () => {
    requests.getPendingCount.mockResolvedValueOnce(4);

    await usePendingVacationRequests().refreshPendingVacationCount();

    expect(usePendingVacationRequests().pendingVacationCount.value).toBe(4);
  });
});
