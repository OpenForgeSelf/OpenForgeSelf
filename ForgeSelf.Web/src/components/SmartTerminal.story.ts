/**
 * SmartTerminal 智能终端 · CT 画廊故事
 *
 * 覆盖：空标签、多标签、直播输出、三级权限、审批、拒绝/批准、审计过滤、
 * 脱敏、错误、Linux 多壳、URL 本地/SSH 启动横幅、标签重命名。
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
  launchType: 'local',
  ...patch,
})

const baseLive = (): SmartTerminalFixture => ({
  version: '1.2.0-ct',
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
  // 测试状态：空标签栏 + 引导文案
  {
    id: 'SmartTerminal/EmptySessions',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        version: '1.2.0-ct',
        shells: linuxShells,
        draftShell: 'bash',
        sessions: [],
        globalMode: 'AskWrite',
      } satisfies SmartTerminalFixture,
    },
  },

  // 测试状态：多标签（本地 bash + SSH 标签），激活第二个
  {
    id: 'SmartTerminal/MultiTabs',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        version: '1.2.0-ct',
        shells: linuxShells,
        draftShell: 'bash',
        globalMode: 'AskWrite',
        sessions: [
          session({ sessionId: 'sess-1', title: '本地构建', shellDisplayName: 'Bash' }),
          session({
            sessionId: 'sess-2',
            title: '生产跳板',
            shellId: 'bash',
            shellDisplayName: 'Bash',
            launchType: 'ssh',
            remoteHost: '10.0.0.8',
            remoteUser: 'ops',
            status: 'running',
          }),
          session({ sessionId: 'sess-3', title: 'Zsh 调试', shellId: 'zsh', shellDisplayName: 'Zsh' }),
        ],
        activeId: 'sess-2',
        chunksBySession: {
          'sess-1': [{ index: 0, stream: 'stdout', text: 'build ok', at: AT }],
          'sess-2': [
            { index: 0, stream: 'system', text: 'URL 启动 SSH · ops@10.0.0.8', at: AT },
            { index: 1, stream: 'stdout', text: 'Welcome to Ubuntu', at: AT },
          ],
        },
        chunks: [
          { index: 0, stream: 'system', text: 'URL 启动 SSH · ops@10.0.0.8', at: AT },
          { index: 1, stream: 'stdout', text: 'Welcome to Ubuntu', at: AT },
        ],
        commands: [
          {
            commandId: 'cmd-ssh',
            sessionId: 'sess-2',
            commandRedacted: 'sshpass -e ssh -p 22 ops@***HOST***',
            risk: 'HighRisk',
            decision: 'Allow',
            matchedRule: 'launch:bootstrap',
            status: 'running',
            createdAt: AT,
          },
        ],
      } satisfies SmartTerminalFixture,
    },
  },

  // 测试状态：标签重命名输入态
  {
    id: 'SmartTerminal/TabRename',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        ...baseLive(),
        sessions: [session({ title: '旧名称' }), session({ sessionId: 'sess-2', title: '另一个' })],
        activeId: 'sess-1',
        renamingId: 'sess-1',
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
          { index: 2, stream: 'system', text: 'SSHPASS=***REDACTED***', at: AT },
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
        version: '1.2.0-ct',
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

  // 测试状态：多壳选择器（打开新建面板）
  {
    id: 'SmartTerminal/LinuxShellPicker',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        version: '1.2.0-ct',
        shells: linuxShells,
        draftShell: 'zsh',
        sessions: [session({ shellId: 'zsh', shellDisplayName: 'Zsh', title: 'Zsh 会话' })],
        activeId: 'sess-1',
        globalMode: 'AskWrite',
        showNewTabMenu: true,
        chunks: [{ index: 0, stream: 'stdout', text: 'zsh 5.9', at: AT }],
        commands: [],
      } satisfies SmartTerminalFixture,
    },
  },


  // 测试状态：密码深链「我已知晓风险」确认模态（未勾选时继续按钮禁用）
  {
    id: 'SmartTerminal/PasswordRiskAck',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        version: '1.2.1-ct',
        shells: linuxShells,
        draftShell: 'bash',
        sessions: [],
        globalMode: 'AskWrite',
        showPasswordAck: true,
        passwordAckChecked: false,
        passwordAckMode: 'always-ack',
        launchBanner: 'URL 启动：profile=ssh · host=10.0.0.1 · user=root · auth=sshpass · password=***REDACTED***',
        launchWarning: '检测到 URL 密码（内网过渡能力）。请确认风险后再继续；推荐改用 auth=key。',
        pendingLaunchSummary: 'profile=ssh · host=10.0.0.1 · user=root · auth=sshpass · password=***REDACTED***',
      } satisfies SmartTerminalFixture,
    },
  },

  // 测试状态：知情确认已勾选（继续可点）
  {
    id: 'SmartTerminal/PasswordRiskAckChecked',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        version: '1.2.1-ct',
        shells: linuxShells,
        draftShell: 'bash',
        sessions: [],
        globalMode: 'AskWrite',
        showPasswordAck: true,
        passwordAckChecked: true,
        passwordAckMode: 'intranet',
        launchBanner: 'URL 启动：profile=wsl-ssh · host=10.0.0.1 · user=ops · password=***REDACTED***',
        launchWarning: '内网模式：确认后将记住本浏览器的知情状态。',
      } satisfies SmartTerminalFixture,
    },
  },
  // 测试状态：URL 启动本地壳 + 命令列表横幅
  {
    id: 'SmartTerminal/UrlLaunchLocal',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        ...baseLive(),
        sessions: [session({ title: 'URL · local/bash' })],
        launchBanner: 'URL 启动：profile=local · shell=bash · cwd=/workspace · steps=2',
        chunks: [
          { index: 0, stream: 'system', text: 'URL 启动本地会话', at: AT },
          { index: 1, stream: 'stdout', text: 'hi', at: AT },
          { index: 2, stream: 'stdout', text: 'file1', at: AT },
        ],
      } satisfies SmartTerminalFixture,
    },
  },

  // 测试状态：URL SSH + 密码已脱敏警告（待连接）
  {
    id: 'SmartTerminal/UrlLaunchSshPending',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        version: '1.2.0-ct',
        shells: linuxShells,
        draftShell: 'bash',
        globalMode: 'AskWrite',
        sessions: [
          session({
            title: 'SSH · root@10.0.0.1',
            launchType: 'ssh',
            remoteHost: '10.0.0.1',
            remoteUser: 'root',
            status: 'running',
          }),
        ],
        activeId: 'sess-1',
        launchBanner: 'URL 启动：profile=ssh · host=10.0.0.1 · user=root · auth=sshpass · password=***REDACTED***',
        launchWarning: '地址栏曾含密码，已从 URL 擦除；密码仅短暂注入进程环境，不写审计原文。建议改用密钥。',
        chunks: [
          { index: 0, stream: 'system', text: '⚠ 启动凭证含密码：已做脱敏处理，密码不落审计原文。建议改用 SSH 密钥。', at: AT },
          { index: 1, stream: 'system', text: '$ sshpass -e ssh -p 22 root@***HOST***', at: AT },
        ],
        commands: [
          {
            commandId: 'cmd-boot',
            sessionId: 'sess-1',
            commandRedacted: 'sshpass -e ssh -p 22 root@***HOST***',
            risk: 'HighRisk',
            decision: 'Allow',
            matchedRule: 'launch:bootstrap',
            status: 'running',
            createdAt: AT,
          },
        ],
      } satisfies SmartTerminalFixture,
    },
  },
  // 测试状态：SSH 无密码 → 交互式遮罩输入（通用启动器默认）
  {
    id: 'SmartTerminal/InteractivePassword',
    component: SmartTerminalStoryHost,
    props: {
      fixture: {
        version: '1.2.1-ct',
        shells: linuxShells,
        draftShell: 'bash',
        sessions: [],
        globalMode: 'AskWrite',
        showInteractivePassword: true,
        interactivePasswordHost: '10.0.0.1',
        interactivePasswordUser: 'root',
        interactivePasswordPort: 22,
        interactivePasswordProfile: 'ssh',
        pendingLaunchSummary: 'profile=ssh · host=10.0.0.1 · user=root · auth=interactive · steps=1',
        launchBanner: 'URL 启动：profile=ssh · host=10.0.0.1 · user=root · auth=interactive · steps=1',
        launchWarning: '未提供密码：请交互输入（遮罩，仅内存）。推荐改用 auth=key。',
      } satisfies SmartTerminalFixture,
    },
  },
]
