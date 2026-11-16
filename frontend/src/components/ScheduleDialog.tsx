import { useState, useMemo } from "react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
  type Frequency,
  COMMON_TIMEZONES,
  getDefaultTimezone,
  buildCron,
  cronToHuman,
} from "@/lib/cronHelper"

interface Props {
  isOpen: boolean
  onClose: () => void
  onSave: (schedule: {
    name: string
    cron: string
    timezone: string
    overlap: number
  }) => Promise<void>
}

const WEEKDAYS = [
  { label: "Mon", value: 1 },
  { label: "Tue", value: 2 },
  { label: "Wed", value: 3 },
  { label: "Thu", value: 4 },
  { label: "Fri", value: 5 },
  { label: "Sat", value: 6 },
  { label: "Sun", value: 0 },
]

export function ScheduleDialog({ isOpen, onClose, onSave }: Props) {
  const [name, setName] = useState("Scheduled Run")
  const [frequency, setFrequency] = useState<Frequency>("daily")
  const [time, setTime] = useState("09:00")
  const [weeklyDays, setWeeklyDays] = useState<number[]>([1]) // Monday
  const [monthDay, setMonthDay] = useState(1)
  const [customCron, setCustomCron] = useState("0 9 * * 1-5")
  const [timezone, setTimezone] = useState(() => getDefaultTimezone())
  const [overlap, setOverlap] = useState(0) // 0 = skip, 1 = queue
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState("")

  const computedCron = useMemo(() => {
    return buildCron(frequency, time, weeklyDays, monthDay, customCron)
  }, [frequency, time, weeklyDays, monthDay, customCron])

  const humanSummary = useMemo(() => {
    return cronToHuman(computedCron, timezone)
  }, [computedCron, timezone])

  if (!isOpen) return null

  const toggleDay = (day: number) => {
    setWeeklyDays(prev => {
      if (prev.includes(day)) {
        if (prev.length === 1) return prev // Keep at least one
        return prev.filter(d => d !== day)
      }
      return [...prev, day]
    })
  }

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault()
    setError("")
    setSaving(true)
    try {
      await onSave({
        name: name.trim() || "Scheduled Run",
        cron: computedCron,
        timezone: timezone.trim() || "UTC",
        overlap,
      })
      onClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create schedule")
    } finally {
      setSaving(false)
    }
  }

  const timezones = Array.from(new Set([timezone, ...COMMON_TIMEZONES])).sort()

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-xs animate-overlay-fade">
      <div
        role="dialog"
        aria-modal="true"
        className="w-full max-w-md rounded-lg border bg-background p-5 shadow-xl space-y-4 animate-dialog-pop"
      >
        <div className="flex items-start justify-between">
          <div>
            <h2 className="text-base font-semibold">Add Automated Schedule</h2>
            <p className="text-xs text-muted-foreground mt-0.5">
              Run this pipeline automatically on a recurring schedule.
            </p>
          </div>
          <button
            onClick={onClose}
            className="text-muted-foreground hover:text-foreground text-sm font-mono px-1.5 py-0.5 rounded"
          >
            ✕
          </button>
        </div>

        {error && (
          <div className="text-xs bg-destructive/10 border border-destructive/20 text-destructive p-2 rounded">
            {error}
          </div>
        )}

        <form onSubmit={handleSave} className="space-y-3.5 text-xs">
          <div className="space-y-1">
            <label className="font-medium text-foreground">Schedule Name</label>
            <Input
              className="h-8 text-xs"
              placeholder="e.g. Daily Morning Sync"
              value={name}
              onChange={e => setName(e.target.value)}
              required
            />
          </div>

          <div className="space-y-1">
            <label className="font-medium text-foreground">Frequency</label>
            <select
              className="w-full h-8 rounded border bg-background px-2 text-xs focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              value={frequency}
              onChange={e => setFrequency(e.target.value as Frequency)}
            >
              <option value="daily">Daily</option>
              <option value="weekdays">Weekdays (Mon – Fri)</option>
              <option value="weekly">Weekly (Specific Days)</option>
              <option value="monthly">Monthly</option>
              <option value="hourly">Hourly</option>
              <option value="30min">Every 30 minutes</option>
              <option value="15min">Every 15 minutes</option>
              <option value="custom">Custom Cron Expression</option>
            </select>
          </div>

          {frequency !== "15min" && frequency !== "30min" && frequency !== "custom" && (
            <div className="space-y-1">
              <label className="font-medium text-foreground">
                {frequency === "hourly" ? "Minute of hour" : "Run Time"}
              </label>
              {frequency === "hourly" ? (
                <div className="flex items-center gap-2">
                  <span className="text-muted-foreground">At minute</span>
                  <input
                    type="number"
                    min={0}
                    max={59}
                    className="w-20 h-8 rounded border bg-background px-2 text-xs text-center font-mono"
                    value={time.split(":")[1] || "00"}
                    onChange={e => {
                      const m = Math.max(0, Math.min(59, parseInt(e.target.value, 10) || 0))
                      setTime(`00:${m.toString().padStart(2, "0")}`)
                    }}
                  />
                  <span className="text-muted-foreground">past each hour</span>
                </div>
              ) : (
                <input
                  type="time"
                  className="w-full h-8 rounded border bg-background px-2 text-xs font-mono"
                  value={time}
                  onChange={e => setTime(e.target.value)}
                  required
                />
              )}
            </div>
          )}

          {frequency === "weekly" && (
            <div className="space-y-1.5">
              <label className="font-medium text-foreground">Repeat on</label>
              <div className="flex gap-1.5">
                {WEEKDAYS.map(d => {
                  const active = weeklyDays.includes(d.value)
                  return (
                    <button
                      key={d.value}
                      type="button"
                      onClick={() => toggleDay(d.value)}
                      className={`flex-1 h-7 text-xs rounded border transition-colors font-medium ${
                        active
                          ? "bg-primary text-primary-foreground border-primary"
                          : "bg-muted/30 text-muted-foreground hover:bg-muted"
                      }`}
                    >
                      {d.label}
                    </button>
                  )
                })}
              </div>
            </div>
          )}

          {frequency === "monthly" && (
            <div className="space-y-1">
              <label className="font-medium text-foreground">Day of the Month</label>
              <select
                className="w-full h-8 rounded border bg-background px-2 text-xs"
                value={monthDay}
                onChange={e => setMonthDay(parseInt(e.target.value, 10))}
              >
                {Array.from({ length: 31 }, (_, i) => i + 1).map(d => (
                  <option key={d} value={d}>
                    {d === 1 ? "1st" : d === 2 ? "2nd" : d === 3 ? "3rd" : `${d}th`} of the month
                  </option>
                ))}
              </select>
            </div>
          )}

          {frequency === "custom" && (
            <div className="space-y-1">
              <label className="font-medium text-foreground">Cron Expression (Standard 5-part)</label>
              <Input
                className="h-8 font-mono text-xs"
                placeholder="0 9 * * 1-5"
                value={customCron}
                onChange={e => setCustomCron(e.target.value)}
                required
              />
              <p className="text-[10px] text-muted-foreground">Format: min hour day-of-month month day-of-week</p>
            </div>
          )}

          <div className="space-y-1">
            <label className="font-medium text-foreground">Timezone</label>
            <select
              className="w-full h-8 rounded border bg-background px-2 text-xs font-mono"
              value={timezone}
              onChange={e => setTimezone(e.target.value)}
            >
              {timezones.map(tz => (
                <option key={tz} value={tz}>
                  {tz}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-1">
            <label className="font-medium text-foreground">Concurrency / Overlap</label>
            <div className="flex gap-2">
              <label className="flex items-center gap-1.5 cursor-pointer">
                <input
                  type="radio"
                  name="overlap"
                  checked={overlap === 0}
                  onChange={() => setOverlap(0)}
                />
                <span>Skip if previous run is still running</span>
              </label>
              <label className="flex items-center gap-1.5 cursor-pointer">
                <input
                  type="radio"
                  name="overlap"
                  checked={overlap === 1}
                  onChange={() => setOverlap(1)}
                />
                <span>Queue next run</span>
              </label>
            </div>
          </div>

          <div className="rounded border bg-muted/30 p-2.5 space-y-1">
            <div className="flex items-center justify-between text-[11px]">
              <span className="font-medium text-foreground">Summary:</span>
              <code className="font-mono text-muted-foreground">{computedCron}</code>
            </div>
            <p className="text-xs text-primary font-medium">{humanSummary}</p>
          </div>

          <div className="flex justify-end gap-2 pt-2 border-t">
            <Button type="button" variant="outline" size="sm" onClick={onClose} disabled={saving}>
              Cancel
            </Button>
            <Button type="submit" size="sm" disabled={saving}>
              {saving ? "Creating..." : "Save Schedule"}
            </Button>
          </div>
        </form>
      </div>
    </div>
  )
}
