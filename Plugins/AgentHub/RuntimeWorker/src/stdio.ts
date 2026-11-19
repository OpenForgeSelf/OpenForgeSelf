import * as readline from 'node:readline';
import type { JsonRpcError, JsonRpcId, JsonRpcRequest, JsonRpcSuccess } from './protocol.js';
import { createWorkerContext, dispatch } from './handlers.js';

function writeLine(obj: unknown): void {
  process.stdout.write(`${JSON.stringify(obj)}\n`);
}

function writeError(id: JsonRpcId | null, code: number, message: string, data?: unknown): void {
  const err: JsonRpcError = {
    jsonrpc: '2.0',
    id,
    error: data === undefined ? { code, message } : { code, message, data },
  };
  writeLine(err);
}

function writeResult(id: JsonRpcId, result: unknown): void {
  const ok: JsonRpcSuccess = { jsonrpc: '2.0', id, result };
  writeLine(ok);
}

/**
 * JSON-RPC 2.0 over JSONL stdio.
 * stdout = protocol frames only; stderr = human logs.
 */
export function runStdioWorker(): void {
  const ctx = createWorkerContext((method, params) => {
    writeLine({ jsonrpc: '2.0', method, params });
  });

  process.stderr.write(
    `[agenthub-runtime] ready protocol=1 oar=0.109.0 node=${process.versions.node}\n`,
  );

  const rl = readline.createInterface({ input: process.stdin, crlfDelay: Infinity });

  rl.on('line', (line) => {
    const trimmed = line.trim();
    if (!trimmed) return;

    let msg: JsonRpcRequest;
    try {
      msg = JSON.parse(trimmed) as JsonRpcRequest;
    } catch {
      writeError(null, -32700, 'parse error');
      return;
    }

    if (!msg || typeof msg.method !== 'string') {
      writeError(msg?.id ?? null, -32600, 'invalid request');
      return;
    }

    const id = msg.id;
    // Notification (no id): fire-and-forget; still run for side effects.
    void (async () => {
      try {
        const result = await dispatch(ctx, msg.method, msg.params);
        if (id !== undefined) {
          writeResult(id, result);
        }
      } catch (e) {
        const err = e as { code?: number; message?: string; data?: unknown };
        const code = typeof err.code === 'number' ? err.code : -32000;
        const message = err.message ?? String(e);
        if (id !== undefined) {
          writeError(id, code, message, err.data);
        } else {
          process.stderr.write(`[agenthub-runtime] notify error ${msg.method}: ${message}\n`);
        }
      }
    })();
  });

  const shutdown = async (signal: string) => {
    process.stderr.write(`[agenthub-runtime] shutting down (${signal})\n`);
    rl.close();
    try {
      await ctx.sessions.disposeAll();
    } catch (e) {
      process.stderr.write(`[agenthub-runtime] disposeAll: ${e}\n`);
    }
    process.exit(0);
  };

  process.on('SIGINT', () => void shutdown('SIGINT'));
  process.on('SIGTERM', () => void shutdown('SIGTERM'));
}
