type ApiError = {
  response?: { data?: { message?: string } };
  request?: unknown;
  message?: string;
};

/**
 * Extracts a human-readable error message from an Axios error response.
 *
 * - Always logs the raw error to the console for debugging.
 * - Returns the server's message if the API responded with one.
 * - Returns a "network error" message if the request was sent but no response arrived.
 * - Falls back to the provided fallback string otherwise.
 */
export function extractApiError(err: unknown, fallback = "Something went wrong"): string {
  console.error("[API Error]", err);

  const apiErr = err as ApiError;

  if (apiErr?.response?.data?.message) {
    return apiErr.response.data.message;
  }

  // Request was sent but no response received — connectivity or server-down issue
  if (!apiErr?.response && apiErr?.request) {
    return "Network error. Please check your connection and try again.";
  }

  return fallback;
}
