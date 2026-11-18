import { describe, expect, it } from "vitest";
import { safeRedirect } from "../safeRedirect";

describe("safeRedirect", () => {
  it("keeps an in-app path with its query", () => {
    expect(safeRedirect("/admin/vacation-requests")).toBe("/admin/vacation-requests");
    expect(safeRedirect("/vacations?employeeId=abc")).toBe("/vacations?employeeId=abc");
  });

  it.each([
    "https://evil.example/phish",
    "//evil.example",
    "/\\evil.example",
    "javascript:alert(1)",
    "admin",
    "",
  ])("refuses %s", (target) => {
    expect(safeRedirect(target)).toBeNull();
  });

  it("ignores a missing or repeated parameter", () => {
    expect(safeRedirect(undefined)).toBeNull();
    expect(safeRedirect(["/a", "/b"])).toBeNull();
  });
});
