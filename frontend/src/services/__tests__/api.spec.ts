import { beforeEach, describe, expect, it, vi } from "vitest";

const captured = vi.hoisted(() => ({
  config: null as Record<string, unknown> | null,
  onRejected: null as ((error: unknown) => unknown) | null,
}));

const authService = vi.hoisted(() => ({ clearSession: vi.fn() }));

vi.mock("axios", () => ({
  default: {
    create: vi.fn((config: Record<string, unknown>) => {
      captured.config = config;
      return {
        interceptors: {
          response: {
            use: (_onFulfilled: unknown, onRejected: (error: unknown) => unknown) => {
              captured.onRejected = onRejected;
            },
          },
        },
      };
    }),
  },
}));
vi.mock("../authService", () => ({ authService }));

import "../api";

describe("api client configuration", () => {
  it("sends the auth cookie with every request", () => {
    // The JWT lives in an HttpOnly cookie, so withCredentials is what authenticates at all.
    expect(captured.config?.withCredentials).toBe(true);
  });

  it("marks requests as XHR so the server can tell them from navigations", () => {
    const headers = captured.config?.headers as Record<string, string>;
    expect(headers["X-Requested-With"]).toBe("XMLHttpRequest");
  });
});

describe("401 handling", () => {
  const reject = (error: unknown) => captured.onRejected!(error);

  beforeEach(() => {
    authService.clearSession.mockClear();
    Object.defineProperty(window, "location", {
      value: { href: "http://localhost/dashboard" },
      writable: true,
      configurable: true,
    });
  });

  it("ends the session and sends the user to login when it expires mid-app", async () => {
    const error = { response: { status: 401 }, config: { url: "/worksessions/today" } };

    await expect(reject(error)).rejects.toBe(error);

    expect(authService.clearSession).toHaveBeenCalledOnce();
    expect(window.location.href).toBe("/login");
  });

  it("leaves a failed login on the page to show its own error", async () => {
    // Redirecting here would blow away the form and the "wrong password" message.
    const error = { response: { status: 401 }, config: { url: "/auth/login" } };

    await expect(reject(error)).rejects.toBe(error);

    expect(authService.clearSession).not.toHaveBeenCalled();
    expect(window.location.href).toBe("http://localhost/dashboard");
  });

  it("passes other error statuses straight through", async () => {
    const error = { response: { status: 500 }, config: { url: "/worksessions/today" } };

    await expect(reject(error)).rejects.toBe(error);

    expect(authService.clearSession).not.toHaveBeenCalled();
  });

  it("passes a network failure through without logging the user out", async () => {
    const error = { request: {}, config: { url: "/worksessions/today" } };

    await expect(reject(error)).rejects.toBe(error);

    expect(authService.clearSession).not.toHaveBeenCalled();
  });
});
