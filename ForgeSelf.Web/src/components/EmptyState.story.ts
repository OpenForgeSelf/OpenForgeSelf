import { h } from 'vue'
import EmptyState from './EmptyState.vue'
import type { ComponentStory } from '../../ct/types'

/**
 * EmptyState 故事变体（CT 画廊用）
 *
 * icon 接的是 SVG 片段字符串（组件内 v-html 渲染）：用 currentColor 取主题色，禁裸色值。
 */
const ICON =
  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round">' +
  '<path d="M12 5v14M5 12h14"/></svg>'

const actionButton = () => () =>
  h('button', {
    type: 'button',
    style:
      'padding:6px 14px;border-radius:var(--radius-md,8px);border:1px solid var(--border-brand);' +
      'background:transparent;color:var(--gold-400);font-size:12px;cursor:pointer',
  }, '去创建')

export const stories: ComponentStory[] = [
  // 测试状态：完整空态（图标 + 主文案 + 副文案 + 引导按钮）
  {
    id: 'EmptyState/Full',
    component: EmptyState,
    props: { icon: ICON, title: '还没有任何器物', sub: 'EMPTY · 先铸造第一件' },
    slots: { action: actionButton() },
  },

  // 测试状态：只有主文案（无图标、无副文案、无引导），验证可选块全部隐藏
  { id: 'EmptyState/TitleOnly', component: EmptyState, props: { title: '暂无数据' } },
]
