import { beforeEach, describe, expect, it, vi } from "vitest";

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
}));
vi.mock("../api", () => ({ default: api }));

import { authService } from "../authService";

describe("authService requests", () => {
  beforeEach(() => {
    api.post.mockResolvedValue({ data: {} });
    api.get.mockResolvedValue({ data: {} });
    api.put.mockResolvedValue({ data: {} });
  });

  it("posts credentials to the login endpoint", async () => {
    const credentials = { email: "emma@example.test", password: "hunter2!", rememberMe: true };
    api.post.mockResolvedValue({ data: { email: credentials.email, fullName: "Emma", roles: ["Employee"] } });

    const result = await authService.login(credentials);

    expect(api.post).toHaveBeenCalledWith("/auth/login", credentials);
    expect(result.roles).toEqual(["Employee"]);
  });

  it("reads the current user from the profile endpoint", async () => {
    api.get.mockResolvedValue({ data: { id: "u1", roles: ["Admin"] } });

    await expect(authService.getCurrentUser()).resolves.toMatchObject({ id: "u1" });
    expect(api.get).toHaveBeenCalledWith("/auth/profile");
  });

  it("sends only the token and the two passwords when resetting", async () => {
    await authService.resetPassword("tok", "NewPass1", "NewPass1");

    expect(api.post).toHaveBeenCalledWith("/auth/reset-password", {
      token: "tok",
      newPassword: "NewPass1",
      confirmPassword: "NewPass1",
    });
  });

  it("wraps the email in an object when asking for a reset link", async () => {
    await authService.forgotPassword("emma@example.test");

    expect(api.post).toHaveBeenCalledWith("/auth/forgot-password", { email: "emma@example.test" });
  });
});

describe("authService session state", () => {
  it("stores only non-sensitive display fields", () => {
    authService.setUserInfo("emma@example.test", "Emma Employee", ["Admin"]);

    const raw = localStorage.getItem("user_info")!;
    expect(JSON.parse(raw)).toEqual({
      email: "emma@example.test",
      fullName: "Emma Employee",
      roles: ["Admin"],
    });
    // The JWT lives in an HttpOnly cookie and must never be mirrored into storage.
    expect(raw).not.toMatch(/token/i);
  });

  it("reports no session before anyone logs in", () => {
    expect(authService.isAuthenticated()).toBe(false);
    expect(authService.getUserInfo()).toBeNull();
    expect(authService.getRoles()).toEqual([]);
  });

  it("reports a session once user info is stored", () => {
    authService.setUserInfo("emma@example.test", "Emma Employee", ["Employee"]);

    expect(authService.isAuthenticated()).toBe(true);
    expect(authService.getRoles()).toEqual(["Employee"]);
  });

  it("survives corrupted storage instead of throwing on every page load", () => {
    localStorage.setItem("user_info", "{not json");

    expect(authService.getUserInfo()).toBeNull();
    expect(authService.getRoles()).toEqual([]);
  });

  it("clears the stored session", () => {
    authService.setUserInfo("emma@example.test", "Emma Employee", ["Admin"]);

    authService.clearSession();

    expect(authService.isAuthenticated()).toBe(false);
  });

  it("clears the session on logout even when the API call fails", async () => {
    // A user who clicks log out must end up logged out locally regardless.
    authService.setUserInfo("emma@example.test", "Emma Employee", ["Admin"]);
    api.post.mockRejectedValue(new Error("network down"));

    await authService.logout();

    expect(authService.isAuthenticated()).toBe(false);
  });

  it("calls the logout endpoint so the server can revoke the token", async () => {
    api.post.mockResolvedValue({ data: {} });

    await authService.logout();

    expect(api.post).toHaveBeenCalledWith("/auth/logout");
  });
});
