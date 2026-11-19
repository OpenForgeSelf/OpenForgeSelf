/** JSON-RPC 2.0 style JSONL frames over stdio (OAR Worker ↔ .NET). */

export type JsonRpcId = string | number;

export interface JsonRpcRequest {
  jsonrpc?: '2.0';
  id?: JsonRpcId;
  method: string;
  params?: unknown;
}

export interface JsonRpcSuccess {
  jsonrpc: '2.0';
  id: JsonRpcId;
  result: unknown;
}

export interface JsonRpcError {
  jsonrpc: '2.0';
  id: JsonRpcId | null;
  error: { code: number; message: string; data?: unknown };
}

export interface JsonRpcNotification {
  jsonrpc: '2.0';
  method: string;
  params?: unknown;
}

export const PROTOCOL_VERSION = 1;
