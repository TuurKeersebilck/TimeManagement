import { ref } from "vue";
import { useAppToast } from "./useAppToast";
import { extractApiError } from "../utils/apiError";

interface ApiCallOptions {
  /** Show a success toast with this message on completion. */
  successMessage?: string;
  /** Show an error toast automatically when the call fails. */
  errorToast?: boolean;
  /** Callback invoked after a successful call (before loading is cleared). */
  onSuccess?: () => void | Promise<void>;
}

/**
 * Wraps an async API call with automatic loading state, error extraction,
 * and optional toast notifications.
 *
 * @example
 * const { execute: save, loading: saving, error } = useApiCall(
 *   () => profileService.update(form.value),
 *   { successMessage: "Profile saved", errorToast: true }
 * );
 */
export function useApiCall<T>(fn: () => Promise<T>, options: ApiCallOptions = {}) {
  const loading = ref(false);
  const error = ref("");
  const toast = useAppToast();

  const execute = async (): Promise<T | undefined> => {
    loading.value = true;
    error.value = "";
    try {
      const result = await fn();
      if (options.onSuccess) await options.onSuccess();
      if (options.successMessage) toast.success(options.successMessage);
      return result;
    } catch (err: unknown) {
      const msg = extractApiError(err);
      error.value = msg;
      if (options.errorToast) toast.error(msg);
      return undefined;
    } finally {
      loading.value = false;
    }
  };

  return { execute, loading, error };
}
