const compact = new Intl.NumberFormat('en', { notation: 'compact', maximumFractionDigits: 1 })
const whole = new Intl.NumberFormat('en')

/** 1,284 / 12.9K / 4.2M */
export const formatCount = (n: number) => (Math.abs(n) < 10_000 ? whole.format(n) : compact.format(n))

/** Costs are often fractions of a cent, so small amounts keep more precision. */
export function formatUsd(value: number | null | undefined) {
  if (value == null) return 'not priced'
  if (value === 0) return '$0.00'
  if (value < 0.01) return `$${value.toFixed(4)}`
  if (value < 100) return `$${value.toFixed(2)}`
  return `$${compact.format(value)}`
}

export const formatDay = (isoDate: string) =>
  new Date(`${isoDate}T00:00:00Z`).toLocaleDateString(undefined, { month: 'short', day: 'numeric', timeZone: 'UTC' })
