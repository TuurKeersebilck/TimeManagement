/**
 * The in-app path to continue to after signing in, e.g. the review page an emailed link pointed
 * at. Only same-app paths are accepted ("/x", not "//host", "/\host" or a full URL), so the
 * parameter can't be used to send someone to another site.
 */
export function safeRedirect(target: unknown): string | null {
  if (typeof target !== "string" || !target.startsWith("/")) return null;
  if (target.startsWith("//") || target.startsWith("/\\")) return null;
  return target;
}
