import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import ChatWidget from './ChatWidget.vue'
import { defaultConfig } from '../config'

/* jsdom **不实现** Element.prototype.scrollTo（它连布局都不跑，更没有滚动）。
   组件滚到最新时会调它并抛 TypeError，这个错飘在 Promise 外面变成 unhandled rejection ——
   结果是「用例全绿，文件却判失败」。先补一个空实现，让滚动这条路径走得通。 */
if (typeof Element !== 'undefined' && !Element.prototype.scrollTo) {
  Element.prototype.scrollTo = () => {
    /* 无布局环境，无需真的滚动 */
  }
}

/**
 * 聊天区状态矩阵单测（041 状态矩阵专项）—— 组件级那四类：
 *   空状态 / 加载中 / 成功（有消息）/ 错误。
 *
 * 与 MessageItem.states.test.ts 的分工：那边管**一条消息**自己的态
 * （发送中 / 流式 / 失败 / 中断 / 低置信度 / 权限不足），
 * 这边管**整个消息区**的态 —— 尤其「空」和「加载中」这两个只在
 * 刚打开、还没说话时才出现的态，它们是用户对产品的第一印象，也最容易被跳过。
 */

/**
 * 测试配置：关掉持久化（否则会读上一次跑留下的 localStorage），
 * 关掉自动刷新定时器（否则 jsdom 里会挂一个跑不完的 interval）。
 *
 * ★ 默认 **不配 welcome**：这是照着 AIAgent 宿主的真实配置来的（AiAgentView 没给 welcome）。
 *   组件源那套默认的欢迎语是「空会话的兜底文案」，宿主没配时这层兜底就消失了 ——
 *   于是 0 条消息 = 一片纯白。空状态正是补这个洞：
 *   有用欢迎语的宿主继续用欢迎语（那时屏上有内容，不该再叠一个空态），
 *   没用的宿主则由空状态接住。
 */
function makeConfig(extra: Record<string, unknown> = {}) {
  return {
    ...defaultConfig,
    persist: false,
    refreshInterval: 0,
    inline: true,
    autoFocus: false,
    welcome: '',
    ...extra,
  }
}

async function mountWidget(config: Record<string, unknown>) {
  const wrapper = mount(ChatWidget, {
    props: { config, control: { open: true } },
    attachTo: document.body,
  })
  await nextTick()
  await nextTick()
  return wrapper
}

beforeEach(() => {
  try {
    localStorage.clear()
  } catch (e) {
    /* jsdom 有 localStorage；真没有也无所谓，测试不依赖它 */
  }
})
afterEach(() => {
  vi.useRealTimers()
})

describe('消息区状态矩阵：空状态', () => {
  it('0 条消息时显示空状态（以前这里是一片纯白，零引导）', async () => {
    const w = await mountWidget(makeConfig())
    expect(w.find('.aiw-empty').exists()).toBe(true)
  })

  it('空状态要有标题与引导文案 —— 只放个图标等于没说', async () => {
    const w = await mountWidget(makeConfig())
    expect(w.find('.aiw-empty__t').exists()).toBe(true)
    expect(w.find('.aiw-empty__d').exists()).toBe(true)
  })

  it('宿主给了文案就用宿主的（组件零业务，不替宿主决定说什么）', async () => {
    const w = await mountWidget(makeConfig({ emptyTitle: '开始一个新会话', emptyHint: '选中文件后再问' }))
    expect(w.get('.aiw-empty__t').text()).toBe('开始一个新会话')
    expect(w.get('.aiw-empty__d').text()).toBe('选中文件后再问')
  })

  it('有了消息，空状态立刻让位（不能和消息挤在一起）', async () => {
    const w = await mountWidget(makeConfig())
    expect(w.find('.aiw-empty').exists()).toBe(true)
    ;(w.vm as unknown as { push: (m: Record<string, unknown>) => void }).push({
      id: 'm1',
      role: 'assistant',
      content: '你好',
      time: Date.now(),
      status: 'done',
    })
    await nextTick()
    await nextTick()
    expect(w.find('.aiw-empty').exists()).toBe(false)
    expect(w.find('.aiw-msg').exists()).toBe(true)
  })

  it('配了欢迎语的宿主不叠空状态 —— 那时屏上有内容，两套引导挤一起只是打架', async () => {
    const w = await mountWidget(makeConfig({ welcome: '你好，我是智能助手' }))
    expect(w.find('.aiw-msg').exists()).toBe(true)
    expect(w.find('.aiw-empty').exists()).toBe(false)
  })
})

describe('消息区状态矩阵：加载中', () => {
  it('config.loading 时显示加载指示（历史加载慢时，空白会被读成「坏了」）', async () => {
    const w = await mountWidget(makeConfig({ loading: true }))
    expect(w.find('.aiw-loading').exists()).toBe(true)
  })

  it('加载中不显示空状态 —— 两个「没内容」的态同时出现只会让人更困惑', async () => {
    const w = await mountWidget(makeConfig({ loading: true }))
    expect(w.find('.aiw-loading').exists()).toBe(true)
    expect(w.find('.aiw-empty').exists()).toBe(false)
  })

  it('加载完成（loading 转 false）后回到空状态', async () => {
    const config = makeConfig({ loading: true })
    const w = await mountWidget(config)
    expect(w.find('.aiw-loading').exists()).toBe(true)
    await w.setProps({ config: { ...config, loading: false } })
    await nextTick()
    expect(w.find('.aiw-loading').exists()).toBe(false)
    expect(w.find('.aiw-empty').exists()).toBe(true)
  })
})

describe('消息区状态矩阵：无障碍', () => {
  it('空状态不抢读屏焦点（不是 alert），它只是个提示不是事故', async () => {
    const w = await mountWidget(makeConfig())
    expect(w.find('.aiw-empty[role="alert"]').exists()).toBe(false)
  })

  it('加载中要能被读屏播报（role=status），否则用户以为卡住了', async () => {
    const w = await mountWidget(makeConfig({ loading: true }))
    expect(w.get('.aiw-loading').attributes('role')).toBe('status')
  })
})
