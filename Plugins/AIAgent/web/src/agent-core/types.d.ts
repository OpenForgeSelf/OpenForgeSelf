/**
 * agent-core（ai-assistant-widget 迁移）的 TS 类型边界。
 * 组件本体为 JS（迁移保真），这里只给宿主侧（agentBridge.ts / AiAgentView.vue）提供编译期形状。
 */

/** transport 上下文：由 useChat.send() 在 transport 分支注入 */
export interface TransportCtx {
  text: string
  sessionId: string
  model: string
  systemPrompt: string
  /** 追加一个流式文本块（最终由组件收尾合并为一条 assistant 消息） */
  onDelta: (chunk: string) => void
  onDone?: (info?: unknown) => void
  onUsage?: (usage: unknown) => void
  /** 抛错即中止本轮；组件转 error 条展示 */
  onError: (err: Error) => void
  signal?: AbortSignal | null
}

export type AgentTransport = (ctx: TransportCtx) => Promise<void>

/** 组件配置（完整默认值见 agent-core/config.js；此处只列宿主常覆盖的键） */
export interface AgentChatConfig {
  [key: string]: unknown
  showHeader?: boolean
  inline?: boolean
  transport?: AgentTransport | null
  sessionId?: string
  persist?: boolean
  storageKey?: string
  title?: string
  subtitle?: string
  welcome?: string
  placeholder?: string
  quickReplies?: string[]
  /** 消息区「加载中」（041 状态矩阵）：宿主拉历史 / 切会话时置 true，收尾置 false。
      空态会自动让位 —— 两个「没内容」的态同时出现只会让人更困惑。 */
  loading?: boolean
  /** 空状态文案（041 状态矩阵）。不配则用组件兜底文案；
      配了 welcome 的宿主不会走到空态（那时屏上有内容，再叠一层只是两套引导打架）。 */
  emptyTitle?: string
  emptyHint?: string
  footerNote?: string
  showFooterNote?: boolean
  darkMode?: boolean
  theme?: 'page' | 'admin' | 'bigscreen'
  position?: 'left' | 'right'
  autoOpen?: boolean
  /** 输入区上方的已选上下文 chips（所见即所发）。 */
  context?: { chips?: unknown[]; onRemove?: (key: string) => void; onClick?: (key: string) => void } | null
  /** 输入框下方的上下文图标行 + 权限胶囊（041）。 */
  contextBar?: {
    items?: unknown[]
    panels?: Record<string, { title: string; multi?: boolean; items?: unknown[]; empty?: string }>
    access?: { modes?: Array<{ key: string; label: string; desc: string }>; value: string; onChange?: (m: string) => void } | null
    onOpen?: (key: string) => void
    onClose?: (key: string) => void
    onPick?: (e: { key: string; id: string }) => void
  } | null
  /** 紧贴输入框上沿的「待你动作」区（041）：审批 / 询问。 */
  ask?: {
    items?: Array<{
      id: string
      kind: 'approval' | 'question'
      title: string
      detail?: string
      risk?: 'low' | 'medium' | 'high' | 'critical'
      options?: string[]
    }>
    progress?: number
    onResolve?: (e: { id: string; decision: string }) => void
    onAnswer?: (e: { id: string; option: string }) => void
  } | null
  /** 待办独立入口（041 修订二）：输入框下方底行最左，点击展开浮层。 */
  todos?: {
    items?: Array<{ id?: string; title: string; done?: boolean }>
    openCount?: number
    progress?: number
  } | null
}

/** ChatWidget 组件 exposed 句柄（AiAgentView 通过 ref 调用） */
export interface ChatWidgetExpose {
  open(): void
  close(): void
  toggle(): void
  send(text?: string): void
  abort(): void
  stop(): void
  reset(): void
  push(msg: unknown): void
  isSending(): boolean
  /** v1.8.0 集成扩展：外部灌入历史消息（会话切换） */
  hydrate(list: unknown[]): void
  /** composer 升级（2026-10-15）：导出会话为 Markdown（/export 内置命令） */
  exportSession(id?: string): string | undefined
  messages: { value: unknown[] }
  lastUsage: { value: unknown | null }
  // 其余 expose（sessions/export/remote/audit/gates…）本批宿主不直接使用，不逐一声明
}
