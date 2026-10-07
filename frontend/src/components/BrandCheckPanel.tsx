import { originalsOf } from '../api'
import type { BrandCheck } from '../types'

/** Shows avoided terms that slipped into the text and which preferred terms were used. */
export function BrandCheckPanel({ check, translations }: { check: BrandCheck; translations?: Record<string, string> | null }) {
  const clean = check.avoidTermsFound.length === 0
  const originals = originalsOf(translations)
  const label = (term: string) => {
    const original = originals.get(term.toLowerCase())
    return original ? `${term} (${original})` : term
  }

  return (
    <div className="seo">
      <div className="seo-header">
        <span className="label">BRAND CHECK</span>
        <span className={`pill ${clean ? 'good' : 'bad'}`}>{clean ? '✓ On brand' : `${check.avoidTermsFound.length} avoided term(s) used`}</span>
      </div>
      <p className="muted small">{check.projectName}</p>
      <div className="brand-terms">
        {check.avoidTermsFound.map((t) => (
          <span key={t} className="pill bad" title="On the project's never-use list">
            ✕ {label(t)}
          </span>
        ))}
        {check.preferredTermsUsed.map((t) => (
          <span key={t} className="pill good" title="Preferred term used">
            ✓ {label(t)}
          </span>
        ))}
        {check.preferredTermsMissing.map((t) => (
          <span key={t} className="pill outline" title="Preferred term not used">
            {label(t)}
          </span>
        ))}
      </div>
    </div>
  )
}
