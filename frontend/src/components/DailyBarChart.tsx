import { useState } from 'react'
import { formatDay } from '../format'

export interface DailyPoint {
  date: string
  value: number
  detail: string
}

/**
 * Single-series daily column chart. Thin capped bars (rounded data-end, square baseline),
 * recessive hairline grid, and a per-bar hover tooltip whose hit target is the whole column.
 */
export function DailyBarChart({ points, format, label }: { points: DailyPoint[]; format: (v: number) => string; label: string }) {
  const [hover, setHover] = useState<number>()
  const max = Math.max(...points.map((p) => p.value), 0)
  const top = max > 0 ? niceCeiling(max) : 1
  const ticks = [top, top / 2, 0]
  const labelEvery = Math.ceil(points.length / 6)

  return (
    <div className="chart" role="img" aria-label={`${label} per day`}>
      <div className="chart-y">
        {ticks.map((t) => (
          <span key={t} style={{ bottom: `${(t / top) * 100}%` }}>
            {format(t)}
          </span>
        ))}
      </div>
      <div className="chart-plot">
        <div className="chart-bars" onMouseLeave={() => setHover(undefined)}>
          {ticks.map((t) => (
            <div key={t} className="chart-grid" style={{ bottom: `${(t / top) * 100}%` }} />
          ))}
          {points.map((p, i) => (
            <div
              key={p.date}
              className={`chart-col ${hover === i ? 'active' : ''}`}
              onMouseEnter={() => setHover(i)}
              onFocus={() => setHover(i)}
              onBlur={() => setHover(undefined)}
              tabIndex={0}
              aria-label={`${formatDay(p.date)}: ${format(p.value)}`}
            >
              {p.value > 0 && <div className="chart-bar" style={{ height: `${(p.value / top) * 100}%` }} />}
              {hover === i && (
                <div className={`chart-tip ${i > points.length / 2 ? 'left' : ''}`}>
                  <strong>{formatDay(p.date)}</strong>
                  <span>{format(p.value)}</span>
                  <span className="muted">{p.detail}</span>
                </div>
              )}
            </div>
          ))}
        </div>
        <div className="chart-x">
          {points.map((p, i) => (
            <span key={p.date}>{i % labelEvery === 0 || i === points.length - 1 ? formatDay(p.date) : ''}</span>
          ))}
        </div>
      </div>
    </div>
  )
}

/** Rounds the axis maximum up to 1, 2 or 5 × a power of ten. */
function niceCeiling(value: number) {
  const power = 10 ** Math.floor(Math.log10(value))
  const step = [1, 2, 5, 10].find((m) => m * power >= value) ?? 10
  return step * power
}
