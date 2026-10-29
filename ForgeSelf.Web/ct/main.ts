import { createApp, h, reactive, type App } from 'vue'
import { createPinia } from 'pinia'
import { navConfig, stories } from './stories'
import type { ComponentStory } from './types'

/* 全局样式装配：顺序与 src/main.ts 保持一致（后加载优先级更高）。
   组件自身的样式由 SFC <style> / unplugin-vue-components 按需注入，这里只补宿主级底座，
   保证画廊里的组件与真实页面「同一套 CSS 管线」——否则截图基线没有意义。 */
import 'element-plus/theme-chalk/dark/css-vars.css'
import 'element-plus/theme-chalk/el-message-box.css'
import 'element-plus/theme-chalk/el-message.css'
import '../src/styles/themes/workshop-forge.css'
import '../src/styles/tailwind.css'
import '../src/styles/themes/bg-image-mode.css'

const registry = new Map<string, ComponentStory>()
for (const s of stories) registry.set(s.id, s)

/* 重复 id 会被 Map 静默覆盖（表现为「点了没反应 / 截图是别人的」），启动期直接炸掉便于定位 */
if (registry.size !== stories.length) {
  const seen = new Set<string>()
  const dup = stories.map((s) => s.id).filter((id) => (seen.has(id) ? true : (seen.add(id), false)))
  throw new Error(`[ct] 重复的 story id：${[...new Set(dup)].join(', ')}`)
}

let currentApp: App | null = null
let mountEl: HTMLElement | null = null
let currentProps: Record<string, unknown> = {}

/**
 * 卸载当前故事。
 * 除了 unmount 组件，还必须清理 Element Plus 通过 Teleport 挂到 body 的浮层：
 * 它们不随组件销毁移除，会盖在下一个故事的截图上（只能靠肉眼猜到的“脏基线”来源）。
 */
function unmountCurrent(): void {
  if (currentApp) {
    currentApp.unmount()
    currentApp = null
  }
  document
    .querySelectorAll('body > .el-overlay, body > .el-dialog__wrapper, body > .v-modal')
    .forEach((n) => n.remove())
  if (mountEl) {
    mountEl.remove()
    mountEl = null
  }
}

/**
 * 浏览器侧挂载契约（供 Playwright 与人类浏览共用）：
 *   window.mountStory(id, props?)   渲染指定变体
 *   window.setStoryProps(patch)     浅合并 props（用于「先挂载、再打开弹窗」这类分步状态）
 *   window.unmountStory()           卸载并清理浮层
 */
function mountStory(id: string, props?: Record<string, unknown>): void {
  const s = registry.get(id)
  if (!s) throw new Error(`Unknown story: ${id}`)
  unmountCurrent()

  mountEl = document.createElement('div')
  mountEl.classList.add('gal-preview')
  document.body.appendChild(mountEl)

  currentProps = reactive({ ...(props ?? s.props ?? {}) })
  const app = createApp({ render: () => h(s.component, currentProps, s.slots) })
  // 与主应用一致提供 Pinia（不提供 router：依赖路由的组件应改为 props/桩注入，或暂不接入）
  app.use(createPinia())
  app.mount(mountEl)
  currentApp = app
}

function setStoryProps(patch: Record<string, unknown>): void {
  Object.assign(currentProps, patch)
}

const bridge = window as unknown as Record<string, unknown>
bridge.mountStory = mountStory
bridge.setStoryProps = setStoryProps
bridge.unmountStory = unmountCurrent

/* ── 侧栏导航（分组/标题/排序的配置点 = ct/stories.ts 的 navConfig） ── */

/** 按 story id 的 `/` 前缀分组；顺序遵循 navConfig.groups，未配置的分组追加在末尾 */
function buildNavGroups(): Array<[string, ComponentStory[]]> {
  const groups = new Map<string, ComponentStory[]>()
  for (const s of stories) {
    const key = s.id.includes('/') ? s.id.split('/')[0] : '(Other)'
    if (!groups.has(key)) groups.set(key, [])
    groups.get(key)!.push(s)
  }
  const configured = navConfig.groups.map((g) => g.key).filter((k) => groups.has(k))
  const rest = [...groups.keys()].filter((k) => !configured.includes(k))
  return [...configured, ...rest].map((key): [string, ComponentStory[]] => [key, groups.get(key)!])
}

function groupTitle(key: string): string {
  return navConfig.groups.find((g) => g.key === key)?.title ?? key
}

function setActiveStory(id: string): void {
  document.querySelectorAll<HTMLElement>('#gallery-nav li[data-story-id]').forEach((el) => {
    el.classList.toggle('active', el.dataset.storyId === id)
  })
}

/** 切换预览；默认同步地址栏（刷新后停在同一故事）。初始挂载不写地址，保持根路径干净 */
function previewStory(id: string, syncUrl = true): void {
  mountStory(id)
  setActiveStory(id)
  if (!syncUrl) return
  const url = new URL(window.location.href)
  url.searchParams.set('story', id)
  window.history.replaceState(null, '', url)
}

/** 折叠模式下展开指定故事所在分组（深链 / 默认选中时用） */
function expandGroupOf(id: string): void {
  const li = document.querySelector<HTMLElement>(`#gallery-nav li[data-story-id="${CSS.escape(id)}"]`)
  const list = li?.closest('.nav-group-list') as HTMLElement | null
  if (!list || list.style.display !== 'none') return
  list.style.display = ''
  const toggle = list.previousElementSibling?.querySelector('.nav-toggle')
  if (toggle) toggle.textContent = '▾'
}

function renderGalleryNav(): void {
  const nav = document.getElementById('gallery-nav')
  if (!nav) return
  document.body.classList.add('has-nav')
  nav.innerHTML = `<h3>${navConfig.title}</h3>`

  /* 故事总数超过阈值时分组默认收起，打开即清爽 */
  const defaultCollapsed = stories.length > navConfig.collapseWhenMoreThan

  for (const [groupKey, items] of buildNavGroups()) {
    const groupEl = document.createElement('div')
    groupEl.className = 'nav-group'

    const header = document.createElement('div')
    header.className = 'nav-group-header'
    const toggle = document.createElement('span')
    toggle.className = 'nav-toggle'
    toggle.textContent = defaultCollapsed ? '▸' : '▾'
    const title = document.createElement('span')
    title.className = 'nav-group-title'
    title.textContent = `${groupTitle(groupKey)} (${items.length})`
    title.title = groupKey
    header.append(toggle, title)
    groupEl.appendChild(header)

    const ul = document.createElement('ul')
    ul.className = 'nav-group-list'
    if (defaultCollapsed) ul.style.display = 'none'

    for (const s of items) {
      const li = document.createElement('li')
      li.textContent = s.id.includes('/') ? s.id.slice(s.id.indexOf('/') + 1) : s.id
      li.title = s.id
      li.dataset.storyId = s.id
      li.addEventListener('click', () => previewStory(s.id))
      ul.appendChild(li)
    }
    groupEl.appendChild(ul)

    header.addEventListener('click', () => {
      const hidden = ul.style.display === 'none'
      ul.style.display = hidden ? '' : 'none'
      toggle.textContent = hidden ? '▾' : '▸'
    })

    nav.appendChild(groupEl)
  }
}

/* ── 深链单故事：?story=<id>（人类浏览保留侧栏；CT 加 &chrome=0 关闭导航保证基线干净） ── */
function renderUnknown(id: string): void {
  unmountCurrent()
  const el = document.createElement('div')
  el.className = 'gal-preview'
  el.innerHTML =
    '<div style="line-height:1.8">' +
    '<div style="color:#c0392b;font-weight:600;margin-bottom:8px">Unknown story: ' +
    `${id}` +
    '</div>' +
    '<div style="opacity:.75;margin-bottom:6px">可用故事：</div>' +
    stories.map((s) => `<div style="opacity:.9">· ${s.id}</div>`).join('') +
    '</div>'
  document.body.appendChild(el)
}

const params = new URLSearchParams(window.location.search)
/* chrome=0：只渲染组件本身（CT 截图用），不生成侧栏与左侧留白 */
if (params.get('chrome') !== '0') renderGalleryNav()

if (params.has('story')) {
  const id = params.get('story')!
  if (registry.has(id)) {
    mountStory(id)
    setActiveStory(id)
    expandGroupOf(id)
  } else {
    renderUnknown(id)
  }
} else if (stories.length > 0) {
  previewStory(stories[0].id, false)
  expandGroupOf(stories[0].id)
}
