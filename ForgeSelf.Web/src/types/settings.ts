// 本地配置（ForgeSetting.config，不落库）前端类型。

/** 本地配置（仅含前端需要的非敏感字段） */
export interface AppSettings {
  /** 默认 AI 推理模型（chatModelId，格式：提供商:上游模型id） */
  defaultModel: string
  /** 首页插件 Id；宿主 `/` 重定向到该插件的 frontend.route，缺失时回退 `/home` */
  homePluginId: string
}
