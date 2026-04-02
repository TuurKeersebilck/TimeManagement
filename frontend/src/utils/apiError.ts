type ApiError = { response?: { data?: { message?: string } } };

/**
 * Extracts a human-readable error message from an Axios error response.
 * Falls back to the provided fallback string if no server message is present.
 */
export function extractApiError(err: unknown, fallback = "Something went wrong"): string {
  return (err as ApiError)?.response?.data?.message ?? fallback;
}
