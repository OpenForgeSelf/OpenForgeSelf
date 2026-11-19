/**
 * P0 probe: 12 steps over the in-process Worker dispatch surface.
 * Uses `scripted` runtime (no third-party binary). Vendor runtimes are
 * only listed + installation-probed (read-only; never auto-install).
 *
 * Exit 0 when all required steps pass; prints a JSON summary to stdout.
 */
import { createWorkerContext, dispatch } from './handlers.js';

interface StepResult {
  step: number;
  name: string;
  ok: boolean;
  detail?: unknown;
  error?: string;
}

const notifications: Array<{ method: string; params: unknown }> = [];

async function call(method: string, params?: unknown): Promise<unknown> {
  return dispatch(ctx, method, params);
}

const ctx = createWorkerContext((method, params) => {
  notifications.push({ method, params });
});

const results: StepResult[] = [];

async function step(n: number, name: string, fn: () => Promise<unknown>): Promise<unknown> {
  try {
    const detail = await fn();
    results.push({ step: n, name, ok: true, detail });
    return detail;
  } catch (e) {
    const error = e instanceof Error ? e.message : String(e);
    results.push({ step: n, name, ok: false, error });
    throw e;
  }
}

async function main(): Promise<void> {
  // 1 hello
  await step(1, 'worker.hello', async () => call('worker.hello'));

  // 2 runtime.list
  const listed = (await step(2, 'runtime.list', async () => call('runtime.list'))) as {
    runtimes: Array<{ id: string }>;
  };
  const ids = listed.runtimes.map((r) => r.id);
  if (!ids.includes('scripted')) throw new Error('scripted runtime missing from list');
  if (!ids.includes('claude') || !ids.includes('codex') || !ids.includes('opencode')) {
    throw new Error('expected first-batch vendor ids in list');
  }

  // 3 installation (scripted bundled)
  await step(3, 'runtime.installation(scripted)', async () => {
    const r = (await call('runtime.installation', { runtimeId: 'scripted' })) as {
      snapshot: { kind: string; via?: string };
    };
    if (r.snapshot.kind !== 'available') throw new Error('scripted not available');
    return r;
  });

  // 4 auth (scripted → not_supported is OK)
  await step(4, 'runtime.auth(scripted)', async () => call('runtime.auth', { runtimeId: 'scripted' }));

  // 5 models (scripted → not_supported is OK)
  await step(5, 'runtime.models(scripted)', async () =>
    call('runtime.models', { runtimeId: 'scripted' }),
  );

  // 6 session.open
  const opened = (await step(6, 'session.open', async () =>
    call('session.open', { runtimeId: 'scripted', cwd: process.cwd() }),
  )) as { sessionKey: string; oarSessionId: string };

  // 7 session.prompt + events notification
  notifications.length = 0;
  await step(7, 'session.prompt', async () => {
    const r = await call('session.prompt', {
      sessionKey: opened.sessionKey,
      input: 'ping',
    });
    // allow event flush
    await new Promise((r) => setTimeout(r, 30));
    const ev = notifications.filter((n) => n.method === 'session.event');
    if (ev.length < 1) throw new Error('expected session.event notifications');
    return { prompt: r, eventCount: ev.length };
  });

  // 8 session.records
  await step(8, 'session.records', async () => {
    const r = (await call('session.records', { sessionKey: opened.sessionKey })) as {
      records: unknown[];
    };
    if (!Array.isArray(r.records) || r.records.length < 1) {
      throw new Error('expected non-empty records');
    }
    return { count: r.records.length };
  });

  // 9 session.resume
  const resumed = (await step(9, 'session.resume', async () =>
    call('session.resume', {
      runtimeId: 'scripted',
      cwd: process.cwd(),
      oarSessionId: opened.oarSessionId,
      previousSessionKey: opened.sessionKey,
    }),
  )) as { sessionKey: string; generation: number };
  if (resumed.generation < 2) throw new Error('expected generation bump on resume');

  // 10 cancel/abort during hold turn
  await step(10, 'session.control(cancel)', async () => {
    const promptPromise = call('session.prompt', {
      sessionKey: resumed.sessionKey,
      input: 'hold:2000',
    });
    await new Promise((r) => setTimeout(r, 40));
    const control = await call('session.control', {
      sessionKey: resumed.sessionKey,
      action: 'cancel',
    });
    await promptPromise;
    return control;
  });

  // 11 session.dispose
  await step(11, 'session.dispose', async () =>
    call('session.dispose', { sessionKey: resumed.sessionKey }),
  );

  // 12 vendor read-only installation probes (never install)
  await step(12, 'vendor.installation(read-only)', async () => {
    const vendors = ['claude', 'codex', 'opencode'] as const;
    const snaps: Record<string, unknown> = {};
    for (const id of vendors) {
      snaps[id] = await call('runtime.installation', { runtimeId: id });
    }
    return snaps;
  });

  const failed = results.filter((r) => !r.ok);
  const summary = {
    ok: failed.length === 0,
    passed: results.filter((r) => r.ok).length,
    total: results.length,
    results: results.map(({ step: s, name, ok, error, detail }) => ({
      step: s,
      name,
      ok,
      error: error ?? null,
      detail: ok ? summarizeDetail(name, detail) : null,
    })),
  };
  process.stdout.write(`${JSON.stringify(summary, null, 2)}\n`);
  if (!summary.ok) process.exit(1);
}

function summarizeDetail(name: string, detail: unknown): unknown {
  if (name === 'runtime.list') {
    const d = detail as { runtimes: Array<{ id: string }> };
    return { count: d.runtimes.length, ids: d.runtimes.map((r) => r.id) };
  }
  if (name.startsWith('session.open') || name.startsWith('session.resume')) {
    const d = detail as Record<string, unknown>;
    return {
      sessionKey: d.sessionKey,
      oarSessionId: d.oarSessionId,
      generation: d.generation,
    };
  }
  if (name === 'vendor.installation(read-only)') {
    const d = detail as Record<string, { snapshot?: { kind?: string } }>;
    return Object.fromEntries(
      Object.entries(d).map(([k, v]) => [k, v?.snapshot?.kind ?? v]),
    );
  }
  return detail;
}

main().catch((e) => {
  process.stderr.write(`${e instanceof Error ? e.stack : e}\n`);
  process.stdout.write(
    `${JSON.stringify({ ok: false, passed: results.filter((r) => r.ok).length, total: 12, results }, null, 2)}\n`,
  );
  process.exit(1);
});
