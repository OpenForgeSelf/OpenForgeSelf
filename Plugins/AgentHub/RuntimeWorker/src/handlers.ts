import type { AvailableInstallation, Runtime, SessionOptions } from '@botiverse/oar';
import { PROTOCOL_VERSION } from './protocol.js';
import { buildRegistry, runtimeSummary, type RuntimeRegistry } from './registry.js';
import { controlOutcomeSafe, jsonSafe } from './serialize.js';
import { SessionTable, type TrackedSession } from './sessions.js';

export type NotifyFn = (method: string, params: unknown) => void;

export interface WorkerContext {
  registry: RuntimeRegistry;
  sessions: SessionTable;
  notify: NotifyFn;
  startedAt: number;
}

export function createWorkerContext(notify: NotifyFn): WorkerContext {
  return {
    registry: buildRegistry(),
    sessions: new SessionTable(),
    notify,
    startedAt: Date.now(),
  };
}

function requireRuntime(ctx: WorkerContext, runtimeId: string): Runtime {
  const rt = ctx.registry.get(runtimeId);
  if (!rt) {
    throw Object.assign(new Error(`unknown runtime: ${runtimeId}`), { code: -32002 });
  }
  return rt;
}

async function requireAvailable(
  rt: Runtime,
  env?: SessionOptions['env'],
): Promise<AvailableInstallation> {
  if (!rt.installation) {
    // Bundled / scripted always exposes installation; treat missing probe as bundled.
    return { kind: 'available', via: 'bundled' };
  }
  const snap = await rt.installation(env ? { env } : undefined);
  if (snap.kind !== 'available') {
    throw Object.assign(
      new Error(
        snap.kind === 'not_found'
          ? `runtime ${rt.id} not installed`
          : `runtime ${rt.id} unsupported: ${snap.reason}`,
      ),
      { code: -32003, data: snap },
    );
  }
  return snap;
}

function attachEvents(ctx: WorkerContext, tracked: TrackedSession): void {
  if (tracked.unsubEvents) {
    tracked.unsubEvents();
  }
  tracked.unsubEvents = tracked.session.events((event) => {
    ctx.notify('session.event', {
      sessionKey: tracked.key,
      generation: tracked.generation,
      oarSessionId: tracked.oarSessionId,
      event: jsonSafe(event),
    });
  });
}

type Params = Record<string, unknown> | undefined;

function asRecord(params: unknown): Params {
  if (params == null) return undefined;
  if (typeof params !== 'object' || Array.isArray(params)) {
    throw Object.assign(new Error('params must be an object'), { code: -32602 });
  }
  return params as Record<string, unknown>;
}

function str(params: Params, key: string, required = true): string | undefined {
  const v = params?.[key];
  if (v == null || v === '') {
    if (required) {
      throw Object.assign(new Error(`missing params.${key}`), { code: -32602 });
    }
    return undefined;
  }
  if (typeof v !== 'string') {
    throw Object.assign(new Error(`params.${key} must be a string`), { code: -32602 });
  }
  return v;
}

export async function dispatch(
  ctx: WorkerContext,
  method: string,
  rawParams: unknown,
): Promise<unknown> {
  const params = asRecord(rawParams);

  switch (method) {
    case 'worker.hello':
    case 'ping':
      return {
        protocolVersion: PROTOCOL_VERSION,
        oar: '0.109.0',
        node: process.versions.node,
        pid: process.pid,
        uptimeMs: Date.now() - ctx.startedAt,
        sessions: ctx.sessions.list().length,
      };

    case 'runtime.list': {
      return {
        runtimes: ctx.registry.list().map(runtimeSummary),
      };
    }

    case 'runtime.installation': {
      const runtimeId = str(params, 'runtimeId')!;
      const rt = requireRuntime(ctx, runtimeId);
      if (!rt.installation) {
        return { runtimeId, snapshot: { kind: 'available', via: 'bundled' } };
      }
      const snapshot = await rt.installation();
      return { runtimeId, snapshot: jsonSafe(snapshot) };
    }

    case 'runtime.installPlan': {
      const runtimeId = str(params, 'runtimeId')!;
      const rt = requireRuntime(ctx, runtimeId);
      if (!rt.installPlan) {
        return { runtimeId, plan: null, reason: 'not_supported' };
      }
      const plan = await rt.installPlan({});
      return { runtimeId, plan: jsonSafe(plan) };
    }

    case 'runtime.install': {
      // Host must gate on user confirm; Worker never auto-installs on boot.
      const runtimeId = str(params, 'runtimeId')!;
      const rt = requireRuntime(ctx, runtimeId);
      if (!rt.install) {
        return { runtimeId, result: null, reason: 'not_supported' };
      }
      const line = str(params, 'line', false);
      const timeoutMs = typeof params?.timeoutMs === 'number' ? params.timeoutMs : undefined;
      const result = await rt.install({
        ...(line ? { line } : {}),
        ...(timeoutMs != null ? { timeoutMs } : {}),
      });
      return { runtimeId, result: jsonSafe(result) };
    }

    case 'runtime.auth': {
      const runtimeId = str(params, 'runtimeId')!;
      const rt = requireRuntime(ctx, runtimeId);
      if (!rt.authStatus) {
        return { runtimeId, status: null, reason: 'not_supported' };
      }
      const installation = await requireAvailable(rt);
      const status = await rt.authStatus(installation);
      return { runtimeId, status: jsonSafe(status) };
    }

    case 'runtime.models': {
      const runtimeId = str(params, 'runtimeId')!;
      const rt = requireRuntime(ctx, runtimeId);
      if (!rt.listModels) {
        return { runtimeId, models: null, reason: 'not_supported' };
      }
      const installation = await requireAvailable(rt);
      const result = await rt.listModels(installation);
      return { runtimeId, result: jsonSafe(result) };
    }

    case 'runtime.usage': {
      const runtimeId = str(params, 'runtimeId')!;
      const rt = requireRuntime(ctx, runtimeId);
      if (!rt.accountUsage) {
        return { runtimeId, usage: null, reason: 'not_supported' };
      }
      const installation = await requireAvailable(rt);
      const usage = await rt.accountUsage(installation);
      return { runtimeId, usage: jsonSafe(usage) };
    }

    case 'session.open': {
      const runtimeId = str(params, 'runtimeId')!;
      const cwd = str(params, 'cwd') ?? process.cwd();
      const model = str(params, 'model', false);
      const rt = requireRuntime(ctx, runtimeId);
      const installation = await requireAvailable(rt);
      const options: SessionOptions = {
        cwd,
        ...(model ? { model } : {}),
      };
      const session = await rt.session(installation, options);
      const tracked = ctx.sessions.add(runtimeId, session, 1);
      attachEvents(ctx, tracked);
      return {
        sessionKey: tracked.key,
        oarSessionId: tracked.oarSessionId,
        runtimeId,
        generation: tracked.generation,
        capabilities: jsonSafe(session.capabilities),
        placement: jsonSafe(session.placement),
      };
    }

    case 'session.resume': {
      const runtimeId = str(params, 'runtimeId')!;
      const cwd = str(params, 'cwd') ?? process.cwd();
      const resume = str(params, 'oarSessionId')!;
      const previousKey = str(params, 'previousSessionKey', false);
      const rt = requireRuntime(ctx, runtimeId);
      const installation = await requireAvailable(rt);
      const prevGen = previousKey ? (ctx.sessions.get(previousKey)?.generation ?? 0) : 0;
      const session = await rt.session(installation, { cwd, resume });
      if (previousKey) {
        const prev = ctx.sessions.get(previousKey);
        if (prev) {
          try {
            await prev.session.dispose();
          } catch {
            /* ignore */
          }
          ctx.sessions.delete(previousKey);
        }
      }
      const tracked = ctx.sessions.add(runtimeId, session, prevGen + 1 || 1);
      attachEvents(ctx, tracked);
      return {
        sessionKey: tracked.key,
        oarSessionId: tracked.oarSessionId,
        runtimeId,
        generation: tracked.generation,
        resumedFrom: resume,
        capabilities: jsonSafe(session.capabilities),
      };
    }

    case 'session.prompt': {
      const sessionKey = str(params, 'sessionKey')!;
      const input = str(params, 'input')!;
      const tracked = ctx.sessions.require(sessionKey);
      const outcome = await tracked.session.prompt(input);
      return {
        sessionKey,
        outcome: controlOutcomeSafe(outcome),
      };
    }

    case 'session.deliver': {
      const sessionKey = str(params, 'sessionKey')!;
      const input = str(params, 'input')!;
      const whenRaw = str(params, 'when', false);
      const tracked = ctx.sessions.require(sessionKey);
      // `interrupt` requires maxWaitMs; P0 only exposes the non-interrupt forms.
      if (whenRaw === 'interrupt') {
        throw Object.assign(
          new Error('params.when=interrupt requires maxWaitMs; use session.control abort instead'),
          { code: -32602 },
        );
      }
      if (whenRaw && whenRaw !== 'now' && whenRaw !== 'after_turn' && whenRaw !== 'when_idle') {
        throw Object.assign(new Error(`params.when invalid: ${whenRaw}`), { code: -32602 });
      }
      const result =
        whenRaw === undefined
          ? await tracked.session.deliver(input)
          : await tracked.session.deliver(input, {
              when: whenRaw as 'now' | 'after_turn' | 'when_idle',
            });
      return { sessionKey, result: jsonSafe(result) };
    }

    case 'session.control': {
      const sessionKey = str(params, 'sessionKey')!;
      const action = str(params, 'action')!;
      const tracked = ctx.sessions.require(sessionKey);
      let outcome;
      switch (action) {
        case 'abort':
        case 'cancel':
          outcome = await tracked.session.abort();
          break;
        default:
          throw Object.assign(new Error(`unsupported control action: ${action}`), {
            code: -32602,
          });
      }
      return { sessionKey, action, outcome: controlOutcomeSafe(outcome) };
    }

    case 'session.records': {
      const sessionKey = str(params, 'sessionKey')!;
      const tracked = ctx.sessions.require(sessionKey);
      return {
        sessionKey,
        records: jsonSafe(tracked.session.records()),
      };
    }

    case 'session.status': {
      const sessionKey = str(params, 'sessionKey')!;
      const tracked = ctx.sessions.require(sessionKey);
      return {
        sessionKey,
        status: jsonSafe(tracked.session.status()),
        model: jsonSafe(tracked.session.model()),
        usage: jsonSafe(tracked.session.usage()),
      };
    }

    case 'session.dispose': {
      const sessionKey = str(params, 'sessionKey')!;
      const tracked = ctx.sessions.require(sessionKey);
      await tracked.session.dispose();
      ctx.sessions.delete(sessionKey);
      return { sessionKey, disposed: true };
    }

    case 'session.list':
      return { sessions: ctx.sessions.list() };

    default:
      throw Object.assign(new Error(`method not found: ${method}`), { code: -32601 });
  }
}
