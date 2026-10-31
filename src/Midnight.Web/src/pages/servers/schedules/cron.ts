import { schedulesApi, type CronParts, type Schedule } from "@/api/server-features";

type Translate = (key: string, ...args: Array<string | number>) => string;

export interface CronPreset {
  key: string;
  parts: CronParts;
}

const cron = (minute: string, hour: string, dayOfMonth: string, month: string, dayOfWeek: string): CronParts => ({
  minute,
  hour,
  dayOfMonth,
  month,
  dayOfWeek,
});

export const cronPresets: CronPreset[] = [
  { key: "Every5Minutes", parts: cron("*/5", "*", "*", "*", "*") },
  { key: "Every15Minutes", parts: cron("*/15", "*", "*", "*", "*") },
  { key: "Hourly", parts: cron("0", "*", "*", "*", "*") },
  { key: "Every6Hours", parts: cron("0", "*/6", "*", "*", "*") },
  { key: "DailyAt4", parts: cron("0", "4", "*", "*", "*") },
  { key: "WeeklyMonday", parts: cron("0", "4", "*", "*", "1") },
  { key: "MonthlyFirst", parts: cron("0", "4", "1", "*", "*") },
];

export function cronExpression(parts: CronParts) {
  return [parts.minute, parts.hour, parts.dayOfMonth, parts.month, parts.dayOfWeek].join(" ");
}

export function sameCron(a: CronParts, b: CronParts) {
  return cronExpression(a) === cronExpression(b);
}

/** One cron field: *, 5, 1-5, 1,2,3, *\/5, 1-30/5 */
export const cronFieldPattern = /^(\*|\d+(-\d+)?)(\/\d+)?(,(\*|\d+(-\d+)?)(\/\d+)?)*$/;

const isNumber = (value: string) => /^\d+$/.test(value);
const step = (value: string) => /^\*\/(\d+)$/.exec(value)?.[1];
const pad = (value: string) => value.padStart(2, "0");

function weekday(value: string) {
  // 2023-01-01 was a Sunday, cron 0 and 7 = Sunday
  const day = Number(value) % 7;
  return new Intl.DateTimeFormat("en", { weekday: "long" }).format(new Date(2023, 0, 1 + day));
}

function ordinal(value: string) {
  const n = Number(value);
  const suffix = n % 100 >= 11 && n % 100 <= 13 ? "th" : ({ 1: "st", 2: "nd", 3: "rd" } as Record<number, string>)[n % 10] ?? "th";
  return `${n}${suffix}`;
}

/** Readable English summary of the common cron patterns, falls back to the expression */
export function describeCron(parts: CronParts, t: Translate): string {
  const { minute, hour, dayOfMonth, month, dayOfWeek } = parts;
  const anyDay = dayOfMonth === "*" && month === "*" && dayOfWeek === "*";

  if (anyDay) {
    if (minute === "*" && hour === "*") return t("ServerSchedules.Cron.EveryMinute");

    const minuteStep = step(minute);
    if (minuteStep && hour === "*") return t("ServerSchedules.Cron.EveryNMinutes", Number(minuteStep));

    if (isNumber(minute) && hour === "*") return t("ServerSchedules.Cron.HourlyAt", pad(minute));

    const hourStep = step(hour);
    if (isNumber(minute) && hourStep) return t("ServerSchedules.Cron.EveryNHours", Number(hourStep), pad(minute));

    if (isNumber(minute) && isNumber(hour)) return t("ServerSchedules.Cron.DailyAt", `${pad(hour)}:${pad(minute)}`);
  }

  if (isNumber(minute) && isNumber(hour) && month === "*") {
    const time = `${pad(hour)}:${pad(minute)}`;

    if (dayOfMonth === "*" && isNumber(dayOfWeek)) return t("ServerSchedules.Cron.WeeklyAt", weekday(dayOfWeek), time);
    if (dayOfMonth === "*" && dayOfWeek === "1-5") return t("ServerSchedules.Cron.WeekdaysAt", time);
    if (isNumber(dayOfMonth) && dayOfWeek === "*") return t("ServerSchedules.Cron.MonthlyAt", ordinal(dayOfMonth), time);
  }

  return t("ServerSchedules.Cron.Custom", cronExpression(parts));
}

/** Toggles IsActive with a full update (the api has no separate toggle) */
export async function saveActive(uuid: string, schedule: Schedule, isActive: boolean) {
  const { id, name, minute, hour, dayOfMonth, month, dayOfWeek, onlyWhenOnline } = schedule;
  return schedulesApi.update(uuid, id, { name, minute, hour, dayOfMonth, month, dayOfWeek, onlyWhenOnline, isActive });
}

