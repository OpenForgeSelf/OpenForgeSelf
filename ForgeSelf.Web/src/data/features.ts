// 功能单一真源（Single Source of Truth）
// 所有「功能列表」展示（运行时「所有功能」页）与文档均派生自此文件。
// 新增功能时必须在此登记，并填写 signals（代码产物基础键），
// 否则 scripts/check-features.mjs 会在 CI 中报红。
//
// signals 说明（值为「代码产物的精确名称/目录名」，用于脚本自动校验）：
//   views:       前端页面对应 `src/views/<key>.vue`（注意文件名本身不统一，须写精确 stem）
//   controllers: 后端控制器对应 `Controllers/<key>Controller.cs`（或 UnifiedAI/ 子目录）
//   plugins:     后端插件对应 `Plugins/<key>/` 目录
// 注意：每个功能对象的 signals 必须放在最后一项，便于校验脚本正则解析。
//
// path:        功能主页面路由（「打开」按钮目标）。无独立页面（如仅后端插件）填 null。
// configPath:  功能配置页路由（「配置」按钮目标）。省略则回落到 path。

import type { PluginMenuContribution } from '@/types/plugin'

export type CategoryKey = 'tools' | 'ai' | 'system' | 'orchestration' | 'dev'

export interface FeatureSignals {
  views?: string[]
  controllers?: string[]
  plugins?: string[]
}

export interface FeatureItem {
  id: string
  name: string
  icon: string
  category: CategoryKey
  categoryLabel: string
  color: string
  bgColor: string
  description: string
  stats: string
  extraInfo?: string
  enabled: boolean
  path?: string | null
  configPath?: string
  signals: FeatureSignals
}

export const categories = [
  { key: 'all', label: '全部' },
  { key: 'tools', label: '工具类' },
  { key: 'ai', label: 'AI类' },
  { key: 'system', label: '系统类' },
  { key: 'orchestration', label: '编排类' },
  { key: 'dev', label: '开发类' },
] as const

export const features: FeatureItem[] = [
  {
    id: 'ai-agent',
    name: 'AI Agent',
    icon: 'bot',
    category: 'ai',
    categoryLabel: 'AI',
    color: '#F59E0B',
    bgColor: 'rgba(245, 158, 11, 0.12)',
    description: 'AI代理核心，自然语言操控工具，自动规划执行复杂任务链',
    stats: '50+ 工具函数',
    extraInfo: '支持多模型',
    enabled: true,
    path: '/ai-agent',
    configPath: '/settings',
    signals: { plugins: ['AIAgent'], views: ['AgentView'] },
  },
  {
    id: 'quick-links',
    name: '快捷链接',
    icon: 'link',
    category: 'tools',
    categoryLabel: '工具',
    color: 'var(--el-color-info)',
    bgColor: 'rgba(29, 78, 216, 0.12)',
    description: '一键打开常用网址，支持分组管理',
    stats: '12 个链接',
    enabled: true,
    path: '/quick-links',
    signals: { plugins: ['QuickLinks'], views: ['QuickLinksView'] },
  },
  {
    id: 'text-tools',
    name: '文本工具',
    icon: 'type',
    category: 'tools',
    categoryLabel: '工具',
    color: 'var(--el-color-info)',
    bgColor: 'rgba(29, 78, 216, 0.12)',
    description: '格式化、编码转换、哈希计算等文本处理工具集',
    stats: '8 个工具',
    enabled: true,
    path: '/text-tools',
    signals: { plugins: ['TextTools'], views: ['TextToolsView'] },
  },
  {
    id: 'file-tools',
    name: '文件工具',
    icon: 'folder-open',
    category: 'tools',
    categoryLabel: '工具',
    color: 'var(--el-color-info)',
    bgColor: 'rgba(29, 78, 216, 0.12)',
    description: '批量重命名、清理、压缩解压等文件管理工具',
    stats: '5 个工具',
    enabled: true,
    path: '/file-tools',
    signals: { plugins: ['FileTools'], views: ['FileToolsView'] },
  },
  {
    id: 'system-monitor',
    name: '系统监控',
    icon: 'monitor',
    category: 'system',
    categoryLabel: '系统',
    color: 'var(--el-color-success)',
    bgColor: 'rgba(16, 185, 129, 0.12)',
    description: 'CPU、内存、磁盘实时监控，进程管理',
    stats: '5 个监控项',
    enabled: true,
    path: '/system-monitor',
    signals: { plugins: ['SystemMonitor'], views: ['SystemMonitorView'] },
  },
  {
    id: 'workflow',
    name: '工作流引擎',
    icon: 'git-branch',
    category: 'orchestration',
    categoryLabel: '编排',
    color: '#A78BFA',
    bgColor: 'rgba(167, 139, 250, 0.12)',
    description: '多工具编排，任务链自动执行，支持条件分支',
    stats: '12 个工作流',
    enabled: true,
    path: '/workflows',
    signals: { plugins: ['WorkflowEngine'], views: ['WorkflowLibrary'] },
  },
  {
    id: 'scheduler',
    name: '定时任务',
    icon: 'clock',
    category: 'orchestration',
    categoryLabel: '编排',
    color: '#2DD4BF',
    bgColor: 'rgba(45, 212, 191, 0.12)',
    description: 'Cron表达式管理，定时执行脚本和工作流',
    stats: '5 个任务',
    enabled: true,
    path: null,
    signals: { plugins: ['Scheduler'] },
  },
  {
    id: 'script-runner',
    name: '脚本运行器',
    icon: 'terminal',
    category: 'dev',
    categoryLabel: '开发',
    color: '#2DD4BF',
    bgColor: 'rgba(45, 212, 191, 0.12)',
    description: 'PowerShell/Python/Node脚本运行，支持AI生成',
    stats: '支持 3 种语言',
    enabled: true,
    path: '/script-runner',
    signals: { plugins: ['ScriptRunner'], views: ['ScriptLibrary'] },
  },
  {
    id: 'dev-tools',
    name: '开发者工具箱',
    icon: 'wrench',
    category: 'dev',
    categoryLabel: '开发',
    color: '#2DD4BF',
    bgColor: 'rgba(45, 212, 191, 0.12)',
    description: 'JSON/YAML/Base64/哈希/正则/时间戳等25+开发工具',
    stats: '25+ 工具',
    enabled: true,
    path: '/dev-tools',
    signals: { plugins: ['DevTools'], views: ['DevToolsView'] },
  },
  {
    id: 'proxy-capture',
    name: '抓包代理',
    icon: 'network',
    category: 'dev',
    categoryLabel: '开发',
    color: '#0EA5E9',
    bgColor: 'rgba(14, 165, 233, 0.12)',
    description: '反向代理抓包监听：完整记录 HTTP/HTTPS 请求（HTTPS 经 MITM 解密），可原样转发到目标地址',
    stats: '抓包监听',
    enabled: true,
    path: '/capture',
    signals: { plugins: ['ProxyCapture'], views: ['CaptureView'] },
  },
  {
    id: 'chat',
    name: '核心聊天',
    icon: 'message-circle',
    category: 'ai',
    categoryLabel: 'AI',
    color: '#6366F1',
    bgColor: 'rgba(99, 102, 241, 0.12)',
    description: '多轮对话聊天页面，对接 AI 提供方实时流式回复',
    stats: '实时流式',
    enabled: true,
    path: '/chat',
    configPath: '/settings',
    signals: { views: ['ChatView'], controllers: ['Chat'] },
  },
  {
    id: 'chat-records',
    name: '聊天记录',
    icon: 'message-square',
    category: 'ai',
    categoryLabel: 'AI',
    color: '#8B5CF6',
    bgColor: 'rgba(139, 92, 246, 0.12)',
    description: '查看与管理历史对话记录，支持回顾与检索',
    stats: '持久化存储',
    enabled: true,
    path: '/chat-records',
    signals: { views: ['ChatRecordsView'], controllers: ['ChatRecords'] },
  },
  {
    id: 'prompts',
    name: '提示词',
    icon: 'file-text',
    category: 'ai',
    categoryLabel: 'AI',
    color: '#EC4899',
    bgColor: 'rgba(236, 72, 153, 0.12)',
    description: '管理可复用的提示词模板',
    stats: '提示词库',
    enabled: true,
    path: '/prompts',
    configPath: '/settings',
    signals: { views: ['PromptsView'] },
  },
  {
    id: 'skills',
    name: '技能',
    icon: 'sparkles',
    category: 'ai',
    categoryLabel: 'AI',
    color: '#14B8A6',
    bgColor: 'rgba(20, 184, 166, 0.12)',
    description: '管理与编排 Agent 技能',
    stats: '技能中心',
    enabled: true,
    path: '/skills',
    configPath: '/settings',
    signals: { views: ['SkillsView'], controllers: ['Skills'] },
  },
  {
    id: 'mcp',
    name: 'MCP 工具',
    icon: 'plug',
    category: 'ai',
    categoryLabel: 'AI',
    color: '#0EA5E9',
    bgColor: 'rgba(14, 165, 233, 0.12)',
    description: '接入 MCP 工具服务器，扩展 Agent 工具集',
    stats: 'MCP 集成',
    enabled: true,
    path: '/mcp-tools',
    configPath: '/settings',
    signals: { views: ['McpToolsView'], controllers: ['Mcp'] },
  },
  {
    id: 'memory',
    name: '记忆',
    icon: 'brain',
    category: 'ai',
    categoryLabel: 'AI',
    color: '#A855F7',
    bgColor: 'rgba(168, 85, 247, 0.12)',
    description: '长期记忆系统，跨会话沉淀知识与上下文',
    stats: '记忆系统',
    enabled: true,
    path: '/memory',
    configPath: '/settings',
    signals: { plugins: ['MemorySystem'], views: ['MemoryView'] },
  },
  {
    id: 'agents',
    name: '多 Agent 管理',
    icon: 'users',
    category: 'ai',
    categoryLabel: 'AI',
    color: '#F43F5E',
    bgColor: 'rgba(244, 63, 94, 0.12)',
    description: '管理多个专业 Agent 与其协作',
    stats: 'Agent 管理',
    enabled: true,
    path: '/agents',
    signals: { views: ['AgentsManageView'] },
  },
  {
    id: 'code-snippets',
    name: '代码片段库',
    icon: 'code',
    category: 'dev',
    categoryLabel: '开发',
    color: '#10B981',
    bgColor: 'rgba(16, 185, 129, 0.12)',
    description: '个人代码片段管理与复用',
    stats: '代码片段',
    enabled: true,
    path: '/code-snippets',
    signals: { views: ['CodeSnippetsView'] },
  },
  {
    id: 'todo',
    name: '待办事项',
    icon: 'check-square',
    category: 'tools',
    categoryLabel: '工具',
    color: '#3B82F6',
    bgColor: 'rgba(59, 130, 246, 0.12)',
    description: '本地待办清单，跟踪个人任务',
    stats: '待办跟踪',
    enabled: true,
    path: '/todo',
    signals: { plugins: ['TodoTracker'], views: ['TodoView'] },
  },
  {
    id: 'profile',
    name: '个人档案',
    icon: 'user',
    category: 'system',
    categoryLabel: '系统',
    color: '#F97316',
    bgColor: 'rgba(249, 115, 22, 0.12)',
    description: '个人能力画像与成长曲线，沉淀使用数据',
    stats: '能力沉淀',
    enabled: true,
    path: '/profile',
    signals: { views: ['ProfileView', 'PersonalLibraryView'] },
  },
  {
    id: 'plugins',
    name: '插件商店',
    icon: 'package',
    category: 'system',
    categoryLabel: '系统',
    color: '#84CC16',
    bgColor: 'rgba(132, 204, 22, 0.12)',
    description: '浏览、安装、更新与管理插件生态',
    stats: '插件生态',
    enabled: true,
    path: '/plugins',
    signals: {
      views: ['PluginStore', 'PluginUpdates', 'PluginImportExport', 'PluginScaffolder', 'PluginDetail'],
      controllers: ['Plugin'],
    },
  },
  {
    id: 'settings',
    name: '设置',
    icon: 'settings',
    category: 'system',
    categoryLabel: '系统',
    color: '#64748B',
    bgColor: 'rgba(100, 116, 139, 0.12)',
    description: 'AI 提供方、API 服务、背景图与端口安全配置',
    stats: '系统设置',
    enabled: true,
    path: '/settings',
    configPath: '/settings',
    signals: {
      views: ['SettingsView'],
      controllers: ['AIProvider', 'AIModel', 'ApiServer', 'PortConfiguration', 'Settings'],
    },
  },
]

/**
 * 「内置特性 + 运行期清单补充」合并：
 * 以硬编码 features 为 fallback，把插件清单贡献出的菜单（未被内置特性覆盖的路由）
 * 追加为补充特性。若清单未就绪/为空（contributions 为空数组），等价于直接返回 features，
 * 从而不破坏现有界面。
 *
 * 去重依据是 path/route：插件清单 route 与内置 path 相同（如 /memory）时跳过，
 * 避免已硬编码的插件菜单重复出现。
 */
export function mergeFeatureList(contributions: PluginMenuContribution[]): FeatureItem[] {
  const existingPaths = new Set<string>(features.flatMap((f) => (f.path ? [f.path] : [])))

  const supplemental: FeatureItem[] = contributions
    .filter((c) => c.route != null && !existingPaths.has(c.route))
    .map((c) => ({
      id: c.id,
      name: c.menu,
      icon: c.icon ?? 'package',
      category: 'tools',
      categoryLabel: '插件',
      color: 'var(--el-color-info)',
      bgColor: 'rgba(29, 78, 216, 0.12)',
      description: '由插件清单贡献的功能',
      stats: '插件贡献',
      enabled: true,
      path: c.route,
      signals: { plugins: [c.id], views: c.views },
    }))

  return [...features, ...supplemental]
}
