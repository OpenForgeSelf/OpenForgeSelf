import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import MessageItem from './MessageItem.vue'

/**
 * 消息状态矩阵单测（041 状态矩阵专项）
 *
 * 为什么单开一个文件：AI 场景最容易漏的从来不是「正常态」，而是**异常与中间态**——
 * 生成失败、用户中断、低置信度、权限不足。这些态平时看不见，一旦后端抽风天天见，
 * 而它们往往根本没进 UI，只留在数据流里（典型症状：useChat 里过滤了 `m.failed`，
 * 但没有任何地方给它赋值过 —— 契约半成品）。
 *
 * 本文件把 6 类消息状态**逐个钉成断言**。写这些断言时它们是红灯 —— 那正是缺陷本身：
 * 组件目前只认 role（user/assistant），不认任何状态。红灯清单就是补齐清单。
 *
 * 状态契约（本文件即契约文档）：
 *   message.status     : 'sending' | 'streaming' | 'done' | 'failed' | 'stopped'
 *   message.confidence : 0~1 的数字，<0.5 视为低置信度（宿主算好，组件只展示）
 *   message.denied     : 字符串，非空 = 这条因权限不足被拦下
 */

const baseConfig = { theme: 'page', showBotAvatar: false, avatar: '' }

async function mountMsg(message: Record<string, unknown>, config = baseConfig) {
  const wrapper = mount(MessageItem, {
    props: { message, avatar: '', config },
    global: { stubs: { BlockRenderer: true, BlockBoundary: true } },
  })
  await nextTick()
  await nextTick()
  return wrapper
}

describe('消息状态矩阵：正常 / 发送中 / 流式', () => {
  it('成功态（status=done）：不带任何状态装饰，只是一条普通消息', async () => {
    const w = await mountMsg({
      id: 'm-done', role: 'assistant', content: '好的', time: Date.now(), status: 'done',
    })
    expect(w.find('.aiw-msg--bot').exists()).toBe(true)
    expect(w.find('.aiw-msg__status').exists()).toBe(false)
  })

  it('发送中（status=sending）：用户刚发出去、还没回，要有「已送出」的可见痕迹', async () => {
    const w = await mountMsg({
      id: 'm-sending', role: 'user', content: '帮我看下这个项目', time: Date.now(), status: 'sending',
    })
    // 期望：气泡上带 is-sending 标记 + 一条状态文案（读屏也要能播报）
    expect(w.find('.aiw-msg.is-sending').exists()).toBe(true)
    expect(w.find('.aiw-msg__status').exists()).toBe(true)
  })

  it('流式生成中（status=streaming）：带 is-streaming 标记，供 CSS 关光标动画', async () => {
    const w = await mountMsg({
      id: 'm-streaming', role: 'assistant', content: '正在写', time: Date.now(), status: 'streaming',
    })
    expect(w.find('.aiw-msg.is-streaming').exists()).toBe(true)
  })
})

describe('消息状态矩阵：AI 特有失败态', () => {
  it('生成失败（status=failed）：要有失败标记 + 「重试」按钮，不能只靠全局 error 条', async () => {
    const w = await mountMsg({
      id: 'm-failed', role: 'assistant', content: '', time: Date.now(), status: 'failed',
      error: '模型返回 502',
    })
    expect(w.find('.aiw-msg.is-failed').exists()).toBe(true)
    expect(w.find('.aiw-msg__status').text()).toContain('502')
    const retry = w.find('.aiw-msg__retry')
    expect(retry.exists()).toBe(true)
    // 点重试要把这条消息原样抛回去（emit retry + 消息 id），否则不知重试谁
    await retry.trigger('click')
    expect(w.emitted('retry')).toBeTruthy()
  })

  it('用户中断（status=stopped）：半截回复要标「已停止」，不能伪装成正常回复', async () => {
    const w = await mountMsg({
      id: 'm-stopped', role: 'assistant', content: '这段话说到一半被用', time: Date.now(), status: 'stopped',
    })
    expect(w.find('.aiw-msg.is-stopped').exists()).toBe(true)
    // 中断的正文要保留（用户停掉不代表他想删掉），但要看得出来是断的
    expect(w.get('.aiw-msg__bubble').text()).toContain('这段话说到一半')
    expect(w.find('.aiw-msg__status').exists()).toBe(true)
  })

  it('低置信度（confidence<0.5）：要显式提示「这条把握不大」，而不是默默输出', async () => {
    const w = await mountMsg({
      id: 'm-low', role: 'assistant', content: '大概是这个原因', time: Date.now(),
      status: 'done', confidence: 0.31,
    })
    expect(w.find('.aiw-msg.is-lowconf').exists()).toBe(true)
    expect(w.find('.aiw-msg__warn').exists()).toBe(true)
  })

  it('高置信度（confidence>=0.5）不提示 —— 天天弹低置信度等于没提示', async () => {
    const w = await mountMsg({
      id: 'm-high', role: 'assistant', content: '确定是这样', time: Date.now(),
      status: 'done', confidence: 0.92,
    })
    expect(w.find('.aiw-msg.is-lowconf').exists()).toBe(false)
    expect(w.find('.aiw-msg__warn').exists()).toBe(false)
  })

  it('权限不足（denied）：工具被安全档拦下时，要就地说明被拦的是什么', async () => {
    const w = await mountMsg({
      id: 'm-denied', role: 'assistant', content: '我需要写文件', time: Date.now(),
      status: 'done', denied: '当前权限档（只读）不允许写文件',
    })
    expect(w.find('.aiw-msg.is-denied').exists()).toBe(true)
    expect(w.find('.aiw-msg__warn').text()).toContain('只读')
  })
})

describe('消息状态矩阵：无障碍', () => {
  it('失败态要能被读屏播报（role=alert），否则键盘/读屏用户不知道这条挂了', async () => {
    const w = await mountMsg({
      id: 'm-failed-a11y', role: 'assistant', content: '', time: Date.now(), status: 'failed',
      error: '连接超时',
    })
    const status = w.get('.aiw-msg__status')
    expect(status.attributes('role')).toBe('alert')
  })

  it('流式态不要抢读屏（不设 alert）—— 否则生成过程会被逐字打断式播报', async () => {
    const w = await mountMsg({
      id: 'm-streaming-a11y', role: 'assistant', content: '正在写', time: Date.now(), status: 'streaming',
    })
    expect(w.find('.aiw-msg__status[role="alert"]').exists()).toBe(false)
  })

  it('状态标记不覆盖「这是谁说的」—— 角色标签必须一直在', async () => {
    const w = await mountMsg({
      id: 'm-who', role: 'assistant', content: '', time: Date.now(), status: 'failed', error: 'x',
    })
    expect(w.get('.aiw-msg').attributes('aria-label')).toBe('助手回复')
  })
})
