import { useState, type FormEvent } from 'react'
import { api } from '../api'
import type { GenerateImageResponse } from '../types'

const STYLES = ['', 'Photorealistic', 'Watercolor', 'Flat illustration', '3D render', 'Minimalist']
const SIZES = {
  Square: { width: 1024, height: 1024 },
  Landscape: { width: 1536, height: 1024 },
  Portrait: { width: 1024, height: 1536 },
} as const
type SizeName = keyof typeof SIZES

const extensionOf = (url: string) => {
  const mime = /^data:image\/([a-z]+)/.exec(url)?.[1]
  return mime === 'svg' ? 'svg' : (mime ?? 'png')
}

export function ImagesPage() {
  const [prompt, setPrompt] = useState('')
  const [style, setStyle] = useState('')
  const [size, setSize] = useState<SizeName>('Square')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string>()
  const [images, setImages] = useState<GenerateImageResponse[]>([])

  async function generate(e: FormEvent) {
    e.preventDefault()
    setLoading(true)
    setError(undefined)
    try {
      const image = await api.generateImage({ prompt, style: style || undefined, ...SIZES[size] })
      setImages((prev) => [image, ...prev])
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="split">
      <form className="card form" onSubmit={generate}>
        <h2>Image brief</h2>
        <label>
          Describe the image *
          <textarea
            rows={4}
            value={prompt}
            onChange={(e) => setPrompt(e.target.value)}
            placeholder="e.g. A cozy bakery storefront at sunrise"
            required
          />
        </label>
        <div className="row">
          <label>
            Style
            <select value={style} onChange={(e) => setStyle(e.target.value)}>
              {STYLES.map((s) => (
                <option key={s} value={s}>
                  {s || 'Any'}
                </option>
              ))}
            </select>
          </label>
          <label>
            Format
            <select value={size} onChange={(e) => setSize(e.target.value as SizeName)}>
              {Object.keys(SIZES).map((s) => (
                <option key={s}>{s}</option>
              ))}
            </select>
          </label>
        </div>
        <button className="primary" disabled={loading || !prompt.trim()}>
          {loading ? <span className="spinner" /> : '🎨'} {loading ? 'Generating…' : 'Generate image'}
        </button>
        {error && <p className="error">{error}</p>}
      </form>

      <section className="card">
        {images.length === 0 ? (
          <div className="empty">
            <div className="empty-icon">🖼️</div>
            <p>Your generated images will appear here.</p>
          </div>
        ) : (
          <ul className="gallery">
            {images.map((img, i) => (
              <li key={images.length - i}>
                <img src={img.url} alt={img.promptUsed ?? 'Generated image'} />
                <div className="gallery-caption">
                  <span className="small">{img.promptUsed}</span>
                  <a className="small" href={img.url} download={`image-${images.length - i}.${extensionOf(img.url)}`}>
                    Download
                  </a>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  )
}
