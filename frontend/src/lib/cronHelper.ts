export type Frequency =
  | "15min"
  | "30min"
  | "hourly"
  | "daily"
  | "weekdays"
  | "weekly"
  | "monthly"
  | "custom"

export const COMMON_TIMEZONES = [
  "UTC",
  "Europe/Paris",
  "Europe/London",
  "Europe/Berlin",
  "Europe/Madrid",
  "America/New_York",
  "America/Chicago",
  "America/Denver",
  "America/Los_Angeles",
  "Asia/Dubai",
  "Asia/Singapore",
  "Asia/Tokyo",
  "Australia/Sydney",
]

export function getDefaultTimezone(): string {
  try {
    const tz = Intl.DateTimeFormat().resolvedOptions().timeZone
    return tz || "UTC"
  } catch {
    return "UTC"
  }
}

const DAY_NAMES = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"]

export function buildCron(
  freq: Frequency,
  time: string,
  weeklyDays: number[], // 0 = Sun, 1 = Mon ... 6 = Sat
  monthDay: number,
  customCron: string
): string {
  const [hStr, mStr] = (time || "09:00").split(":")
  const hour = parseInt(hStr || "9", 10)
  const minute = parseInt(mStr || "0", 10)

  switch (freq) {
    case "15min":
      return "*/15 * * * *"
    case "30min":
      return "*/30 * * * *"
    case "hourly":
      return `${minute} * * * *`
    case "daily":
      return `${minute} ${hour} * * *`
    case "weekdays":
      return `${minute} ${hour} * * 1-5`
    case "weekly": {
      const days = weeklyDays.length > 0 ? [...weeklyDays].sort().join(",") : "1"
      return `${minute} ${hour} * * ${days}`
    }
    case "monthly":
      return `${minute} ${hour} ${Math.max(1, Math.min(31, monthDay))} * *`
    case "custom":
      return customCron.trim() || "0 9 * * *"
  }
}

export function cronToHuman(cron: string, tz?: string | null): string {
  const clean = cron.trim()
  const tzSuffix = tz ? ` (${tz})` : ""

  if (clean === "*/15 * * * *") return `Every 15 minutes${tzSuffix}`
  if (clean === "*/30 * * * *") return `Every 30 minutes${tzSuffix}`
  if (clean === "0 * * * *") return `Every hour${tzSuffix}`

  const parts = clean.split(/\s+/)
  if (parts.length !== 5) return `cron ${clean}${tzSuffix}`

  const [minStr, hourStr, dom, mon, dow] = parts
  const min = minStr.padStart(2, "0")
  const hour = hourStr.padStart(2, "0")
  const timeFormatted = `${hour}:${min}`

  if (clean.startsWith("*/") && dom === "*" && mon === "*" && dow === "*") {
    const step = clean.slice(2, clean.indexOf(" "))
    return `Every ${step} minutes${tzSuffix}`
  }

  if (hourStr === "*" && dom === "*" && mon === "*" && dow === "*") {
    return `Hourly at :${min}${tzSuffix}`
  }

  if (dom === "*" && mon === "*" && dow === "*") {
    return `Daily at ${timeFormatted}${tzSuffix}`
  }

  if (dom === "*" && mon === "*" && (dow === "1-5" || dow === "1,2,3,4,5")) {
    return `Weekdays at ${timeFormatted}${tzSuffix}`
  }

  if (dom === "*" && mon === "*" && dow !== "*") {
    const days = dow.split(",").map(d => {
      const idx = parseInt(d, 10)
      return DAY_NAMES[idx % 7] ?? d
    }).join(", ")
    return `Every ${days} at ${timeFormatted}${tzSuffix}`
  }

  if (dom !== "*" && mon === "*" && dow === "*") {
    const d = parseInt(dom, 10)
    const suffix = d === 1 ? "st" : d === 2 ? "nd" : d === 3 ? "rd" : "th"
    return `Monthly on the ${d}${suffix} at ${timeFormatted}${tzSuffix}`
  }

  return `cron ${clean}${tzSuffix}`
}
