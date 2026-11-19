/** JSON-safe projection of OAR control / event objects (drop cycles / BigInt). */

export function jsonSafe(value: unknown): unknown {
  return JSON.parse(
    JSON.stringify(value, (_key, v) => {
      if (typeof v === 'bigint') return v.toString();
      if (v instanceof Error) {
        return { name: v.name, message: v.message };
      }
      return v;
    }),
  );
}

export function controlOutcomeSafe(outcome: {
  kind: string;
  seq: number;
  requestId: string;
  code?: string;
  reason?: string;
}): Record<string, unknown> {
  return {
    kind: outcome.kind,
    seq: outcome.seq,
    requestId: outcome.requestId,
    code: outcome.code ?? null,
    reason: outcome.reason ?? null,
  };
}
