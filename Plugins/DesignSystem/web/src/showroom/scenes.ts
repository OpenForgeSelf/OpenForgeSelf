/**
 * 场景与页面注册表（03-plan §U / FR10 的**唯一真源**）。
 *
 * 展厅的「场景页签」「页面页签」「设备框」「舞台 `data-scene`/`data-page`」全部读这里，
 * 组件里不许另列一份 —— 同一件事两份真源必然漂移（本插件反复踩过的坑）。
 *
 * 本文件保持纯模块：页面只登记 `component` 字符串键，实际 SFC 由 `Showroom.vue` 映射，
 * 这样 vitest 能直接断言注册表（不必加载 .vue）。
 *
 * 标签即判据（§U DOM 契约）：
 * - 场景页签名恰为 `后台/中台` `工具/工作台` `状态板` `官网/落地页` `移动端 H5`；
 * - 后台场景的页面页签名恰为 `仪表盘` `列表` `表单` `详情` `设置`。
 */

/** 设备框三档（FR11） */
export type DeviceId = 'desktop' | 'tablet' | 'mobile'

export interface DeviceOption {
  id: DeviceId
  label: string
  /** 舞台框宽度（px，纯 CSS 外框，不画假系统栏） */
  width: number
}

export const DEVICES: readonly DeviceOption[] = [
  { id: 'desktop', label: '桌面', width: 1280 },
  { id: 'tablet', label: '平板', width: 820 },
  { id: 'mobile', label: '手机', width: 390 },
]

export function deviceById(id: string): DeviceOption | null {
  return DEVICES.find((d) => d.id === id) ?? null
}

/** 一页模特（`data-mq-page` 即 `id`；`component` 为 SFC 键） */
export interface ScenePage {
  id: string
  label: string
  component: string
}

export interface Scene {
  id: string
  label: string
  /** 该场景的默认设备；`mobile` 场景强制手机框（AC15） */
  device: DeviceId
  pages: readonly ScenePage[]
}

/**
 * 场景注册表。切片 A 落 `admin` 与 `board`（对应 A 片 6 个模特）；
 * 切片 B 补 `workbench / landing / mobile`（03-plan 步骤9），本文件是唯一落点。
 */
export const SCENES: readonly Scene[] = [
  {
    id: 'admin',
    label: '后台/中台',
    device: 'desktop',
    pages: [
      { id: 'admin-dashboard', label: '仪表盘', component: 'AdminDashboard' },
      { id: 'admin-list', label: '列表', component: 'AdminList' },
      { id: 'admin-form', label: '表单', component: 'AdminForm' },
      { id: 'admin-detail', label: '详情', component: 'AdminDetail' },
      { id: 'admin-settings', label: '设置', component: 'AdminSettings' },
    ],
  },
  {
    id: 'board',
    label: '状态板',
    device: 'desktop',
    pages: [{ id: 'status-board', label: '概览', component: 'StatusBoard' }],
  },
  {
    id: 'workbench',
    label: '工具/工作台',
    device: 'desktop',
    pages: [{ id: 'workbench-editor', label: '编辑器', component: 'WorkbenchEditor' }],
  },
  {
    id: 'landing',
    label: '官网/落地页',
    device: 'desktop',
    pages: [{ id: 'landing-home', label: '首页', component: 'LandingHome' }],
  },
  {
    id: 'mobile',
    label: '移动端 H5',
    // FR10：mobile 场景强制手机设备框（`forcesMobile` 即判据）
    device: 'mobile',
    pages: [{ id: 'mobile-home', label: '首页', component: 'MobileHome' }],
  },
]

export function sceneById(id: string): Scene | null {
  return SCENES.find((s) => s.id === id) ?? null
}

/** 页 id → 所属场景与其页面（深链还原用；找不到回 null，不编造） */
export function findPage(pageId: string): { scene: Scene; page: ScenePage } | null {
  for (const scene of SCENES) {
    const page = scene.pages.find((p) => p.id === pageId)
    if (page) return { scene, page }
  }
  return null
}

/** 场景首页（切场景时的落点） */
export function firstPage(scene: Scene): ScenePage {
  return scene.pages[0]
}

/** 某场景是否强制手机设备框（FR10：mobile 强制） */
export function forcesMobile(scene: Scene): boolean {
  return scene.device === 'mobile'
}