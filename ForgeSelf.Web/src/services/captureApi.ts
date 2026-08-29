/**
 * 抓包代理 API 服务层 - 封装 /api/capture 端点通信
 */

import type {
  ListenerConfig,
  CreateListenerRequest,
  UpdateListenerRequest,
  CapturePagedResult,
  CaptureQueryParams,
  CaptureSessionDetail,
  CaCertInfo
} from '@/types/capture'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'
const CAPTURE_BASE = `${API_BASE_URL}/capture`

/**
 * 解析后端 ApiResponse<T> 包装，返回 T。
 * 兼容直接返回 T 的场景（向后兼容）。
 */
async function parseResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let message = `请求失败: ${response.status}`
    try {
      const json = await response.json()
      if (json?.message) message = json.message
    } catch {
      // ignore parse error
    }
    throw new Error(message)
  }

  if (response.status === 204) {
    return undefined as unknown as T
  }

  const json = await response.json()
  // ApiResponse<T> 格式: { code, message, success, data }
  // 若返回体含 data 字段则取 data，否则直接返回（兼容）
  return (json?.data !== undefined ? json.data : json) as T
}

export const captureApi = {
  /**
   * 获取监听器列表
   */
  async fetchListeners(): Promise<ListenerConfig[]> {
    const response = await fetch(`${CAPTURE_BASE}/listeners`)
    return parseResponse<ListenerConfig[]>(response)
  },

  /**
   * 创建监听器
   */
  async createListener(payload: CreateListenerRequest): Promise<ListenerConfig> {
    const response = await fetch(`${CAPTURE_BASE}/listeners`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    })
    return parseResponse<ListenerConfig>(response)
  },

  /**
   * 更新监听器
   */
  async updateListener(id: number, payload: UpdateListenerRequest): Promise<ListenerConfig> {
    const response = await fetch(`${CAPTURE_BASE}/listeners/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    })
    return parseResponse<ListenerConfig>(response)
  },

  /**
   * 删除监听器
   */
  async deleteListener(id: number): Promise<void> {
    const response = await fetch(`${CAPTURE_BASE}/listeners/${id}`, {
      method: 'DELETE'
    })
    if (!response.ok) {
      let message = `删除失败: ${response.status}`
      try {
        const json = await response.json()
        if (json?.message) message = json.message
      } catch {
        // ignore parse error
      }
      throw new Error(message)
    }
  },

  /**
   * 启动监听器
   */
  async startListener(id: number): Promise<void> {
    const response = await fetch(`${CAPTURE_BASE}/listeners/${id}/start`, {
      method: 'POST'
    })
    await parseResponse<void>(response)
  },

  /**
   * 停止监听器
   */
  async stopListener(id: number): Promise<void> {
    const response = await fetch(`${CAPTURE_BASE}/listeners/${id}/stop`, {
      method: 'POST'
    })
    await parseResponse<void>(response)
  },

  /**
   * 查询抓包记录（分页 + 过滤）
   */
  async fetchSessions(params?: CaptureQueryParams): Promise<CapturePagedResult> {
    const queryParams = new URLSearchParams()
    if (params?.listenerId !== undefined) {
      queryParams.append('listenerId', String(params.listenerId))
    }
    if (params?.protocol) {
      queryParams.append('protocol', params.protocol)
    }
    if (params?.page !== undefined) {
      queryParams.append('page', String(params.page))
    }
    if (params?.pageSize !== undefined) {
      queryParams.append('pageSize', String(params.pageSize))
    }

    const queryString = queryParams.toString()
    const url = `${CAPTURE_BASE}/sessions${queryString ? `?${queryString}` : ''}`

    const response = await fetch(url)
    return parseResponse<CapturePagedResult>(response)
  },

  /**
   * 获取抓包记录详情
   */
  async fetchSessionDetail(id: number): Promise<CaptureSessionDetail> {
    const response = await fetch(`${CAPTURE_BASE}/sessions/${id}`)
    return parseResponse<CaptureSessionDetail>(response)
  },

  /**
   * 清空抓包记录（可按监听器过滤）
   */
  async clearSessions(listenerId?: number): Promise<void> {
    const query = listenerId !== undefined ? `?listenerId=${listenerId}` : ''
    const response = await fetch(`${CAPTURE_BASE}/sessions${query}`, {
      method: 'DELETE'
    })
    await parseResponse<void>(response)
  },

  /**
   * 获取 CA 证书（HTTPS MITM 解密安装用）
   */
  async fetchCaCert(): Promise<CaCertInfo> {
    const response = await fetch(`${CAPTURE_BASE}/ca-cert`)
    return parseResponse<CaCertInfo>(response)
  }
}

export { parseResponse }
