import { beforeEach, describe, expect, it, vi } from "vitest";

const authService = vi.hoisted(() => ({
  getCurrentUser: vi.fn(),
  setUserInfo: vi.fn(),
  clearSession: vi.fn(),
  getRoles: vi.fn(() => [] as string[]),
}));
const push = vi.hoisted(() => vi.fn());

vi.mock("../../services/authService", () => ({ authService }));
vi.mock("vue-router", () => ({ useRouter: () => ({ push }) }));
vi.mock("../useClockEventsStore", () => ({
  useClockEventsStore: () => ({ clearCache: vi.fn() }),
}));
vi.mock("../useNotifications", () => ({
  useNotifications: () => ({ reset: vi.fn() }),
}));

type Module = typeof import("../useAuth");

const emma = {
  id: "u1",
  email: "emma@example.test",
  fullName: "Emma Employee",
  userName: "emma@example.test",
  roles: ["Admin"],
};

describe("useAuth", () => {
  let useAuth: Module["useAuth"];

  beforeEach(async () => {
    vi.resetModules();
    push.mockReset();
    authService.getRoles.mockReturnValue([]);
    authService.getCurrentUser.mockReset().mockResolvedValue(emma);
    authService.setUserInfo.mockReset();
    authService.clearSession.mockReset();
    ({ useAuth } = await import("../useAuth"));
  });

  it("loads the current user and exposes it", async () => {
    const { fetchUser, currentUser } = useAuth();

    await fetchUser();

    expect(currentUser.value).toEqual(emma);
  });

  it("mirrors the fetched user into storage so role checks do not go stale", async () => {
    const { fetchUser } = useAuth();

    await fetchUser();

    expect(authService.setUserInfo).toHaveBeenCalledWith(
      "emma@example.test",
      "Emma Employee",
      ["Admin"],
    );
  });

  it("does not refetch once the user is loaded", async () => {
    const { fetchUser } = useAuth();
    await fetchUser();

    await fetchUser();

    expect(authService.getCurrentUser).toHaveBeenCalledOnce();
  });

  it("refetches when forced", async () => {
    const { fetchUser } = useAuth();
    await fetchUser();

    await fetchUser(true);

    expect(authService.getCurrentUser).toHaveBeenCalledTimes(2);
  });

  it("clears the loading flag after a successful load", async () => {
    const { fetchUser, isLoadingUser } = useAuth();

    await fetchUser();

    expect(isLoadingUser.value).toBe(false);
  });

  it("sends the user to login when the profile call fails", async () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    authService.getCurrentUser.mockRejectedValue(new Error("401"));
    const { fetchUser, currentUser, isLoadingUser } = useAuth();

    const result = await fetchUser();

    expect(result).toBeNull();
    expect(currentUser.value).toBeNull();
    expect(authService.clearSession).toHaveBeenCalled();
    expect(push).toHaveBeenCalledWith("/login");
    expect(isLoadingUser.value).toBe(false);
  });

  it("reports admin from the roles it just fetched", async () => {
    const { fetchUser, isAdmin } = useAuth();
    expect(isAdmin.value).toBe(false);

    await fetchUser();

    expect(isAdmin.value).toBe(true);
  });

  it("drops the admin flag when the user is cleared", async () => {
    // A logout that left isAdmin true would keep admin-only navigation visible.
    const { fetchUser, clearUser, isAdmin } = useAuth();
    await fetchUser();

    clearUser();

    expect(isAdmin.value).toBe(false);
  });

  it("clearing the user allows the next login to fetch again", async () => {
    const { fetchUser, clearUser, currentUser } = useAuth();
    await fetchUser();

    clearUser();
    expect(currentUser.value).toBeNull();

    await fetchUser();
    expect(authService.getCurrentUser).toHaveBeenCalledTimes(2);
  });

  it("builds initials from the first and last name", async () => {
    const { fetchUser, userInitials } = useAuth();

    await fetchUser();

    expect(userInitials.value).toBe("EE");
  });

  it("uses the first two letters for a single-word name", async () => {
    authService.getCurrentUser.mockResolvedValue({ ...emma, fullName: "Prince" });
    const { fetchUser, userInitials } = useAuth();

    await fetchUser();

    expect(userInitials.value).toBe("PR");
  });

  it("takes the outer initials of a three-part name", async () => {
    authService.getCurrentUser.mockResolvedValue({ ...emma, fullName: "Ana Maria Silva" });
    const { fetchUser, userInitials } = useAuth();

    await fetchUser();

    expect(userInitials.value).toBe("AS");
  });

  it("shows a placeholder before anyone is loaded", () => {
    expect(useAuth().userInitials.value).toBe("??");
  });
});
