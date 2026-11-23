/**
 * GitHub 账号绑定域类型（PILOT-083）。与后端 GitHubAccountController 契约一一对应。
 * 令牌永不出现在任何类型字段里：绑定状态只含账号 login 与时间。
 */

/** 绑定状态（GET /api/github-account/status） */
export interface GitHubBindingStatus {
  /** 是否已绑定 */
  bound: boolean
  /** 绑定账号 login（如 octocat） */
  accountLogin: string
  /** 授权范围 */
  scopes: string
  /** 绑定时间（ISO 字符串或 null） */
  boundAt: string | null
  /** 插件 GitHub 拉取是否自动附凭据 */
  autoAttachToken: boolean
}

/** 设备码申请结果（POST /api/github-account/bind/start） */
export interface DeviceCodeStart {
  /** 设备码（仅用于轮询，不展示给用户） */
  deviceCode: string
  /** 用户码（展示并预填） */
  userCode: string
  /** 官方验证页地址（未预填） */
  verificationUri: string
  /** 预填后的验证页地址（user_code 已带） */
  verificationUriPrefilled: string
  /** 设备码有效期（秒） */
  expiresIn: number
  /** 建议轮询间隔（秒） */
  interval: number
}

/** 轮询状态（小写，与后端枚举 toString 对齐） */
export type GitHubPollStatus = 'pending' | 'slow_down' | 'bound' | 'expired' | 'denied' | 'failed'

/** 轮询结果（POST /api/github-account/bind/poll） */
export interface GitHubPollResult {
  status: GitHubPollStatus
  accountLogin: string | null
  error: string | null
}

/** 统一响应外壳 */
export interface ApiEnvelope<T> {
  success: boolean
  data?: T
  error?: string
}
