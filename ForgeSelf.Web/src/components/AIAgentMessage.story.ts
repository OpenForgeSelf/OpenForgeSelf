/**
 * AIAgent 聊天助手 · 消息组件故事变体（CT 画廊用）
 *
 * 这是「用 AI Agent 插件的聊天助手测试画廊」的落地形态：挂进画廊的是插件真实聊天链路的
 * MessageItem（工具卡 / 失败续跑 / 用户气泡），不是为测试新写的样例组件。
 *
 * 四个变体覆盖近期修过的两个回归与两条常见状态：
 *   - 工具卡与正文共存（曾用 v-if/v-else 二选一，工具卡把正文整段吞掉）
 *   - 失败态优先给「继续执行」（重试会丢半截成果）
 *
 * 跨包说明：story 放在宿主 src/components 下（不在插件目录内），理由见
 * `AIAgentMessageStoryHost.vue` 头部注释（@playwright/test 只在宿主 node_modules）。
 * 插件前端自己的 story 目录约定与宿主一致：`<组件名>.story.ts`。
 */
import AIAgentMessageStoryHost from './AIAgentMessageStoryHost.vue'
import type { ComponentStory } from '../../ct/types'

/** 与真实宿主一致的最小配置：显示机器人头像、不启用角色门禁（不传 role = 不设防） */
const CONFIG = { showBotAvatar: true }

/** 固定时间：timeText 会格式化 message.time，用固定值避免截图里的时钟漂移 */
const AT = '2026-10-26T10:30:00'

const assistant = (patch: Record<string, unknown>) => ({
  id: 'm-1',
  role: 'assistant',
  time: AT,
  status: 'done',
  ...patch,
})

const user = { id: 'm-2', role: 'user', time: AT, content: '看一下当前项目结构' }

export const stories: ComponentStory[] = [
  // 测试状态：助手纯文本回复（Markdown 渲染、无工具卡）
  {
    id: 'AIAgent/MessageText',
    component: AIAgentMessageStoryHost,
    props: {
      message: assistant({ content: '已完成 **目录扫描**，共 12 个文件、3 个子目录。' }),
      config: CONFIG,
    },
  },

  // 测试状态：工具卡 + 正文共存（回归守卫：只出现工具卡而正文被吞 = 红）
  {
    id: 'AIAgent/MessageToolAndText',
    component: AIAgentMessageStoryHost,
    props: {
      message: assistant({
        content: '命令已执行完毕，工作区是干净的。',
        blocks: [
          {
            type: 'tool',
            name: 'run_terminal_command',
            icon: 'code',
            status: 'ok',
            args: { command: 'git status --porcelain' },
            result: '',
            durationMs: 420,
          },
        ],
      }),
      config: CONFIG,
    },
  },

  // 测试状态：同一消息内多行可折叠块（左列对齐 + 边框归属的几何靶子）
  // 第 1 行工具调用展开（.aiw-tool__body 带框）、第 2 行折叠（无框）、思考行折叠。
  // CT 用它断言：两行图标左缘 x 相等、悬浮时不位移、正文无框。
  {
    id: 'AIAgent/MessagesToolRows',
    component: AIAgentMessageStoryHost,
    props: {
      message: assistant({
        content: '两条工具调用都已完成，工作区干净。',
        blocks: [
          {
            type: 'tool',
            name: 'get_memory_stats',
            icon: 'file',
            status: 'ok',
            args: { scope: 'all' },
            result: '{"total":12}',
            durationMs: 84,
            open: true,
          },
          {
            type: 'tool',
            name: 'add_memory',
            icon: 'file',
            status: 'ok',
            args: { text: '记录一条' },
            result: 'ok',
            durationMs: 61,
            collapsed: true,
          },
          { type: 'thinking', text: '先看记忆规模，再补一条', done: true, durationMs: 300, collapsed: true },
        ],
      }),
      config: CONFIG,
    },
  },

  // 测试状态：失败态且可续跑（必须出现「继续执行」，并与「重试」并列）
  {
    id: 'AIAgent/MessageFailed',
    component: AIAgentMessageStoryHost,
    props: {
      message: assistant({ content: '', status: 'failed', error: '连接中断', resumable: true }),
      config: CONFIG,
    },
  },

  // 测试状态：用户消息（右对齐、无机器人头像、不折叠）
  {
    id: 'AIAgent/MessageUser',
    component: AIAgentMessageStoryHost,
    props: { message: user, config: CONFIG },
  },
]
