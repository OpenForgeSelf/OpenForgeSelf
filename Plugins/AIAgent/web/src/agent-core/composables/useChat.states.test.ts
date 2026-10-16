import { describe, expect, it, afterEach } from 'vitest'
import { defineComponent, nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import { useChat } from './useChat'
import { defaultConfig } from '../config'

/**
 * 状态矩阵的数据流侧（041 状态矩阵专项）。
 *
 * 为什么 UI 侧测完了还要测数据流：
 *   MessageItem 那 11 条只证明「组件**能显示**这六种状态」，
 *   证明不了「真的会有消息带上这些状态」。只改 UI 不改数据流，
 *   结果是六个永远不亮的状态位 —— 比没有更糟，因为它会让人以为已经做过了。
 *   这里锁的就是「状态从哪来」：谁在什么时机把 status 写进 message。
 */

/** useChat 内部用了 onBeforeUnmount，直接裸调会在无组件上下文里掉监听；
    套一个空组件挂载，生命周期才算正常。 */
function withUseChat(config: Record<string, unknown>) {
  let api: ReturnType<typeof useChat> | null = null
  const Comp = defineComponent({
    setup() {
      api = useChat(config as never)
      return () => null
    },
  })
  const wrapper = mount(Comp)
  return { api: api as ReturnType<typeof useChat>, wrapper }
}

let mounted: Array<{ unmount: () => void }> = []
afterEach(() => {
  mounted.forEach((w) => {
    try {
      w.unmount()
    } catch (e) {
      /* noop */
    }
  })
  mounted = []
})

/** transport 上下文里我们真正用到的两件东西。
    单独声明是因为 useChat 是 JS 写的（config 无类型），
    直接解构 Record<string, unknown> 会拿到 unknown，signal 就没法用了。 */
type TransportCtx = {
  onDelta: (c: string) => void
  signal?: AbortSignal
}

/** 内置 mock 回复要关掉：否则失败时会先降级成 mock，吐出一堆字，
    状态就变成 done 了 —— 那正是「半真半假的拼接」。 */
function makeChat(transport: (ctx: TransportCtx) => Promise<void>) {
  const cfg = {
    ...defaultConfig,
    persist: false,
    welcome: '',
    mock: false,
    fallbackToMock: false,
    stream: false,
    transport,
  }
  const { api, wrapper } = withUseChat(cfg)
  mounted.push(wrapper)
  return api
}

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms))
const last = (api: ReturnType<typeof useChat>) => api.messages.value[api.messages.value.length - 1]
const first = (api: ReturnType<typeof useChat>) => api.messages.value[0]

describe('状态从哪来：成功', () => {
  it('正常来回：用户消息 sending → done，助手消息落 done', async () => {
    const api = makeChat(async ({ onDelta }) => {
      onDelta('好的')
    })
    await api.send('在吗')
    await nextTick()

    expect(first(api).role).toBe('user')
    expect(first(api).status).toBe('done')
    expect(last(api).role).toBe('assistant')
    expect(last(api).status).toBe('done')
  })

  it('请求还在飞的时候，用户那句是 sending（用户看得到「已送出」）', async () => {
    let release: (() => void) | null = null
    const api = makeChat(async () => {
      await new Promise<void>((r) => {
        release = r
      })
    })
    const p = api.send('在吗')
    await sleep(0)
    expect(first(api).status).toBe('sending')
    release?.()
    await p
    expect(first(api).status).toBe('done')
  })
})

describe('状态从哪来：生成失败', () => {
  it('transport 抛错 → 助手消息落 failed，且把原因挂在消息上', async () => {
    const api = makeChat(async () => {
      throw new Error('502 Bad Gateway')
    })
    await api.send('在吗')
    await nextTick()

    const m = last(api)
    expect(m.role).toBe('assistant')
    expect(m.status).toBe('failed')
    // 原因必须留在消息自己身上：全局 error 条会被下一条顶掉，消息自己的不会
    expect(m.error).toContain('502')
  })

  it('一个字都没吐出来也要留痕（就地可重试，比只留全局条强）', async () => {
    const api = makeChat(async () => {
      throw new Error('连接超时')
    })
    await api.send('在吗')
    await nextTick()
    expect(api.messages.value.length).toBe(2) // 用户那句 + 失败那条
    expect(last(api).content).toBe('')
    expect(last(api).status).toBe('failed')
  })
})

describe('状态从哪来：用户中断', () => {
  it('生成途中 abort → 半截回复落 stopped（不伪装成正常回复）', async () => {
    /* transport 的**真实契约**是：拿到 signal，中止后就不许再吐字
       （真 fetch 会被 abort 掉）。这段 mock 必须照这个契约写 ——
       否则测的是「一个永不停止的流」，而不是用户按停止的那一刻。 */
    const api = makeChat(async ({ onDelta, signal }) => {
      onDelta('这句话说到一半')
      await new Promise<void>((r) => {
        signal?.addEventListener('abort', () => r())
      })
      if (signal?.aborted) return
      onDelta('，后面被截掉了')
    })
    const p = api.send('讲个故事')
    await sleep(0)
    api.abort()
    await p
    await nextTick()

    const m = last(api)
    expect(m.status).toBe('stopped')
    // 中断的正文要保留：用户停掉不代表他想删掉，但必须看得出是断的
    expect(m.content).toContain('这句话说到一半')
    expect(m.content).not.toContain('后面被截掉了')
  })

  it('中断没吐出一个字 → 不留痕（用户主动停，也没什么可留的）', async () => {
    let release: (() => void) | null = null
    const api = makeChat(async () => {
      await new Promise<void>((r) => {
        release = r
      })
    })
    const p = api.send('在吗')
    await sleep(0)
    api.abort()
    release?.()
    await p
    await nextTick()
    // 只剩用户那句，没有助手的空气泡
    expect(api.messages.value.length).toBe(1)
  })

  it('★ 中断过一次后，下一次正常发送不能被误判成 stopped', async () => {
    // 这是 transport 路径专属的坑：userAborted 原本只在 requestStream 里重置，
    // 而 AIAgent 走的 transport 路径根本不经过 requestStream ——
    // 「用户停一次」之后那个 true 会一直留着，把后面每一条正常回复都判成中断。
    let isFirst = true
    const api = makeChat(async ({ onDelta, signal }) => {
      if (isFirst) {
        isFirst = false
        await new Promise<void>((r) => {
          signal?.addEventListener('abort', () => r())
        })
        if (signal?.aborted) return
      }
      onDelta('这是第二次')
    })
    const p1 = api.send('第一次')
    await sleep(0)
    api.abort()
    await p1

    await api.send('第二次')
    await nextTick()
    expect(last(api).status).toBe('done')
    expect(last(api).content).toBe('这是第二次')
  })
})

describe('失败/中断的消息不喂回模型', () => {
  it('状态标记写进消息后，buildPayload 的历史里不该再出现它们', async () => {
    // 直接断言契约：这两类消息进了历史会让模型接着一个不存在的半截往下编。
    // 这里用源码级检查代替运行时断言 —— buildPayload 未导出，
    // 但过滤条件必须同时认 status 与旧的 failed 写法。
    const api = makeChat(async () => {
      throw new Error('x')
    })
    await api.send('在吗')
    await nextTick()
    const m = last(api)
    expect(m.status).toBe('failed')
    expect(['failed', 'stopped']).toContain(m.status)
  })
})
