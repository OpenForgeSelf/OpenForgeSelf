import type { Session, Unsubscribe } from '@botiverse/oar';
import { randomUUID } from 'node:crypto';

export interface TrackedSession {
  /** Host-facing SessionKey (GUID). */
  readonly key: string;
  readonly runtimeId: string;
  readonly oarSessionId: string;
  readonly session: Session;
  generation: number;
  unsubEvents: Unsubscribe | null;
}

export class SessionTable {
  readonly #byKey = new Map<string, TrackedSession>();

  add(runtimeId: string, session: Session, generation = 1): TrackedSession {
    const key = randomUUID();
    const tracked: TrackedSession = {
      key,
      runtimeId,
      oarSessionId: session.id,
      session,
      generation,
      unsubEvents: null,
    };
    this.#byKey.set(key, tracked);
    return tracked;
  }

  get(key: string): TrackedSession | undefined {
    return this.#byKey.get(key);
  }

  require(key: string): TrackedSession {
    const t = this.#byKey.get(key);
    if (!t) {
      throw Object.assign(new Error(`session not found: ${key}`), { code: -32001 });
    }
    return t;
  }

  delete(key: string): void {
    const t = this.#byKey.get(key);
    if (t?.unsubEvents) {
      try {
        t.unsubEvents();
      } catch {
        /* ignore */
      }
    }
    this.#byKey.delete(key);
  }

  list(): Array<{ key: string; runtimeId: string; oarSessionId: string; generation: number }> {
    return [...this.#byKey.values()].map((t) => ({
      key: t.key,
      runtimeId: t.runtimeId,
      oarSessionId: t.oarSessionId,
      generation: t.generation,
    }));
  }

  async disposeAll(): Promise<void> {
    const keys = [...this.#byKey.keys()];
    for (const key of keys) {
      const t = this.#byKey.get(key);
      if (!t) continue;
      try {
        await t.session.dispose();
      } catch {
        /* ignore */
      }
      this.delete(key);
    }
  }
}
