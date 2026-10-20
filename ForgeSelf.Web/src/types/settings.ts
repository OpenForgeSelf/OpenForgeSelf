// 本地配置（ForgeSetting.config，不落库）前端类型。

/** 代理模式（FR-1）：system=跟随系统代理 / manual=手动配置 / direct=显式直连 */
export type ProxyMode = 'system' | 'manual' | 'direct'

/** 本地配置（仅含前端需要的非敏感字段；代理密码永不下发，只回存在性） */
export interface AppSettings {
  /** 默认 AI 推理模型（chatModelId，格式：提供商:上游模型id） */
  defaultModel: string
  /** 首页插件 Id；宿主 `/` 重定向到该插件的 frontend.route，缺失时回退 `/home` */
  homePluginId: string
  /** 代理模式（2026-10-16-settings-proxy-mode-bypass；取代旧 proxyEnabled） */
  proxyMode: ProxyMode
  /** 不走代理的条目（一行一条，manual/system 模式生效） */
  proxyBypass: string[]
  /** 代理服务器地址（域名/IPv4，不含 scheme/端口） */
  proxyHost: string
  /** 代理服务器端口（1-65535，0=未填） */
  proxyPort: number
  /** 代理认证用户名（空=无需认证） */
  proxyUsername: string
  /** 是否已设置代理密码（仅存在性，无密码本体） */
  proxyPasswordSet: boolean
}

/**
 * POST /api/settings 载荷：字段缺省=不修改。
 * proxyPassword：不传=不修改；空串=清空（仅「清除密码」链接提交）；非空=加密覆盖。
 * proxyBypass：不传=不修改；空数组=清空。
 */
export type UpdateAppSettings = Partial<Omit<AppSettings, 'proxyPasswordSet'>> & {
  proxyPassword?: string
}

/** POST /api/settings/test-proxy 入参（FR-3）：未保存的表单值，配置不落盘 */
export interface TestProxyRequest {
  proxyHost: string
  proxyPort: number
  proxyUsername?: string
  /** 仅本次请求内存使用；已保存密码留空时后端不补默认凭据以外的回退 */
  proxyPassword?: string
  /** 探测目标（http(s):// 绝对地址），缺省=内置探针 https://www.baidu.com */
  proxyTestUrl?: string
}

/** test-proxy 出参信封 data（判据：任何 HTTP 响应=代理可用，仅网络层失败=不可用） */
export interface TestProxyResult {
  reachable: boolean
  statusCode: number | null
  latencyMs: number
  message: string
}
