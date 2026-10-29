import { h } from 'vue'
import PageHead from './PageHead.vue'
import type { ComponentStory } from '../../ct/types'

/**
 * PageHead 故事变体（CT 画廊用）
 *
 * 覆盖两个分支：eyebrow 有/无；actions 插槽有/无。
 */
const actionButton = (label: string) => () =>
  h('button', {
    type: 'button',
    style:
      'padding:6px 14px;border-radius:var(--radius-md,8px);border:1px solid var(--border-brand);' +
      'background:transparent;color:var(--gold-400);font-size:12px;cursor:pointer',
  }, label)

export const stories: ComponentStory[] = [
  // 测试状态：完整页头（eyebrow + 标题 + 右侧操作按钮）
  {
    id: 'PageHead/WithEyebrow',
    component: PageHead,
    props: { eyebrow: 'System Monitor', title: '资源 · 进程 · 日志' },
    slots: { actions: actionButton('刷新') },
  },

  // 测试状态：只有标题（无 eyebrow、无操作区），验证可选块整体不渲染
  { id: 'PageHead/TitleOnly', component: PageHead, props: { title: '仅标题' } },
]
