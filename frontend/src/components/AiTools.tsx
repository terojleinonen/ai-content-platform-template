import { useState, type FormEvent } from 'react'
import { LANGUAGES, TONES, type TransformAction } from '../types'

export interface TransformOptions {
  toneOfVoice?: string
  language?: string
  instruction?: string
}

export const TRANSFORM_LABELS: Record<TransformAction, string> = {
  Improve: 'Improving',
  Shorten: 'Shortening',
  Expand: 'Expanding',
  ChangeTone: 'Changing tone',
  Translate: 'Translating',
  Custom: 'Editing',
}

/** Toolbar of AI editing actions for an existing piece of content. */
export function AiTools({ disabled, onRun }: { disabled?: boolean; onRun: (action: TransformAction, options?: TransformOptions) => void }) {
  const [customOpen, setCustomOpen] = useState(false)
  const [instruction, setInstruction] = useState('')

  function applyCustom(e: FormEvent) {
    e.preventDefault()
    if (!instruction.trim()) return
    onRun('Custom', { instruction: instruction.trim() })
  }

  return (
    <div className="ai-tools">
      <div className="ai-tools-row">
        <span className="label">AI tools</span>
        <button type="button" className="chip" disabled={disabled} onClick={() => onRun('Improve')}>
          ✨ Improve
        </button>
        <button type="button" className="chip" disabled={disabled} onClick={() => onRun('Shorten')}>
          Shorten
        </button>
        <button type="button" className="chip" disabled={disabled} onClick={() => onRun('Expand')}>
          Expand
        </button>
        <select
          className="chip-select"
          value=""
          disabled={disabled}
          aria-label="Change tone"
          onChange={(e) => e.target.value && onRun('ChangeTone', { toneOfVoice: e.target.value })}
        >
          <option value="">Tone…</option>
          {TONES.map((t) => (
            <option key={t}>{t}</option>
          ))}
        </select>
        <select
          className="chip-select"
          value=""
          disabled={disabled}
          aria-label="Translate"
          onChange={(e) => e.target.value && onRun('Translate', { language: e.target.value })}
        >
          <option value="">Translate…</option>
          {LANGUAGES.map((l) => (
            <option key={l}>{l}</option>
          ))}
        </select>
        <button type="button" className={`chip ${customOpen ? 'active' : ''}`} disabled={disabled} onClick={() => setCustomOpen(!customOpen)}>
          Custom…
        </button>
      </div>
      {customOpen && (
        <form className="ai-tools-custom" onSubmit={applyCustom}>
          <input
            value={instruction}
            onChange={(e) => setInstruction(e.target.value)}
            placeholder="e.g. Add a call to action at the end"
            disabled={disabled}
            autoFocus
          />
          <button className="primary" disabled={disabled || !instruction.trim()}>
            Apply
          </button>
        </form>
      )}
    </div>
  )
}
