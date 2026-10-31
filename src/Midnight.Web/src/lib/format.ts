export function formatBytes(bytes: number) {
  if (!bytes) return "0 B";

  const units = ["B", "KB", "MB", "GB", "TB"];
  const index = Math.min(units.length - 1, Math.floor(Math.log(bytes) / Math.log(1024)));

  return `${(bytes / 1024 ** index).toFixed(index === 0 ? 0 : 1)} ${units[index]}`;
}

/** System.Text.Json writes TimeSpan as "d.hh:mm:ss.fffffff" or "hh:mm:ss.fffffff" */
export function formatTimeSpan(value: string) {
  const match = /^(?:(\d+)\.)?(\d+):(\d+):(\d+)/.exec(value);
  if (!match) return value;

  const [, days, hours, minutes, seconds] = match;
  return days ? `${days}d ${Number(hours)}h ${Number(minutes)}m` : `${Number(hours)}h ${Number(minutes)}m ${Number(seconds)}s`;
}

export function percent(used: number, total: number) {
  return total > 0 ? Math.round((used / total) * 100) : 0;
}
