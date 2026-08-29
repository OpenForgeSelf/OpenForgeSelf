/**
 * 抓包代理（ProxyCapture 插件）类型定义
 */

/** 监听器配置（含运行时状态） */
export interface ListenerConfig {
  id: number
  name: string
  listenAddress: string
  listenPort: number
  /** 目标主机，未配置则仅抓包不转发 */
  targetHost?: string | null
  /** 目标端口，未配置则仅抓包不转发 */
  targetPort?: number | null
  enabled: boolean
  description?: string | null
  createdAt: string
  updatedAt: string
  /** 运行时是否正在监听 */
  isRunning: boolean
}

/** 创建监听器请求 */
export interface CreateListenerRequest {
  name: string
  listenAddress: string
  listenPort: number
  targetHost?: string | null
  targetPort?: number | null
  enabled: boolean
  description?: string | null
}

/** 更新监听器请求（字段同创建） */
export interface UpdateListenerRequest extends CreateListenerRequest {}

/** 抓包记录摘要（列表用） */
export interface CaptureSessionSummary {
  id: number
  listenerId: number
  timestamp: string
  /** HTTP / HTTPS / TCP 等 */
  protocol: string
  clientIp: string
  target?: string | null
  method?: string | null
  url?: string | null
  statusCode?: number | null
  requestBytes: number
  responseBytes: number
  durationMs: number
  /** 是否已转发到目标 */
  forwarded: boolean
}

/** 抓包记录详情（抽屉用） */
export interface CaptureSessionDetail extends CaptureSessionSummary {
  localEndpoint: string
  httpVersion?: string | null
  requestHeaders?: string | null
  requestBody?: string | null
  responseHeaders?: string | null
  responseBody?: string | null
  rawPreview?: string | null
  errorMessage?: string | null
}

/** 分页结果 */
export interface CapturePagedResult {
  items: CaptureSessionSummary[]
  total: number
  page: number
  pageSize: number
}

/** 抓包记录查询参数 */
export interface CaptureQueryParams {
  listenerId?: number
  protocol?: string
  page?: number
  pageSize?: number
}

/** CA 证书信息（HTTPS MITM 解密用） */
export interface CaCertInfo {
  pem: string
  installHint: string
  certPath: string
}
