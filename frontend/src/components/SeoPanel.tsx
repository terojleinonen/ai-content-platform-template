const LOW = 0.005
const HIGH = 0.03

function rate(density: number) {
  if (density === 0) return { label: 'missing', tone: 'bad' }
  if (density < LOW) return { label: 'low', tone: 'warn' }
  if (density <= HIGH) return { label: 'good', tone: 'good' }
  return { label: 'too high', tone: 'warn' }
}

export function SeoPanel({ wordCount, scores }: { wordCount: number; scores?: Record<string, number> }) {
  const entries = Object.entries(scores ?? {})

  return (
    <div className="seo">
      <div className="seo-header">
        <span className="label">SEO</span>
        <span className="muted">{wordCount} words</span>
      </div>
      {entries.length === 0 ? (
        <p className="muted small">Add keywords to see keyword density.</p>
      ) : (
        <ul className="seo-list">
          {entries.map(([keyword, density]) => {
            const r = rate(density)
            return (
              <li key={keyword}>
                <div className="seo-row">
                  <span>{keyword}</span>
                  <span className={`pill ${r.tone}`}>
                    {(density * 100).toFixed(1)}% · {r.label}
                  </span>
                </div>
                <div className="bar">
                  <div className={`bar-fill ${r.tone}`} style={{ width: `${Math.min(density / 0.05, 1) * 100}%` }} />
                </div>
              </li>
            )
          })}
        </ul>
      )}
      <p className="muted small">Target density: 0.5% – 3% per keyword.</p>
    </div>
  )
}
