import { Fragment, type ReactNode } from 'react'

/**
 * Minimal, safe Markdown renderer (headings, lists, bold, italic, paragraphs).
 * Builds React elements instead of injecting HTML, so model output can't inject markup.
 */
export function Markdown({ text }: { text: string }) {
  const blocks: ReactNode[] = []
  let list: string[] = []
  let paragraph: string[] = []

  const flushList = () => {
    if (list.length) {
      blocks.push(
        <ul key={blocks.length}>
          {list.map((item, i) => (
            <li key={i}>{inline(item)}</li>
          ))}
        </ul>,
      )
      list = []
    }
  }
  const flushParagraph = () => {
    if (paragraph.length) {
      blocks.push(<p key={blocks.length}>{inline(paragraph.join(' '))}</p>)
      paragraph = []
    }
  }

  for (const raw of text.split('\n')) {
    const line = raw.trim()
    const heading = /^(#{1,4})\s+(.*)$/.exec(line)
    const bullet = /^[-*]\s+(.*)$/.exec(line)

    if (!line) {
      flushList()
      flushParagraph()
    } else if (heading) {
      flushList()
      flushParagraph()
      const level = Math.min(heading[1].length + 1, 5)
      const Tag = `h${level}` as 'h2' | 'h3' | 'h4' | 'h5'
      blocks.push(<Tag key={blocks.length}>{inline(heading[2])}</Tag>)
    } else if (bullet) {
      flushParagraph()
      list.push(bullet[1])
    } else {
      flushList()
      paragraph.push(line)
    }
  }
  flushList()
  flushParagraph()

  return <div className="markdown">{blocks}</div>
}

function inline(text: string): ReactNode {
  // Split on **bold** and _italic_/*italic* spans.
  const parts = text.split(/(\*\*[^*]+\*\*|_[^_]+_|\*[^*]+\*)/g)
  return parts.map((part, i) => {
    if (/^\*\*[^*]+\*\*$/.test(part)) return <strong key={i}>{part.slice(2, -2)}</strong>
    if (/^(_[^_]+_|\*[^*]+\*)$/.test(part)) return <em key={i}>{part.slice(1, -1)}</em>
    return <Fragment key={i}>{part}</Fragment>
  })
}
