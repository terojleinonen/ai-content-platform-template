import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { api, parseKeywords } from '../api'
import { AiTools, TRANSFORM_LABELS, type TransformOptions } from '../components/AiTools'
import { BrandCheckPanel } from '../components/BrandCheckPanel'
import { BrandVoiceEditor } from '../components/BrandVoiceEditor'
import { Markdown } from '../components/Markdown'
import { useContentStream } from '../hooks/useContentStream'
import { CONTENT_TYPES, CONTENT_TYPE_LABELS, type BrandCheck, type ContentItem, type ContentType, type Project, type TransformAction } from '../types'

export function ProjectsPage() {
  const [projects, setProjects] = useState<Project[]>([])
  const [selectedId, setSelectedId] = useState<string>()
  const [items, setItems] = useState<ContentItem[]>([])
  const [openItem, setOpenItem] = useState<ContentItem>()
  const [newName, setNewName] = useState('')
  const [editingBrand, setEditingBrand] = useState(false)
  const [error, setError] = useState<string>()

  const loadProjects = useCallback(async () => {
    try {
      const list = await api.listProjects()
      setProjects(list)
      setSelectedId((id) => (id && list.some((p) => p.id === id) ? id : list[0]?.id))
    } catch (err) {
      setError((err as Error).message)
    }
  }, [])

  const loadItems = useCallback(async (projectId: string) => {
    try {
      setItems(await api.listContent(projectId))
    } catch (err) {
      setError((err as Error).message)
    }
  }, [])

  useEffect(() => {
    loadProjects()
  }, [loadProjects])

  useEffect(() => {
    setOpenItem(undefined)
    setEditingBrand(false)
    if (selectedId) loadItems(selectedId)
    else setItems([])
  }, [selectedId, loadItems])

  async function createProject(e: FormEvent) {
    e.preventDefault()
    if (!newName.trim()) return
    try {
      const project = await api.createProject(newName.trim())
      setNewName('')
      await loadProjects()
      setSelectedId(project.id)
    } catch (err) {
      setError((err as Error).message)
    }
  }

  async function deleteProject(project: Project) {
    if (!confirm(`Delete “${project.name}” and all its content?`)) return
    await api.deleteProject(project.id)
    setSelectedId(undefined)
    await loadProjects()
  }

  const selected = projects.find((p) => p.id === selectedId)

  return (
    <div className="projects">
      <aside className="card sidebar">
        <h2>Projects</h2>
        <ul className="project-list">
          {projects.map((p) => (
            <li key={p.id}>
              <button className={p.id === selectedId ? 'active' : ''} onClick={() => setSelectedId(p.id)}>
                <span>{p.name}</span>
                <span className="count">{p.contentCount}</span>
              </button>
            </li>
          ))}
        </ul>
        <form className="new-project" onSubmit={createProject}>
          <input value={newName} onChange={(e) => setNewName(e.target.value)} placeholder="New project name" />
          <button className="primary" disabled={!newName.trim()}>
            Add
          </button>
        </form>
        {error && <p className="error">{error}</p>}
      </aside>

      <section className="card">
        {!selected ? (
          <div className="empty">
            <div className="empty-icon">📁</div>
            <p>Create a project to start organizing your content.</p>
          </div>
        ) : openItem ? (
          <ContentEditor
            item={openItem}
            onClose={() => setOpenItem(undefined)}
            onSaved={async (saved) => {
              setOpenItem(saved)
              await loadItems(selected.id)
            }}
            onDeleted={async () => {
              setOpenItem(undefined)
              await Promise.all([loadItems(selected.id), loadProjects()])
            }}
          />
        ) : (
          <>
            <div className="section-header">
              <div>
                <h2>
                  {selected.name}{' '}
                  {selected.brandVoice && (
                    <span className="pill good" title={selected.brandVoice.voice ?? undefined}>
                      Brand voice ✓
                    </span>
                  )}
                </h2>
                {selected.description && <p className="muted">{selected.description}</p>}
              </div>
              <div className="header-actions">
                <button className="ghost" onClick={() => setEditingBrand(!editingBrand)}>
                  {selected.brandVoice ? 'Edit brand voice' : 'Add brand voice'}
                </button>
                <button className="ghost danger" onClick={() => deleteProject(selected)}>
                  Delete project
                </button>
              </div>
            </div>
            {editingBrand && (
              <BrandVoiceEditor
                key={selected.id}
                project={selected}
                onClose={() => setEditingBrand(false)}
                onSaved={async () => {
                  setEditingBrand(false)
                  await loadProjects()
                }}
              />
            )}
            {items.length === 0 ? (
              <div className="empty">
                <p>No content yet.</p>
                <a href="#/write">Generate your first piece →</a>
              </div>
            ) : (
              <ul className="content-grid">
                {items.map((item) => (
                  <li key={item.id}>
                    <button className="content-card" onClick={() => setOpenItem(item)}>
                      <span className="pill neutral">{CONTENT_TYPE_LABELS[item.type]}</span>
                      <strong>{item.title}</strong>
                      <span className="muted small snippet">{item.body.replace(/[#*_]/g, '').slice(0, 140)}…</span>
                      <span className="muted small">{new Date(item.updatedAt ?? item.createdAt).toLocaleString()}</span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </>
        )}
      </section>
    </div>
  )
}

function ContentEditor({
  item,
  onClose,
  onSaved,
  onDeleted,
}: {
  item: ContentItem
  onClose: () => void
  onSaved: (item: ContentItem) => void
  onDeleted: () => void
}) {
  const [editing, setEditing] = useState(false)
  const [type, setType] = useState<ContentType>(item.type)
  const [title, setTitle] = useState(item.title)
  const [body, setBody] = useState(item.body)
  const [keywords, setKeywords] = useState(item.keywords.join(', '))
  const [error, setError] = useState<string>()
  const [notice, setNotice] = useState<string>()
  const [brandCheck, setBrandCheck] = useState<BrandCheck | null>()
  const [streamLabel, setStreamLabel] = useState('')
  const stream = useContentStream()

  function resetFields() {
    setType(item.type)
    setTitle(item.title)
    setBody(item.body)
    setKeywords(item.keywords.join(', '))
  }

  function cancel() {
    resetFields()
    setEditing(false)
    setNotice(undefined)
    setBrandCheck(undefined)
    setError(undefined)
  }

  async function runTool(action: TransformAction, options?: TransformOptions) {
    setError(undefined)
    setNotice(undefined)
    setStreamLabel(`${TRANSFORM_LABELS[action]}…`)
    setEditing(true)

    const outcome = await stream.run((onDelta, signal) =>
      api.transformContentStream(
        { action, title, body, type, keywords: parseKeywords(keywords), projectId: item.projectId, ...options },
        onDelta,
        signal,
      ),
    )
    if (outcome?.kind === 'done') {
      setTitle(outcome.result.title)
      setBody(outcome.result.body)
      setBrandCheck(outcome.result.brandCheck)
      setNotice('AI edit applied. Review it, then Save, or Cancel to discard.')
    } else if (outcome?.kind === 'stopped') {
      setNotice('Edit stopped. The text is unchanged.')
    } else if (outcome?.kind === 'error') {
      setError(outcome.message)
    }
  }

  async function save() {
    try {
      const saved = await api.updateContent(item.id, {
        type,
        title,
        body,
        targetAudience: item.targetAudience,
        toneOfVoice: item.toneOfVoice,
        keywords: parseKeywords(keywords),
      })
      setEditing(false)
      setNotice(undefined)
      onSaved(saved)
    } catch (err) {
      setError((err as Error).message)
    }
  }

  async function remove() {
    if (!confirm(`Delete “${item.title}”?`)) return
    await api.deleteContent(item.id)
    onDeleted()
  }

  return (
    <div>
      <div className="result-toolbar">
        <button className="ghost" onClick={onClose}>
          ← Back
        </button>
        <div className="spacer" />
        {stream.running ? (
          <>
            <span className="pill neutral">{streamLabel}</span>
            <button className="ghost" onClick={stream.stop}>
              ■ Stop
            </button>
          </>
        ) : editing ? (
          <>
            <button className="ghost" onClick={cancel}>
              Cancel
            </button>
            <button className="primary" onClick={save}>
              Save
            </button>
          </>
        ) : (
          <>
            <button className="ghost" onClick={() => setEditing(true)}>
              Edit
            </button>
            <button className="ghost danger" onClick={remove}>
              Delete
            </button>
          </>
        )}
      </div>

      {editing ? (
        <div className="form">
          <div className="row">
            <label>
              Type
              <select value={type} onChange={(e) => setType(e.target.value as ContentType)}>
                {CONTENT_TYPES.map((t) => (
                  <option key={t} value={t}>
                    {CONTENT_TYPE_LABELS[t]}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Keywords
              <input value={keywords} onChange={(e) => setKeywords(e.target.value)} />
            </label>
          </div>
          <input
            className="title-input"
            value={stream.live ? stream.live.title : title}
            onChange={(e) => setTitle(e.target.value)}
            readOnly={stream.running}
          />
          <textarea rows={18} value={stream.live ? stream.live.body : body} onChange={(e) => setBody(e.target.value)} readOnly={stream.running} />
          <AiTools disabled={stream.running} onRun={runTool} />
          {notice && <p className="notice">{notice}</p>}
          {brandCheck && <BrandCheckPanel check={brandCheck} />}
          {error && <p className="error">{error}</p>}
        </div>
      ) : (
        <article>
          <div className="meta">
            <span className="pill neutral">{CONTENT_TYPE_LABELS[item.type]}</span>
            {item.toneOfVoice && <span className="pill neutral">{item.toneOfVoice}</span>}
            {item.keywords.map((k) => (
              <span key={k} className="pill outline">
                {k}
              </span>
            ))}
          </div>
          <h1 className="result-title">{item.title}</h1>
          <Markdown text={item.body} />
          <AiTools onRun={runTool} />
        </article>
      )}
    </div>
  )
}
