/**
 * agentBridge —— AIAgent 插件 ⇄ agent-core 组件的适配层（v1.8.0 迁移）。
 *
 * 职责：
 * 1. 构造组件 config（inline 内嵌形态、隐藏组件标题栏、关闭组件本地持久化）；
 * 2. 实现 transport：按当前激活 Agent 的 executionMode 分流到 AIAgent 后端
 *    （free → streamAgentChat 聊天流；plan → streamAgentRun 计划驱动流），
 *    工具事件本批不渲染（组件无工具卡片），usage 上抛、错误转组件 error 条。
 *
 * 组件零插件依赖：本文件是唯一把组件与插件 http/types 接起来的地方。
 */
import { streamAgentChat, streamAgentRun } from './http'
import type { AgentUsage } from './http'
import type { AgentDefinition } from './types'
import type { AgentChatConfig, TransportCtx } from './agent-core/types'
import { deriveToolAllowlist } from './composerContext'

/** 适配层依赖：全部以 getter 注入，保证 transport 每次发送都取最新值（响应式源）。 */
export interface AgentBridgeDeps {
  sessionId: () => string
  activeAgentId: () => string
  selectedModelId: () => string
  selectedToolNames: () => string[]
  selectedSkillIds: () => string[]
  /** 当前激活 Agent（用于 executionMode 分流；null = 按 free 处理）。 */
  activeAgent: () => AgentDefinition | null
  /**
   * 041 权限三档（readonly / standard / full）。
   * getter 注入而非传值：用户随时可能在输入框下方切档，transport 必须每次发送取最新。
   * 缺省按 standard 处理（宁可少放开，不可意外放开）。
   */
  accessMode?: () => string
  /** 全量已注册工具名（权限派生需要「未勾选时按档位下发全集」，见 composerContext.deriveToolAllowlist）。 */
  allToolNames?: () => string[]
}

/** 宿主外观快照：组件的深/浅主题与主色必须跟随宿主，不能用组件自带的紫/白底默认值。 */
export interface HostAppearance {
  /** 宿主当前是否深色主题（html.dark / data-theme=dark）。 */
  dark: boolean
  /** 宿主主色 hex（取自 --el-color-primary），映射组件 --aiw-primary。 */
  primaryColor: string
}

/** 宿主品牌主色兜底（与 DESIGN.md 琥珀色一致）：读不到 --el-color-primary 时使用。 */
const HOST_FALLBACK_PRIMARY = '#F59E0B'

/**
 * 订阅宿主外观（深/浅主题、主色）。宿主切换主题时 <html> 的 class / data-theme / style
 * 发生变化，MutationObserver 触发重读；组件 config 据此响应，无需刷新页面。
 * 返回 get（同步快照）/ subscribe（变更订阅，返回退订函数）/ dispose（停止监听）。
 */
export function createHostAppearance(): {
  get: () => HostAppearance
  subscribe: (cb: (v: HostAppearance) => void) => () => void
  dispose: () => void
} {
  const read = (): HostAppearance => {
    const root = document.documentElement
    const dark = root.classList.contains('dark') || root.getAttribute('data-theme') === 'dark'
    const raw = getComputedStyle(root).getPropertyValue('--el-color-primary').trim()
    return { dark, primaryColor: raw || HOST_FALLBACK_PRIMARY }
  }
  const listeners = new Set<(v: HostAppearance) => void>()
  let current = read()
  const emit = () => {
    const next = read()
    if (next.dark !== current.dark || next.primaryColor !== current.primaryColor) {
      current = next
      listeners.forEach((cb) => cb(current))
    }
  }
  const observer = new MutationObserver(emit)
  observer.observe(document.documentElement, {
    attributes: true,
    attributeFilter: ['class', 'data-theme', 'style'],
  })
  return {
    get: () => current,
    subscribe: (cb) => {
      listeners.add(cb)
      return () => listeners.delete(cb)
    },
    dispose: () => observer.disconnect(),
  }
}

/**
 * 构造组件 config。transport 内部不重试/不续传（组件侧 fallbackToMock 关、reconnect 由
 * 组件默认策略管理；本批 AIAgent 流中断的续传记 TODO），错误一律转组件 error 条。
 */
export function buildAgentChatConfig(deps: AgentBridgeDeps): AgentChatConfig {
  return {
    // —— 形态：内嵌 + 隐藏组件自身标题栏（AIAgent 中栏工具条由页面提供）——
    showHeader: false,
    inline: true,
    /* 助手消息不显示头像（用户要求）：这类「一个面板=一个助手」的布局里，
       头像只是在重复面板标题已经说清的信息，纯占宽度。用户侧本来也没有头像。 */
    showBotAvatar: false,
    // —— 数据：对话归属 AIAgent 后端（ISessionStore），组件不落本地存储/历史 ——
    persist: false,
    history: { enabled: false },
    storageKey: 'forgeself-agent-msg',
    sessionId: '',
    // —— 文案与外观：保持与宿主 --el-* 主题协调 ——
    title: 'AI Agent',
    subtitle: '就绪',
    welcome:
      '你好，我是铸己匣 AI Agent 👋\n告诉我你想做什么，比如「查看项目结构」「帮我实现一个功能」或「排查这个报错」。',
    quickReplies: ['查看当前项目结构', '帮我实现一个功能', '排查一个报错'],
    /* 041 状态矩阵：宿主在拉历史 / 切会话时把它置 true（见 AiAgentView.loadHistory）。
       值由 reactive 的 agentConfig 承载，所以改它组件立刻跟着变。 */
    loading: false,
    placeholder: '输入消息，Enter 发送…',
    footerNote: '',
    showFooterNote: false,
    theme: 'page',
    darkMode: false,
    position: 'right',
    autoOpen: true,
    // —— 发送通道：AIAgent 后端 ——
    transport: async (ctx: TransportCtx) => {
      const sessionId = deps.sessionId() || ctx.sessionId
      const chatModelId = deps.selectedModelId() || undefined
      const agentId = deps.activeAgentId() || undefined
      const toolNames = deps.selectedToolNames()
      const skillIds = deps.selectedSkillIds()
      /* 041 权限三档：下发的白名单 = 「用户勾选」∩「当前档位允许」。
         未勾选时也必须按档位下发全集 —— 后端语义是「空 = 默认全挂」，
         不下发就等于绕过档位（详见 composerContext.deriveToolAllowlist 注释）。 */
      const allowlist = deriveToolAllowlist(
        deps.accessMode?.() ?? 'standard',
        toolNames,
        deps.allToolNames?.() ?? []
      )

      const agent = deps.activeAgent()
      if (agent?.executionMode === 'plan') {
        // 计划驱动：步骤中间产出归步骤卡（本批不渲染，记 TODO），仅终局交付文本展示
        await streamAgentRun(
          { sessionId, agentId, taskInput: ctx.text, chatModelId },
          {
            onContent: () => {
              /* 步骤内增量本批不展示（步骤卡迁入组件后恢复） */
            },
            onUsage: (u: AgentUsage | undefined) => {
              if (u) ctx.onUsage?.(u)
            },
            onDone: (content: string) => {
              if (content) ctx.onDelta(content)
            },
            onError: (msg: string) => ctx.onError(new Error(msg || '计划执行失败')),
          },
          ctx.signal ?? undefined,
        )
        return
      }

      // 自由循环：流式增量逐块展示
      await streamAgentChat(
        {
          sessionId,
          message: ctx.text,
          chatModelId,
          agentId,
          enabledToolNames: allowlist && allowlist.length > 0 ? allowlist : undefined,
          skillIds: skillIds.length > 0 ? skillIds : undefined,
        },
        {
          onContent: (chunk: string) => ctx.onDelta(chunk),
          onToolCall: () => {
            /* 工具卡片本批不渲染（组件迁入后恢复，记 TODO） */
          },
          onToolResult: () => {
            /* 工具卡片本批不渲染（组件迁入后恢复，记 TODO） */
          },
          onUsage: (u: AgentUsage | undefined) => {
            if (u) ctx.onUsage?.(u)
          },
          onDone: () => {
            /* 完成由组件 finally 统一收尾 */
          },
          onError: (msg: string) => ctx.onError(new Error(msg || '请求失败')),
        },
        ctx.signal ?? undefined,
      )
    },
  }
}
