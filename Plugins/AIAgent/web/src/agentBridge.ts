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
import { resumeAgentRun, streamAgentChat, streamAgentRun } from './http'
import type { AgentUsage } from './http'
import type {
  AgentDefinition,
  GoalEvaluatedPayload,
  GoalRoundStartedPayload,
  PlanCreatedPayload,
  RunStuckPayload,
  StepCompletedPayload,
  StepStartedPayload,
} from './types'
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

/* ======================= 计划进度卡（plan 模式） =======================
   为什么放在 transport 里做：计划事件（plan_created / step_started /
   step_completed）只在这一条流上出现，旧实现把它们全丢了，界面于是
   从「发送」直接跳到「整段结论」，中间几分钟完全静默。 */

/** 计划中的一步（后端 Plan DSL 的宽松投影）。 */
export interface PlanStepLike {
  id?: string
  title?: string
  name?: string
  objective?: string
}

export interface PlanBlockLike {
  id: string
  type: 'plan'
  title: string
  steps: Array<{ title: string; status: string; output?: string }>
}

const STEP_OUTPUT_MAX = 400

/** 把 `plan_created` 的负载压成组件认的 plan 块。 */
export function toPlanBlock(runId: number, plan: { steps?: PlanStepLike[] } | undefined): PlanBlockLike {
  const steps = (plan?.steps ?? []).map((s) => ({
    title: String(s?.title ?? s?.name ?? s?.objective ?? '步骤'),
    status: 'pending',
  }))
  return { id: `plan:${runId}`, type: 'plan', title: `执行计划（${steps.length} 步）`, steps }
}

/** 按步骤事件推进计划块（返回新对象，保证 Vue 能看见变化）。 */
export function applyStepEvent(
  block: PlanBlockLike,
  ev: { stepIndex: number; status: 'running' | 'done' | 'failed'; output?: string },
): PlanBlockLike {
  const steps = block.steps.map((s, i) => {
    if (i !== ev.stepIndex) return s
    const output = ev.output != null ? String(ev.output).slice(0, STEP_OUTPUT_MAX) : s.output
    return { ...s, status: ev.status, output }
  })
  return { ...block, steps }
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
/**
 * 计划型 run 的事件 → 组件块/文本的映射。
 * transport 与 resume 必须共用这一份：两处各写一遍，续跑那次的步骤卡
 * 迟早与首次执行长得不一样。
 */
/** 目标评估事件 → 聊天正文里的紧凑进度行（goal 模式续跑/恢复时可见）。 */
function goalLine(p: GoalEvaluatedPayload): string {
  const tag = `〔目标评估 第 ${p.round} 轮〕`
  if (p.achieved) return `\n${tag}达标${p.summary ? `：${p.summary}` : ''}\n`
  const miss = p.missing?.length ? `：${p.missing.join('；')}` : ''
  const note = p.maxRounds && p.round >= p.maxRounds ? '（预算已用完，等待人工处理）' : '（将追加纠正步骤继续）'
  return `\n${tag}未达成${miss}，${note}\n`
}

function planHandlers(
  ctx: {
    onDelta: (c: string) => void
    onBlock?: (b: unknown) => void
    onUsage?: (u: unknown) => void
  },
  getRunId: () => number,
  setRunId: (id: number) => void,
) {
  let planBlock: PlanBlockLike | null = null
  let stepBuffer = ''
  const emitPlan = () => {
    if (!planBlock) return
    try {
      ctx.onBlock?.({ ...planBlock, steps: planBlock.steps.map((s) => ({ ...s })) })
    } catch {
      /* 渲染层异常不影响正文流 */
    }
  }
  return {
    onPlanCreated: (p: PlanCreatedPayload) => {
      const rid = Number((p?.plan as { runId?: number } | undefined)?.runId) || getRunId()
      setRunId(rid)
      planBlock = toPlanBlock(rid, p?.plan)
      emitPlan()
    },
    onStepStarted: (p: StepStartedPayload) => {
      if (!planBlock) return
      stepBuffer = ''
      planBlock = applyStepEvent(planBlock, { stepIndex: p.stepIndex, status: 'running' })
      emitPlan()
    },
    onStepCompleted: (p: StepCompletedPayload) => {
      if (!planBlock) return
      planBlock = applyStepEvent(planBlock, {
        stepIndex: p.stepIndex,
        status: 'done',
        output: p.output || stepBuffer,
      })
      stepBuffer = ''
      emitPlan()
    },
    onContent: (chunk: string) => {
      /* 步骤内正文只进步骤卡产出，不混进最终结论 */
      stepBuffer += chunk
    },
    onRunStuck: (p: RunStuckPayload) => {
      if (planBlock) {
        planBlock = applyStepEvent(planBlock, { stepIndex: p.stepIndex, status: 'failed' })
        emitPlan()
      }
      throw new Error('执行已暂停：等待你审批或补充信息（可到 Run 记录面板处理）')
    },
    /* FR-3 目标模式：评估结论以紧凑行进正文（轮次/达标/缺失项）；轮次开始不单独播报 */
    onGoalRoundStarted: (_p: GoalRoundStartedPayload) => {},
    onGoalEvaluated: (p: GoalEvaluatedPayload) => {
      ctx.onDelta(goalLine(p))
    },
    onUsage: (u: AgentUsage | undefined) => {
      if (u) ctx.onUsage?.(u)
    },
  }
}
/** 中断后接着跑：`POST /api/ai-agent/runs/{id}/resume`。已完成的步骤不会重跑。 */
export async function resumeRun(
  runId: number,
  ctx: {
    onDelta: (c: string) => void
    onBlock?: (b: unknown) => void
    onUsage?: (u: unknown) => void
  },
  signal?: AbortSignal | null,
): Promise<void> {
  let rid = runId
  const handlers = planHandlers(ctx, () => rid, (n) => { rid = n })
  const { completed } = await resumeAgentRun(
    runId,
    {
      ...handlers,
      onDone: (content: string) => {
        if (content) ctx.onDelta(content)
      },
      onError: (msg: string) => {
        throw new Error(msg || '继续执行失败')
      },
    } as Parameters<typeof resumeAgentRun>[1],
    signal ?? undefined,
  )
  if (!completed) {
    const err = new Error('继续执行中断：服务端未发完成事件') as Error & {
      runId?: number
      resumable?: boolean
    }
    err.name = 'StreamInterruptedError'
    err.runId = runId
    err.resumable = true
    throw err
  }
}

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
    // ---- 中断续跑：仅 plan 模式有服务端 resume 能力 ----
    resume: async (rc) => {
      await resumeRun(rc.runId, rc, rc.signal)
    },
    transport: async (ctx: TransportCtx) => {
      /* ---- 工具事件 → 组件富内容块 ----
         Agent 跑一批工具动辄十几秒：不告诉界面，用户只看到一只不动的气泡，
         很容易以为卡死了。这里把每次调用发成「工具卡片」，流式期间就可见。 */
      type ToolBlock = {
        type: 'tool'
        name: string
        status: 'running' | 'ok' | 'error'
        args?: string
        result?: string
        startedAt?: number
        durationMs?: number
        dangerous?: boolean
      }
      const DANGEROUS_RE =
        /(write|create|update|delete|remove|rename|move|terminal|command|exec|script|save|upload|kill|install|cleanup)/i
      /* ---- 计划进度 ---- */
      let planBlock: PlanBlockLike | null = null
      let stepBuffer = '' // 当前步骤内累计的正文
      const emitPlan = () => {
        if (!planBlock) return
        try {
          ctx.onBlock?.({ ...planBlock, steps: planBlock.steps.map((s) => ({ ...s })) })
        } catch {
          /* 渲染层异常不影响正文流 */
        }
      }

      const toolBlocks: ToolBlock[] = []
      const emitTool = (b: ToolBlock) => {
        toolBlocks.push(b)
        try {
          ctx.onBlock?.(b)
        } catch {
          /* 渲染层异常不能影响正文流 */
        }
      }
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
        let runId = 0
        const { completed: runCompleted, errored: runErrored } = await streamAgentRun(
          { sessionId, agentId, taskInput: ctx.text, chatModelId },
          {
            onPlanCreated: (p: PlanCreatedPayload) => {
              runId = Number(p?.plan && (p.plan as { runId?: number }).runId) || runId
              planBlock = toPlanBlock(runId, p?.plan)
              emitPlan()
            },
            onStepStarted: (p: StepStartedPayload) => {
              if (!planBlock) return
              stepBuffer = ''
              planBlock = applyStepEvent(planBlock, { stepIndex: p.stepIndex, status: 'running' })
              emitPlan()
            },
            onStepCompleted: (p: StepCompletedPayload) => {
              if (!planBlock) return
              planBlock = applyStepEvent(planBlock, {
                stepIndex: p.stepIndex,
                status: 'done',
                output: p.output || stepBuffer,
              })
              stepBuffer = ''
              emitPlan()
            },
            onContent: (chunk: string) => {
              /* 步骤内正文只进步骤卡的产出，不混进最终结论 ——
                 否则最终答案会把每一步的草稿都拼一遍。 */
              stepBuffer += chunk
            },
            onUsage: (u: AgentUsage | undefined) => {
              if (u) ctx.onUsage?.(u)
            },
            onDone: (content: string) => {
              if (content) ctx.onDelta(content)
            },
            onRunStuck: (p: RunStuckPayload) => {
              if (planBlock) {
                planBlock = applyStepEvent(planBlock, { stepIndex: p.stepIndex, status: 'failed' })
                emitPlan()
              }
              /* Run 卡住等人工介入。原来这个事件根本没人接 ——
                 界面永远是「运行中」，用户不知道要他做什么。 */
              ctx.onError(
                new Error('执行已暂停：等待你审批或补充信息（可到 Run 记录面板处理）')
              )
            },
            /* FR-3 目标模式：评估结论以紧凑行进正文（resume/服务端续跑会触发） */
            onGoalRoundStarted: (_p: GoalRoundStartedPayload) => {},
            onGoalEvaluated: (p: GoalEvaluatedPayload) => {
              ctx.onDelta(goalLine(p))
            },
            onError: (msg: string) => ctx.onError(new Error(msg || '计划执行失败')),
          },
          ctx.signal ?? undefined,
        )
        if (!runCompleted && !runErrored) {
          const err = new Error('计划执行中断：服务端未发完成事件')
          err.name = 'StreamInterruptedError'
          /* 只有拿到 runId 才谈得上续跑（服务端 runs/{id}/resume 需要它）。 */
          if (runId > 0) {
            ;(err as Error & { runId?: number; resumable?: boolean }).runId = runId
            ;(err as Error & { runId?: number; resumable?: boolean }).resumable = true
          }
          throw err
        }
        return
      }

      // 自由循环：流式增量逐块展示
      const { completed, errored } = await streamAgentChat(
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
          onToolCall: (e: { name?: string; arguments?: string }) => {
            const name = e?.name || 'tool'
            emitTool({
              type: 'tool',
              name,
              status: 'running',
              args: e?.arguments,
              startedAt: Date.now(),
              dangerous: DANGEROUS_RE.test(name),
            })
          },
          onToolResult: (e: { name?: string; result?: string; success?: boolean }) => {
            const name = e?.name || 'tool'
            const hit = [...toolBlocks].reverse().find((b) => b.name === name && b.status === 'running')
            if (hit) {
              hit.status = e?.success === false ? 'error' : 'ok'
              hit.result = e?.result
              hit.durationMs = hit.startedAt ? Date.now() - hit.startedAt : undefined
            } else {
              emitTool({ type: 'tool', name, status: e?.success === false ? 'error' : 'ok', result: e?.result })
            }
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
      /* ★ 断流必须可见。反代 60s 超时、进程被杀、Wi-Fi 掉一下，SSE 都是
         「正常结束」而不发 done 事件。不提前报错，半截回答会被当成完整回答存下来，
         下一轮还会被当上下文回喂。 */
      if (!completed && !errored) {
        const err = new Error('流式响应中断：服务端未发完成事件（已保留已生成内容）')
        err.name = 'StreamInterruptedError'
        throw err
      }
    },
  }
}
