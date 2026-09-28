import { beforeEach, describe, expect, it, vi } from "vitest";

const workSessionService = vi.hoisted(() => ({ getSummaries: vi.fn() }));
vi.mock("@/services/workSessionService", () => ({ workSessionService }));
vi.mock("../../services/workSessionService", () => ({ workSessionService }));

type Store = typeof import("../useClockEventsStore");

/**
 * The store keeps its summaries at module scope so every view shares one copy. That makes the
 * cache the whole point of the composable — and makes module state the thing tests must reset.
 */
describe("useClockEventsStore", () => {
  let useClockEventsStore: Store["useClockEventsStore"];

  beforeEach(async () => {
    vi.resetModules();
    workSessionService.getSummaries.mockReset();
    workSessionService.getSummaries.mockResolvedValue([{ date: "2026-03-02" }]);
    ({ useClockEventsStore } = await import("../useClockEventsStore"));
  });

  it("fetches the summaries on first use", async () => {
    const { fetchSummaries, summaries } = useClockEventsStore();

    await fetchSummaries();

    expect(workSessionService.getSummaries).toHaveBeenCalledOnce();
    expect(summaries.value).toEqual([{ date: "2026-03-02" }]);
  });

  it("serves later callers from the cache", async () => {
    const { fetchSummaries } = useClockEventsStore();
    await fetchSummaries();

    await fetchSummaries();

    expect(workSessionService.getSummaries).toHaveBeenCalledOnce();
  });

  it("refetches when forced", async () => {
    const { fetchSummaries } = useClockEventsStore();
    await fetchSummaries();

    await fetchSummaries(true);

    expect(workSessionService.getSummaries).toHaveBeenCalledTimes(2);
  });

  it("refreshSummaries always goes back to the API", async () => {
    // Clocking out must not leave the dashboard showing the pre-clock-out day.
    const { fetchSummaries, refreshSummaries } = useClockEventsStore();
    await fetchSummaries();

    await refreshSummaries();

    expect(workSessionService.getSummaries).toHaveBeenCalledTimes(2);
  });

  it("shares state between separate call sites", async () => {
    await useClockEventsStore().fetchSummaries();

    expect(useClockEventsStore().summaries.value).toHaveLength(1);
  });

  it("clearing the cache makes the next read hit the API again", async () => {
    const { fetchSummaries, clearCache, summaries } = useClockEventsStore();
    await fetchSummaries();

    clearCache();

    expect(summaries.value).toEqual([]);
    await fetchSummaries();
    expect(workSessionService.getSummaries).toHaveBeenCalledTimes(2);
  });

  it("clears the loading flag and rethrows when the fetch fails", async () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    workSessionService.getSummaries.mockRejectedValue(new Error("offline"));
    const { fetchSummaries, loading } = useClockEventsStore();

    await expect(fetchSummaries()).rejects.toThrow("offline");

    // A stuck spinner is the failure mode a missing finally block produces.
    expect(loading.value).toBe(false);
  });

  it("does not cache a failed fetch", async () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    workSessionService.getSummaries.mockRejectedValueOnce(new Error("offline"));
    const { fetchSummaries, summaries } = useClockEventsStore();

    await expect(fetchSummaries()).rejects.toThrow();
    await fetchSummaries();

    expect(summaries.value).toEqual([{ date: "2026-03-02" }]);
  });
});
