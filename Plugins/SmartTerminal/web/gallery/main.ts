import { createApp, defineComponent, h, ref } from 'vue'
import SmartTerminalView from '../src/SmartTerminalView.vue'
import type { StoryId } from '../src/types'

const stories: { id: StoryId; label: string }[] = [
  { id: 'S0', label: 'S0 空态' },
  { id: 'S1', label: 'S1 提示符' },
  { id: 'S2', label: 'S2 多标签 / 长标题' },
  { id: 'S3', label: 'S3 新建本机' },
  { id: 'S4', label: 'S4 SSH 密钥' },
  { id: 'S5', label: 'S5 交互密码' },
  { id: 'S6', label: 'S6 写操作审批' },
  { id: 'S7', label: 'S7 高风险审批' },
  { id: 'S8', label: 'S8 断线重连' },
  { id: 'S9', label: 'S9 助手对话' },
  { id: 'S10', label: 'S10 片段与历史' },
  { id: 'S11-loading', label: 'S11 加载' },
  { id: 'S11-error', label: 'S11 错误' },
  { id: 'S11-401', label: 'S11 401' },
  { id: 'S-R', label: 'S-R 刷新恢复' },
]

const App = defineComponent({
  setup() {
    const params = new URLSearchParams(location.search)
    const initial = (params.get('story') as StoryId) || 'S0'
    const story = ref<StoryId>(stories.some((s) => s.id === initial) ? initial : 'S0')
    function pick(id: StoryId) {
      story.value = id
      const url = new URL(location.href)
      url.searchParams.set('story', id)
      history.replaceState(null, '', url)
    }
    return () =>
      h('div', { class: 'st-gallery' }, [
        h('nav', { class: 'st-gallery-nav' }, [
          h('strong', '智能终端 v2 · 画廊'),
          ...stories.map((s) =>
            h(
              'button',
              {
                class: story.value === s.id ? 'on' : '',
                type: 'button',
                onClick: () => pick(s.id),
              },
              s.label,
            ),
          ),
        ]),
        h('div', { class: 'st-gallery-stage' }, [h(SmartTerminalView, { story: story.value, key: story.value })]),
      ])
  },
})

const css = document.createElement('style')
css.textContent = `
  html,body,#app{height:100%;margin:0;background:#0B0908;color:#F4F0E7;font:13px/1.4 Inter,system-ui,sans-serif}
  .st-gallery{display:flex;flex-direction:column;height:100%}
  .st-gallery-nav{display:flex;flex-wrap:wrap;gap:6px;align-items:center;padding:8px 12px;border-bottom:1px solid rgba(245,240,231,.12);background:#14110F}
  .st-gallery-nav button{background:transparent;border:1px solid rgba(245,240,231,.16);color:#B5AB9F;padding:4px 8px;border-radius:4px;cursor:pointer}
  .st-gallery-nav button.on{color:#F9BC4E;border-color:#F59E0B}
  .st-gallery-stage{flex:1;min-height:0}
`
document.head.appendChild(css)

createApp(App).mount('#app')
