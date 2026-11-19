import { createRuntimeRegistry, defaultRuntimes, type Runtime } from '@botiverse/oar';
import { scriptedRuntime } from '@botiverse/oar/testing';

/**
 * Build the Worker registry: OAR default runtimes plus an in-process `scripted`
 * runtime used **only** for P0 smoke / host protocol tests (no binary, no login).
 *
 * Product UX (`api/agent-hub/runtimes` consumer projection) **omits** `scripted`.
 * Do not present scripted as a normal user Agent/Runtime.
 */
export function buildRegistry(): ReturnType<typeof createRuntimeRegistry> {
  const scripted = scriptedRuntime({
    id: 'scripted',
    model: 'scripted-1',
    turn: async (turn) => {
      turn.say(`echo:${turn.input}`);
      // Stay alive briefly so abort-during-turn can be exercised by smoke.
      if (turn.input.startsWith('hold:')) {
        await new Promise<void>((resolve) => {
          if (turn.signal.aborted) {
            resolve();
            return;
          }
          const done = () => {
            turn.signal.removeEventListener('abort', done);
            resolve();
          };
          turn.signal.addEventListener('abort', done);
          const ms = Number(turn.input.slice('hold:'.length)) || 5000;
          setTimeout(done, ms);
        });
      }
    },
  });

  return createRuntimeRegistry([...defaultRuntimes.list(), scripted]);
}

export type RuntimeRegistry = ReturnType<typeof buildRegistry>;

export function runtimeSummary(rt: Runtime): Record<string, unknown> {
  // Omit brand.icon / icons (data: SVG) — too large for stdio list frames.
  return {
    id: rt.id,
    brand: { name: rt.brand?.name ?? rt.id },
    execution: rt.execution,
    placement: rt.placement,
    hasInstallation: typeof rt.installation === 'function',
    hasInstall: typeof rt.install === 'function',
    hasAuthStatus: typeof rt.authStatus === 'function',
    hasListModels: typeof rt.listModels === 'function',
    hasAccountUsage: typeof rt.accountUsage === 'function',
    refusedSessionOptions: rt.refusedSessionOptions ?? null,
  };
}
