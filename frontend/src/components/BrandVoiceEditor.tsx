import { useState, type FormEvent } from 'react'
import { api, parseKeywords } from '../api'
import type { Project } from '../types'

/** Form for a project's brand guidelines, which are added to every AI prompt for that project. */
export function BrandVoiceEditor({ project, onSaved, onClose }: { project: Project; onSaved: (p: Project) => void; onClose: () => void }) {
  const brand = project.brandVoice
  const [voice, setVoice] = useState(brand?.voice ?? '')
  const [audience, setAudience] = useState(brand?.targetAudience ?? '')
  const [facts, setFacts] = useState(brand?.keyFacts ?? '')
  const [preferred, setPreferred] = useState(brand?.preferredTerms.join(', ') ?? '')
  const [avoid, setAvoid] = useState(brand?.avoidTerms.join(', ') ?? '')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string>()

  async function save(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(undefined)
    try {
      onSaved(
        await api.saveBrandVoice(project.id, {
          voice,
          targetAudience: audience,
          keyFacts: facts,
          preferredTerms: parseKeywords(preferred),
          avoidTerms: parseKeywords(avoid),
        }),
      )
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="brand-editor form" onSubmit={save}>
      <div>
        <h3>Brand voice</h3>
        <p className="muted small">
          Added to every AI prompt for this project: generating, variants and AI tools. Tone or audience chosen in a brief still
          wins.
        </p>
      </div>
      <label>
        Voice &amp; personality
        <textarea
          rows={2}
          value={voice}
          onChange={(e) => setVoice(e.target.value)}
          placeholder="e.g. Warm and witty, like a friendly barista. Short sentences. Never hypey."
        />
      </label>
      <label>
        Default audience
        <input value={audience} onChange={(e) => setAudience(e.target.value)} placeholder="e.g. Home coffee enthusiasts in Finland" />
      </label>
      <label>
        <span>
          Key facts <span className="muted">(one per line: the only product facts the AI may state)</span>
        </span>
        <textarea
          rows={4}
          value={facts}
          onChange={(e) => setFacts(e.target.value)}
          placeholder={'e.g. Roasted to order in Helsinki\nSubscription from €14.90/month'}
        />
      </label>
      <div className="row">
        <label>
          <span>
            Preferred terms <span className="muted">(comma-separated)</span>
          </span>
          <input value={preferred} onChange={(e) => setPreferred(e.target.value)} placeholder="freshly roasted, single-origin" />
        </label>
        <label>
          <span>
            Never use <span className="muted">(comma-separated)</span>
          </span>
          <input value={avoid} onChange={(e) => setAvoid(e.target.value)} placeholder="cheap, revolutionary" />
        </label>
      </div>
      <div className="save-bar">
        <div className="spacer" />
        <button type="button" className="ghost" onClick={onClose}>
          Cancel
        </button>
        <button className="primary" disabled={saving}>
          {saving ? 'Saving…' : 'Save brand voice'}
        </button>
      </div>
      {error && <p className="error">{error}</p>}
    </form>
  )
}
