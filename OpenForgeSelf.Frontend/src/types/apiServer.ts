/// <summary>
/// API 服务器配置状态（对应后端 GET /api/api-server/status 响应）
/// </summary>
export interface ApiServerConfig {
  /** API 基准地址（主服务地址 + /v1） */
  apiBaseUrl: string
  /** API 密钥掩码展示（如 sk-****abcd） */
  apiKeyMasked: string
  /** API 密钥明文（仅管理面返回，外部公开接口不返回） */
  apiKeyPlain?: string
  /** 授权标头示例（如 Authorization: Bearer sk-...） */
  authHeader: string
  /** 是否已配置有效密钥 */
  hasKey: boolean
}
