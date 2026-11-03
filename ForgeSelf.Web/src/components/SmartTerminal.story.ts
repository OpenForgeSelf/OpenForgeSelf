/**
 * SmartTerminal 智能终端 · CT 画廊故事
 *
 * 覆盖产品关键 UI 态：空会话、实时输出、三级权限徽章、待审批弹窗、
 * 拒绝/批准历史、审计抽屉过滤、密钥脱敏行、错误态、含 Linux 壳的多壳选择器。
 *
 * 跨包：story / CT 放宿主 src/components（与 AIAgentMessage 同约定）。
 */
import SmartTerminalStoryHost from './SmartTerminalStoryHost.vue'
import type { ComponentStory } from '../../ct/types'
import type { SmartTerminalFixture } from '../../../Plugins/SmartTerminal/web/src/types'

const AT = '2026-11-04T10:30:00+08:00'

const linuxShells = [
  { id: 'bash', displayName: 'Bash', executable: '/bin/bash', kind: 'bash', available: true, note: '$SHELL 默认（/bin/bash）' },
  { id: 'zsh', displayName: 'Zsh', executable: '/usr/bin/zsh', kind: 'zsh', available: true },
  { id: 'sh', displayName: 'POSIX sh', executable: '/bin/sh', kind: 'sh', available: true },
  { id: 'fish', displayName: 'Fish', executable: 'fish', kind: 'fish', available: false },
  { id: 'dash', displayName: 'Dash', executable: '/bin/dash', kind: 'sh', available: true },
  { id: 'pwsh', displayName: 'PowerShell 7+', executable: 'pwsh', kind: 'pwsh', available: false, note: '未安装' },
  { id: 'powershell', displayName: 'Windows PowerShell', executable: 'powershell.exe', kind: 'powershell', available: false, note: '仅 Windows 可用' },
  { id: 'cmd', displayName: 'Command Prompt', executable: 'cmd.exe', kind: 'cmd', available: false, note: '仅 Windows 可用' },
]

const session = (patch: Record<string, unknown> = {}) => ({
  sessionId: 'sess-1',
  title: '构建会话',
  shellId: 'bash',
  shellDisplayName: 'Bash',
  cwd: '/workspace/OpenForgeSelf',
  permissionMode: 'AskWrite' as const,
  status: 'idle',
  createdAt: AT,
  updatedAt: AT,
  commandCount: 1,
  ...patch,
})

const baseLive = (): SmartTerminalFixture => ({
  version: '1.1.0-ct',
  shells: linuxShells,
  draftShell: 'bash',
  sessions: [session()],
  activeId: 'sess-1',
  globalMode: 'AskWrite',
  chunks: [
    { index: 0, stream: 'system', text: '会话已创建 · Bash · /workspace/OpenForgeSelf', at: AT },
    { index: 1, stream: 'stdout', text: 'hello from bash', at: AT },
  ],
  commands: [
    {
      commandId: 'cmd-1',
      sessionId: 'sess-1',
      commandRedacted: 'echo hello from bash',
      risk: 'Read',
      decision: 'Allow',
      matchedRule: 'readAllow:echo',
      status: 'completed',
      exitCode: 0,
      createdAt: AT,
    },
  ],
})

export const stories: ComponentStory[] = [
  // 测试状态：空会话列表 + Linux 多壳选择器（bash/zsh/sh 可用，Windows 壳不可用）
  {
    id: 'SmartTerminal/EmptySessions',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        version: '1.1.0-ct',
        shells: linuxShells,
        draftShell: 'bash',
        sessions: [],
        globalMode: 'AskWrite',
      } satisfies SmartTerminalFixture,
    },
  },

  // 测试状态：AskWrite 徽章 + 实时 stdout/system 输出
  {
    id: 'SmartTerminal/LiveOutputAskWrite',
    component: SmartTerminalStoryHost,
    props: { fixture: baseLive() },
  },

  // 测试状态：Auto 权限徽章
  {
    id: 'SmartTerminal/ModeAuto',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        ...baseLive(),
        globalMode: 'Auto',
        sessions: [session({ permissionMode: 'Auto', title: '自动执行会话' })],
      } satisfies SmartTerminalFixture,
    },
  },

  // 测试状态：AskHighRisk 权限徽章
  {
    id: 'SmartTerminal/ModeAskHighRisk',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        ...baseLive(),
        globalMode: 'AskHighRisk',
        sessions: [session({ permissionMode: 'AskHighRisk', title: '仅询高危' })],
      } satisfies SmartTerminalFixture,
    },
  },

  // 测试状态：待审批弹窗（写操作）
  {
    id: 'SmartTerminal/PendingApproval',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        ...baseLive(),
        showApproval: true,
        approvals: [
          {
            approvalId: 'appr-1',
            sessionId: 'sess-1',
            commandId: 'cmd-2',
            commandRedacted: 'rm -rf ./tmp-build',
            risk: 'HighRisk',
            matchedRule: 'highRisk:rm',
            reason: 'AskWrite 模式下高危命令需人工批准',
            mode: 'AskWrite',
            createdAt: AT,
          },
        ],
        activeApproval: {
          approvalId: 'appr-1',
          sessionId: 'sess-1',
          commandId: 'cmd-2',
          commandRedacted: 'rm -rf ./tmp-build',
          risk: 'HighRisk',
          matchedRule: 'highRisk:rm',
          reason: 'AskWrite 模式下高危命令需人工批准',
          mode: 'AskWrite',
          createdAt: AT,
        },
      } satisfies SmartTerminalFixture,
    },
  },

  // 测试状态：历史中含拒绝与已批准完成
  {
    id: 'SmartTerminal/DeniedAndApproved',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        ...baseLive(),
        chunks: [
          { index: 0, stream: 'system', text: '命令被拒绝：sudo apt remove --purge nginx', at: AT },
          { index: 1, stream: 'system', text: '已批准并执行：pnpm install', at: AT },
          { index: 2, stream: 'stdout', text: 'Done in 1.2s', at: AT },
        ],
        commands: [
          {
            commandId: 'cmd-deny',
            sessionId: 'sess-1',
            commandRedacted: 'sudo apt remove --purge nginx',
            risk: 'HighRisk',
            decision: 'Deny',
            matchedRule: 'highRisk:sudo',
            status: 'denied',
            createdAt: AT,
          },
          {
            commandId: 'cmd-ok',
            sessionId: 'sess-1',
            commandRedacted: 'pnpm install',
            risk: 'Write',
            decision: 'Allow',
            matchedRule: 'write:pnpm',
            status: 'completed',
            exitCode: 0,
            createdAt: AT,
          },
        ],
      } satisfies SmartTerminalFixture,
    },
  },

  // 测试状态：审计抽屉打开 + kind 过滤预填
  {
    id: 'SmartTerminal/AuditDrawer',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        ...baseLive(),
        showAudit: true,
        auditKindFilter: 'command',
        auditItems: [
          {
            eventId: 'evt-1',
            time: AT,
            kind: 'command',
            sessionId: 'sess-1',
            shellId: 'bash',
            commandRedacted: 'echo hello',
            decision: 'Allow',
            matchedRule: 'readAllow:echo',
            exitCode: 0,
          },
          {
            eventId: 'evt-2',
            time: AT,
            kind: 'approval',
            sessionId: 'sess-1',
            commandRedacted: 'rm -rf ./tmp',
            decision: 'Deny',
            matchedRule: 'highRisk:rm',
            detail: 'user denied',
          },
          {
            eventId: 'evt-3',
            time: AT,
            kind: 'permission_mode',
            decision: 'AskWrite',
            detail: 'global',
          },
        ],
      } satisfies SmartTerminalFixture,
    },
  },

  // 测试状态：密钥脱敏行（token/password 占位）
  {
    id: 'SmartTerminal/SecretRedacted',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        ...baseLive(),
        chunks: [
          { index: 0, stream: 'stdout', text: 'export TOKEN=***REDACTED***', at: AT },
          { index: 1, stream: 'stderr', text: 'Authorization: Bearer ***REDACTED***', at: AT },
          { index: 2, stream: 'system', text: '已脱敏写入审计日志', at: AT },
        ],
        commands: [
          {
            commandId: 'cmd-secret',
            sessionId: 'sess-1',
            commandRedacted: 'curl -H "Authorization: Bearer ***REDACTED***" https://api.example.com',
            risk: 'HighRisk',
            decision: 'Allow',
            matchedRule: 'highRisk:curl',
            status: 'completed',
            exitCode: 0,
            createdAt: AT,
          },
        ],
      } satisfies SmartTerminalFixture,
    },
  },

  // 测试状态：401 未授权
  {
    id: 'SmartTerminal/ErrorUnauthorized',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        version: '1.1.0-ct',
        shells: linuxShells,
        draftShell: 'bash',
        sessions: [],
        unauthorized: true,
        globalMode: 'AskWrite',
      } satisfies SmartTerminalFixture,
    },
  },

  // 测试状态：通用错误横幅
  {
    id: 'SmartTerminal/ErrorGeneric',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        ...baseLive(),
        errorText: '操作失败：工作目录不存在: /missing/path',
      } satisfies SmartTerminalFixture,
    },
  },

  // 测试状态：多壳选择器（强调 bash/zsh/sh 可用）
  {
    id: 'SmartTerminal/LinuxShellPicker',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        version: '1.1.0-ct',
        shells: linuxShells,
        draftShell: 'zsh',
        sessions: [session({ shellId: 'zsh', shellDisplayName: 'Zsh', title: 'Zsh 会话' })],
        activeId: 'sess-1',
        globalMode: 'AskWrite',
        chunks: [{ index: 0, stream: 'stdout', text: 'zsh 5.9', at: AT }],
        commands: [],
      } satisfies SmartTerminalFixture,
    },
  },
]
