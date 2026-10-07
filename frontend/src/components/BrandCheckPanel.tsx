import type { BrandCheck } from '../types'

/** Shows avoided terms that slipped into the text and which preferred terms were used. */
export function BrandCheckPanel({ check }: { check: BrandCheck }) {
  const clean = check.avoidTermsFound.length === 0

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
            ✕ {t}
          </span>
        ))}
        {check.preferredTermsUsed.map((t) => (
          <span key={t} className="pill good" title="Preferred term used">
            ✓ {t}
          </span>
        ))}
        {check.preferredTermsMissing.map((t) => (
          <span key={t} className="pill outline" title="Preferred term not used">
            {t}
          </span>
        ))}
      </div>
    </div>
  )
}
