/**
 * 工具桥 API 层：全部端点走 `/api/tool-bridge*`，鉴权由 http.ts 带 Bearer token。
 * 后端响应是标准信封 { code/message/success/data }，http.ts 已解包到 data ⇒ 这里**不得再取 .data**
 * （plugin-development 铁律 15：再取一次得到 undefined，页面不报错但功能静默失效）。
 */

import { apiGet, apiPost, apiPut } from './http'
import type {
  ParseResult,
  PromptData,
  ToolResult,
  TurnList,
  TurnResponse,
  WorkspaceInfo,
  ParsedCall,
} from './types'

const BASE = '/api/tool-bridge'

export const toolBridgeApi = {
  fetchPrompt: (): Promise<PromptData | undefined> => apiGet<PromptData>(`${BASE}/prompt`),
  parse: (text: string): Promise<ParseResult | undefined> => apiPost<ParseResult>(`${BASE}/parse`, { text }),
  execute: (calls: ParsedCall[]): Promise<ExecuteResponse | undefined> =>
    apiPost<ExecuteResponse>(`${BASE}/execute`, { calls }),
  turn: (text: string): Promise<TurnResponse | undefined> => apiPost<TurnResponse>(`${BASE}/turn`, { text }),
  getWorkspace: (): Promise<WorkspaceInfo | undefined> => apiGet<WorkspaceInfo>(`${BASE}/workspace`),
  setWorkspace: (root: string, confirmUnsafe: boolean): Promise<WorkspaceInfo | undefined> =>
    apiPut<WorkspaceInfo>(`${BASE}/workspace`, { root, confirmUnsafe }),
  listTurns: (take = 20, skip = 0): Promise<TurnList | undefined> =>
    apiGet<TurnList>(`${BASE}/turns?take=${take}&skip=${skip}`),
  getTurn: (turnId: string): Promise<TurnResponse | TurnList | undefined> =>
    apiGet(`${BASE}/turns/${encodeURIComponent(turnId)}`),
}

/**
 * 根视图版本徽标数据源（铁律 13）：宿主 `GET /api/plugin` 返回信封里的数组，
 * 按插件 id 过滤取 version。解包已由 http.ts 完成，这里直接当数组用。
 */
export async function fetchPluginVersion(pluginId: string): Promise<string> {
  const data = await apiGet<{ id?: string; version?: string }[]>('/api/plugin')
  const list = Array.isArray(data) ? data : []
  return list.find(p => p?.id === pluginId)?.version ?? '未知版本'
}
