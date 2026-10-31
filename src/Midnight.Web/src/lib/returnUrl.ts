/** Only local paths are allowed as return url to prevent open redirects */
export function safeReturnUrl(value: string | null) {
  return value && value.startsWith("/") && !value.startsWith("//") && !value.startsWith("/\\") ? value : "/";
}
