import { beforeEach, describe, expect, it, vi } from "vitest";

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), info: vi.fn() }));
vi.mock("../useAppToast", () => ({ useAppToast: () => toast }));

import { useApiCall } from "../useApiCall";

describe("useApiCall", () => {
  beforeEach(() => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    toast.success.mockReset();
    toast.error.mockReset();
  });

  it("returns the result of the wrapped call", async () => {
    const { execute } = useApiCall(async () => "saved");

    await expect(execute()).resolves.toBe("saved");
  });

  it("toggles loading around the call and clears it afterwards", async () => {
    let seenDuringCall = false;
    const call = useApiCall(async () => {
      seenDuringCall = call.loading.value;
      return 1;
    });

    await call.execute();

    expect(seenDuringCall).toBe(true);
    expect(call.loading.value).toBe(false);
  });

  it("swallows the failure and surfaces the server's message", async () => {
    // Views bind to `error`; an unhandled rejection here would break the page instead.
    const { execute, error } = useApiCall(async () => {
      throw { response: { data: { message: "Insufficient balance." } } };
    });

    await expect(execute()).resolves.toBeUndefined();
    expect(error.value).toBe("Insufficient balance.");
  });

  it("clears the loading flag when the call fails", async () => {
    const { execute, loading } = useApiCall(async () => {
      throw new Error("boom");
    });

    await execute();

    expect(loading.value).toBe(false);
  });

  it("clears a previous error before retrying", async () => {
    let shouldFail = true;
    const { execute, error } = useApiCall(async () => {
      if (shouldFail) throw new Error("boom");
      return "ok";
    });

    await execute();
    expect(error.value).not.toBe("");

    shouldFail = false;
    await execute();

    expect(error.value).toBe("");
  });

  it("shows a success toast only when one is configured", async () => {
    await useApiCall(async () => 1).execute();
    expect(toast.success).not.toHaveBeenCalled();

    await useApiCall(async () => 1, { successMessage: "Profile saved" }).execute();
    expect(toast.success).toHaveBeenCalledWith("Profile saved");
  });

  it("shows an error toast only when one is requested", async () => {
    const failing = async () => {
      throw new Error("boom");
    };

    await useApiCall(failing).execute();
    expect(toast.error).not.toHaveBeenCalled();

    await useApiCall(failing, { errorToast: true }).execute();
    expect(toast.error).toHaveBeenCalledWith("Something went wrong");
  });

  it("runs the success callback before reporting success", async () => {
    const order: string[] = [];
    const { execute } = useApiCall(async () => "value", {
      successMessage: "Saved",
      onSuccess: async () => {
        order.push("callback");
      },
    });
    toast.success.mockImplementation(() => order.push("toast"));

    await execute();

    expect(order).toEqual(["callback", "toast"]);
  });

  it("does not run the success callback when the call fails", async () => {
    const onSuccess = vi.fn();
    const { execute } = useApiCall(async () => {
      throw new Error("boom");
    }, { onSuccess });

    await execute();

    expect(onSuccess).not.toHaveBeenCalled();
  });
});
