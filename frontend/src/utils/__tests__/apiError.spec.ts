import { beforeEach, describe, expect, it, vi } from "vitest";
import { extractApiError } from "../apiError";

describe("extractApiError", () => {
  beforeEach(() => {
    // The helper always logs the raw error; silence it so runs stay readable.
    vi.spyOn(console, "error").mockImplementation(() => {});
  });

  it("prefers the message the API sent back", () => {
    const err = { response: { data: { message: "You are already clocked in." } } };

    expect(extractApiError(err)).toBe("You are already clocked in.");
  });

  it("explains a request that never got a response as a connectivity problem", () => {
    // Axios sets `request` but no `response` when the server is unreachable.
    const err = { request: {}, message: "Network Error" };

    expect(extractApiError(err)).toBe(
      "Network error. Please check your connection and try again.",
    );
  });

  it("falls back to the caller's message when the shape is unrecognised", () => {
    expect(extractApiError(new Error("boom"), "Could not save settings")).toBe(
      "Could not save settings",
    );
  });

  it("has a generic fallback when the caller supplies none", () => {
    expect(extractApiError(undefined)).toBe("Something went wrong");
  });

  it("falls back when the response carries no message field", () => {
    expect(extractApiError({ response: { data: {} } }, "Fallback")).toBe("Fallback");
  });

  it("prefers a server message even when a request object is present", () => {
    const err = { request: {}, response: { data: { message: "Validation failed" } } };

    expect(extractApiError(err)).toBe("Validation failed");
  });

  it("logs the raw error for debugging", () => {
    extractApiError({ response: { data: { message: "nope" } } });

    expect(console.error).toHaveBeenCalledWith("[API Error]", expect.anything());
  });
});
