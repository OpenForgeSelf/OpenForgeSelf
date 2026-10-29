import { h } from 'vue'
import ForgeCard from './ForgeCard.vue'
import type { ComponentStory } from '../../ct/types'

/**
 * ForgeCard 故事变体（CT 画廊用）
 *
 * story 只描述「传什么 props / 插槽放什么」，不另写 demo 页。
 * 颜色一律 token 引用（本文件在 src/ 下，与组件同受设计守卫扫描，禁裸色值）。
 */
const body = (title: string, sub: string) => () =>
  h('div', { style: 'display:flex;flex-direction:column;gap:4px;min-width:220px' }, [
    h('span', { style: 'font-size:14px;color:var(--fg-primary)' }, title),
    h('span', {
      style: 'font-size:10px;letter-spacing:.04em;color:var(--fg-tertiary);font-family:var(--font-mono)',
    }, sub),
  ])

export const stories: ComponentStory[] = [
  // 测试状态：默认静态卡片（不可交互、padding 16px）
  { id: 'ForgeCard/Default', component: ForgeCard, slots: { default: body('器物玻璃卡片', 'forge-card · 默认态') } },

  // 测试状态：可交互卡片（hover 出现金边 + 指针），点击/回车都能触发 onClick
  {
    id: 'ForgeCard/Interactive',
    component: ForgeCard,
    props: { interactive: true },
    slots: { default: body('可交互卡片', 'hover 出现金边与抬升') },
  },

  // 测试状态：自定义内边距 20px（设计稿 glass 卡 padding 上限）
  { id: 'ForgeCard/Padding20', component: ForgeCard, props: { padding: 20 }, slots: { default: body('内边距 20px', 'padding prop 落到内联样式') } },
]
