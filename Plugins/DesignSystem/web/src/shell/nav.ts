/**
 * 工作台导航常量（v3：从 `DesignSystemView.vue` 原样搬出，供外壳与深链共用）。
 *
 * 唯一真源约束：标签 / 分组 / capability **一字不改**（既有的 14 入口文案与能力置灰
 * 是 e2e 与 AC6 的判据）。抽到独立模块是为了 `design/route.ts`（深链）能校验
 * section key 而不反向依赖根视图，避免"视图文件被路由文件 import"的循环。
 */
export type SectionKey =
  | 'projects'
  | 'studio'
  | 'color'
  | 'type'
  | 'scale'
  | 'motion'
  | 'themes'
  | 'brand'
  | 'components'
  | 'icons'
  | 'audit'
  | 'export'
  | 'releases'
  | 'showcase'

export interface NavItem {
  key: SectionKey
  label: string
  group: string
  /** 需要的后端能力；缺能力时入口置灰并说明原因（不是藏起来装没看见） */
  capability?: string
  skin?: boolean
}

export const NAV: NavItem[] = [
  { key: 'projects', label: '项目与生成', group: '建系统' },
  { key: 'studio', label: '令牌工作台', group: '建系统' },
  { key: 'color', label: '色彩实验室', group: '建系统' },
  { key: 'type', label: '排版标度', group: '建系统' },
  { key: 'scale', label: '尺度与密度', group: '建系统' },
  { key: 'motion', label: '阴影与动效', group: '建系统' },
  { key: 'themes', label: '主题实验室', group: '建系统' },
  { key: 'brand', label: '品牌资产', group: '建系统', capability: 'assets' },
  { key: 'icons', label: '图标库', group: '建系统' },
  { key: 'audit', label: '审计与门禁', group: '把质量' },
  { key: 'export', label: '导出交付', group: '把质量', capability: 'export' },
  { key: 'releases', label: '版本与对比', group: '把质量', capability: 'releases' },
  { key: 'components', label: '组件库', group: '看效果', skin: true },
  { key: 'showcase', label: '品牌展示页', group: '看效果', skin: true },
]

/** 深链 / 路由校验用的合法 section key 集（与 NAV 同源，不另列一份） */
export const SECTION_KEYS: readonly SectionKey[] = NAV.map((n) => n.key)

export function isSectionKey(v: unknown): v is SectionKey {
  return typeof v === 'string' && (SECTION_KEYS as readonly string[]).includes(v)
}
