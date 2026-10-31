/** Only local paths are allowed as return url to prevent open redirects */
export function safeReturnUrl(value: string | null) {
  return value && value.startsWith("/") && !value.startsWith("//") && !value.startsWith("/\\") ? value : "/";
}

/** Links set by admins (footer): local paths and http(s) only, no javascript: or data: urls */
export function isSafeLink(value: string | null | undefined) {
  if (!value) return false;
  if (value.startsWith("/")) return !value.startsWith("//") && !value.startsWith("/\\");
  return /^https?:\/\//i.test(value);
}
