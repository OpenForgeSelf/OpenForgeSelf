import { agentAuthHeaders, eventsUrl } from './api'
import type { AgentEventEnvelope } from './types'

export interface SubscribeOptions {
  sessionId: string
  after?: number
  onEvent: (ev: AgentEventEnvelope) => void
  onError?: (err: Error) => void
}

export function parseSseChunk(buffer: string): { events: AgentEventEnvelope[]; rest: string } {
  const events: AgentEventEnvelope[] = []
  let rest = buffer
  let sep: number
  while ((sep = rest.indexOf('\n\n')) >= 0) {
    const raw = rest.slice(0, sep)
    rest = rest.slice(sep + 2)
    const dataLines = raw
      .split('\n')
      .filter((l) => l.startsWith('data:'))
      .map((l) => l.slice(5).trimStart())
    if (!dataLines.length) continue
    try {
      const ev = JSON.parse(dataLines.join('\n')) as AgentEventEnvelope
      if (ev && ev.type) events.push(ev)
    } catch {
      /* ignore malformed frame */
    }
  }
  return { events, rest }
}

export function subscribeSessionEvents(opts: SubscribeOptions): () => void {
  const ac = new AbortController()
  let after = opts.after ?? 0
  let stopped = false
  let delay = 500

  const loop = async () => {
    while (!stopped) {
      try {
        const res = await fetch(eventsUrl(opts.sessionId, after), {
          headers: { ...agentAuthHeaders(), Accept: 'text/event-stream' },
          signal: ac.signal,
        })
        if (!res.ok || !res.body) throw new Error(`events ${res.status}`)
        delay = 500
        const reader = res.body.getReader()
        const decoder = new TextDecoder()
        let buf = ''
        while (!stopped) {
          const { value, done } = await reader.read()
          if (done) break
          buf += decoder.decode(value, { stream: true })
          const parsed = parseSseChunk(buf)
          buf = parsed.rest
          for (const ev of parsed.events) {
            if (ev.seq > after) after = ev.seq
            opts.onEvent(ev)
          }
        }
      } catch (e) {
        if (stopped || ac.signal.aborted) return
        opts.onError?.(e instanceof Error ? e : new Error(String(e)))
        await new Promise((r) => setTimeout(r, delay))
        delay = Math.min(delay * 2, 8000)
      }
    }
  }
  void loop()
  return () => {
    stopped = true
    ac.abort()
  }
}
