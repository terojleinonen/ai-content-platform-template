import { useCallback, useEffect, useRef, useState, type MouseEvent } from 'react'
import type { OnDelta } from '../api'
import type { GenerateContentResponse } from '../types'

export type StreamOutcome =
  | { kind: 'done'; result: GenerateContentResponse }
  | { kind: 'stopped'; partial: { title: string; body: string } }
  | { kind: 'error'; message: string }

/** Splits streamed Markdown into the "# Title" first line and the body, like the API does. */
export function splitTitle(markdown: string) {
  const text = markdown.trimStart()
  if (!text.startsWith('# ')) return { title: '', body: text }
  const newline = text.indexOf('\n')
  return newline < 0
    ? { title: text.slice(2).trim(), body: '' }
    : { title: text.slice(2, newline).trim(), body: text.slice(newline + 1).trim() }
}

/**
 * Runs one streaming generation or edit at a time, exposing the live text and a stop function.
 * In-flight requests are cancelled when the component unmounts.
 */
export function useContentStream() {
  const [text, setText] = useState<string>()
  const abortRef = useRef<AbortController | null>(null)

  useEffect(() => () => abortRef.current?.abort(), [])

  const run = useCallback(
    async (start: (onDelta: OnDelta, signal: AbortSignal) => Promise<GenerateContentResponse>): Promise<StreamOutcome | undefined> => {
      if (abortRef.current) return undefined

      const controller = new AbortController()
      abortRef.current = controller
      let received = ''
      setText('')
      try {
        const result = await start((chunk) => {
          received += chunk
          setText(received)
        }, controller.signal)
        return { kind: 'done', result }
      } catch (err) {
        return controller.signal.aborted
          ? { kind: 'stopped', partial: splitTitle(received) }
          : { kind: 'error', message: (err as Error).message }
      } finally {
        abortRef.current = null
        setText(undefined)
      }
    },
    [],
  )

  const stop = useCallback((e?: MouseEvent) => {
    // A Stop button can be re-rendered as a submit button before its click finishes;
    // preventDefault keeps that same click from submitting the form.
    e?.preventDefault()
    abortRef.current?.abort()
  }, [])

  return {
    running: text !== undefined,
    live: text !== undefined ? splitTitle(text) : undefined,
    run,
    stop,
  }
}
