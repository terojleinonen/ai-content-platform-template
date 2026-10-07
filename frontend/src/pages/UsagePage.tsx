import { useEffect, useState } from 'react'
import { api } from '../api'
import { DailyBarChart } from '../components/DailyBarChart'
import { formatCount, formatUsd } from '../format'
import type { AiCallStatus, UsageGroup, UsageReport } from '../types'

const RANGES = [7, 30, 90] as const
type Metric = 'cost' | 'calls' | 'tokens'

const OPERATION_LABELS: Record<string, string> = {
  Generate: 'Generate',
  Variant: 'Variant',
  Transform: 'AI edit',
  TermTranslation: 'Term translation',
  Image: 'Image',
}

const STATUS: Record<AiCallStatus, { icon: string; tone: string }> = {
  Succeeded: { icon: '✓', tone: 'good' },
  Failed: { icon: '✕', tone: 'bad' },
  Cancelled: { icon: '■', tone: 'warn' },
}

export function UsagePage() {
  const [days, setDays] = useState<number>(30)
  const [report, setReport] = useState<UsageReport>()
  const [metric, setMetric] = useState<Metric>()
  const [error, setError] = useState<string>()

  useEffect(() => {
    setError(undefined)
    api.usage(days).then(setReport, (err: Error) => setError(err.message))
  }, [days])

  if (error) return <p className="error">{error}</p>
  if (!report) return <p className="muted">Loading usage…</p>

  const { totals } = report
  // Until anything costs money (e.g. on the mock provider), calls are the more useful view.
  const shown: Metric = metric ?? (totals.costUsd > 0 ? 'cost' : 'calls')
  const chartPoints = report.byDay.map((d) => ({
    date: d.date,
    value: shown === 'cost' ? d.costUsd : shown === 'calls' ? d.calls : d.tokens,
    detail: `${formatCount(d.calls)} ${d.calls === 1 ? 'call' : 'calls'} · ${formatCount(d.tokens)} tokens · ${formatUsd(d.costUsd)}`,
  }))
  const chartFormat = shown === 'cost' ? formatUsd : formatCount

  return (
    <div className="usage">
      <div className="usage-header">
        <div>
          <h2>AI usage</h2>
          <p className="muted small">
            Estimated from token counts and the prices in <code>Ai:Pricing</code>. Your provider’s invoice is the source of truth.
          </p>
        </div>
        <div className="segmented" role="group" aria-label="Time range">
          {RANGES.map((r) => (
            <button key={r} className={r === days ? 'active' : ''} onClick={() => setDays(r)}>
              {r} days
            </button>
          ))}
        </div>
      </div>

      <div className="kpis">
        <Kpi label="Estimated cost" value={formatUsd(totals.costUsd)} note={totals.unpricedCalls ? `${totals.unpricedCalls} calls not priced` : undefined} />
        <Kpi label="AI calls" value={formatCount(totals.calls)} note={totals.failedCalls ? `${totals.failedCalls} failed` : undefined} />
        <Kpi label="Input tokens" value={formatCount(totals.inputTokens)} />
        <Kpi label="Output tokens" value={formatCount(totals.outputTokens)} />
      </div>

      <section className="card">
        <div className="section-header">
          <h3 className="no-margin">{shown === 'cost' ? 'Estimated cost' : shown === 'calls' ? 'Calls' : 'Tokens'} per day</h3>
          <div className="segmented small" role="group" aria-label="Metric">
            {(['cost', 'calls', 'tokens'] as const).map((m) => (
              <button key={m} className={m === shown ? 'active' : ''} onClick={() => setMetric(m)}>
                {m[0].toUpperCase() + m.slice(1)}
              </button>
            ))}
          </div>
        </div>
        {totals.calls === 0 ? (
          <p className="muted">No AI calls in this period yet.</p>
        ) : (
          <DailyBarChart points={chartPoints} format={chartFormat} label={shown} />
        )}
      </section>

      <div className="usage-groups">
        <GroupTable title="By operation" rows={report.byOperation} labelOf={(g) => OPERATION_LABELS[g.label] ?? g.label} />
        <GroupTable title="By project" rows={report.byProject} />
        <GroupTable title="By model" rows={report.byModel} />
      </div>

      <section className="card">
        <h3>Recent calls</h3>
        {report.recent.length === 0 ? (
          <p className="muted">No calls yet.</p>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Time</th>
                  <th>Operation</th>
                  <th>Project</th>
                  <th>Model</th>
                  <th className="num">Tokens in / out</th>
                  <th className="num">Cost</th>
                  <th className="num">Duration</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {report.recent.map((r) => (
                  <tr key={r.id}>
                    <td>{new Date(r.createdAt).toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' })}</td>
                    <td>
                      {OPERATION_LABELS[r.operation] ?? r.operation}
                      {r.detail && <span className="muted"> · {r.detail}</span>}
                    </td>
                    <td>{r.projectName ?? <span className="muted">-</span>}</td>
                    <td>{r.model}</td>
                    <td className="num" title={r.estimated ? 'Estimated from text length' : undefined}>
                      {r.estimated && '~'}
                      {formatCount(r.inputTokens)} / {formatCount(r.outputTokens)}
                    </td>
                    <td className="num">{formatUsd(r.costUsd)}</td>
                    <td className="num">{(r.durationMs / 1000).toFixed(1)} s</td>
                    <td>
                      <span className={`pill ${STATUS[r.status].tone}`}>
                        {STATUS[r.status].icon} {r.status}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}

function Kpi({ label, value, note }: { label: string; value: string; note?: string }) {
  return (
    <div className="kpi card">
      <span className="kpi-label">{label}</span>
      <span className="kpi-value">{value}</span>
      {note && <span className="muted small">{note}</span>}
    </div>
  )
}

function GroupTable({ title, rows, labelOf }: { title: string; rows: UsageGroup[]; labelOf?: (g: UsageGroup) => string }) {
  return (
    <section className="card">
      <h3>{title}</h3>
      {rows.length === 0 ? (
        <p className="muted small">No calls yet.</p>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th>Name</th>
              <th className="num">Calls</th>
              <th className="num">Tokens</th>
              <th className="num">Cost</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((g) => (
              <tr key={g.key}>
                <td>{labelOf ? labelOf(g) : g.label}</td>
                <td className="num">{formatCount(g.calls)}</td>
                <td className="num">{formatCount(g.tokens)}</td>
                <td className="num">{formatUsd(g.costUsd)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}
