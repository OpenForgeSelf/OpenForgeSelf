/// <summary>
/// API 子密钥相关类型（与后端 ApiKeyItemDto / ApiKeyPlainResultDto 一一对齐，camelCase）
/// 见 specs/030-authentication-upgrade/design.md §3.3 / §3.4
/// </summary>

/** 列表项：永不携带明文、KeyHash、KeyCipher */
export interface ApiKeyItem {
  /** 主键 */
  id: number
  /** 密钥名称（可重命名） */
  name: string
  /** 用途备注（替代作用域） */
  remark: string
  /** 掩码展示，如 sk-****abcd；解密失败时为空串 */
  maskedKey: string
  /** 是否可在本机解密；false 时 UI 需提示「无法在本机解密，请重新生成」 */
  canDecrypt: boolean
  /** 是否启用 */
  enabled: boolean
  /** 是否已过期 */
  isExpired: boolean
  /** 过期时间（ISO 字符串），null 表示不过期 */
  expiresAt: string | null
  /** 最后使用时间（ISO 字符串，节流更新），null 表示从未使用 */
  lastUsedAt: string | null
  /** 创建时间（ISO 字符串） */
  createdAt: string
  /** 更新时间（ISO 字符串） */
  updatedAt: string
}

/** 创建 / 重新生成（roll）的返回：唯一一次携带明文 */
export interface ApiKeyPlainResult {
  /** 密钥条目（掩码） */
  item: ApiKeyItem
  /** 明文密钥，如 sk-xxxxxxxx……仅本次响应返回 */
  plainKey: string
  /** 授权标头示例，如 Authorization: Bearer sk-... */
  authHeader: string
}

/** 创建密钥请求体 */
export interface CreateApiKeyRequest {
  /** 名称；为空时后端自动生成「密钥 N」 */
  name: string
  /** 用途备注 */
  remark?: string
  /** 过期时间（ISO 字符串）；null / 省略表示不过期 */
  expiresAt?: string | null
}

/** 更新（重命名）密钥请求体：字段同创建 */
export type UpdateApiKeyRequest = CreateApiKeyRequest

/** 启停请求体 */
export interface ToggleApiKeyRequest {
  /** true 启用 / false 停用 */
  enabled: boolean
}

/** 删除结果 */
export interface DeleteApiKeyResult {
  /** 是否删除成功 */
  deleted: boolean
}
