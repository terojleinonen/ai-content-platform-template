import { originalsOf } from '../api'
import type { KeywordInsight, SeoReport } from '../types'

const TONES: Record<KeywordInsight['rating'], string> = {
  missing: 'bad',
  low: 'warn',
  good: 'good',
  'too high': 'warn',
  'too many': 'warn',
}

export function SeoPanel({ report, translations }: { report: SeoReport; translations?: Record<string, string> | null }) {
  const originals = originalsOf(translations)
  const byMentions = report.mode === 'Mentions'

  // Bar shows how far into (or past) the target each keyword is.
  const fill = (k: KeywordInsight) => (byMentions ? Math.min(k.occurrences / 3, 1) : Math.min(k.density / 0.05, 1))

  return (
    <div className="seo">
      <div className="seo-header">
        <span className="label">SEO</span>
        <span className="muted">{report.wordCount} words</span>
      </div>
      {report.keywords.length === 0 ? (
        <p className="muted small">Add keywords to see how well they're used.</p>
      ) : (
        <ul className="seo-list">
          {report.keywords.map((k) => (
            <li key={k.keyword}>
              <div className="seo-row">
                <span>
                  {k.keyword}
                  {originals.has(k.keyword.toLowerCase()) && <span className="muted small"> ({originals.get(k.keyword.toLowerCase())})</span>}
                </span>
                <span className={`pill ${TONES[k.rating]}`}>
                  {byMentions ? `${k.occurrences}×` : `${(k.density * 100).toFixed(1)}%`} · {k.rating}
                </span>
              </div>
              <div className="bar">
                <div className={`bar-fill ${TONES[k.rating]}`} style={{ width: `${fill(k) * 100}%` }} />
              </div>
            </li>
          ))}
        </ul>
      )}
      <p className="muted small">{report.target} Inflected forms count (e.g. blogi → blogia).</p>
    </div>
  )
}
