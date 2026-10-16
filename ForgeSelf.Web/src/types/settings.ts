// 本地配置（ForgeSetting.config，不落库）前端类型。

/** 本地配置（仅含前端需要的非敏感字段；代理密码永不下发，只回存在性） */
export interface AppSettings {
  /** 默认 AI 推理模型（chatModelId，格式：提供商:上游模型id） */
  defaultModel: string
  /** 首页插件 Id；宿主 `/` 重定向到该插件的 frontend.route，缺失时回退 `/home` */
  homePluginId: string
  /** 模型请求是否走代理（false=显式直连，忽略系统代理） */
  proxyEnabled: boolean
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
 */
export type UpdateAppSettings = Partial<Omit<AppSettings, 'proxyPasswordSet'>> & {
  proxyPassword?: string
}
