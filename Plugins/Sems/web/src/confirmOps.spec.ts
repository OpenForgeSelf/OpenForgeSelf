import { describe, expect, it, vi } from 'vitest'
import {
  deleteCommandMessage,
  errorMessage,
  removeProjectMessage,
  runWithConfirm,
  stopExternalMessage,
  stopSessionMessage,
} from './confirmOps'

/**
 * sems 界面「二次确认 → 执行」编排单测。
 * 关键语义：用户取消时**一个请求都不发**；确认后才执行；失败转可读文案。
 * 确认动作由测试注入假实现，因此不依赖 element-plus 弹窗（与 AIAgent sessionArchive 同法）。
 */
describe('runWithConfirm', () => {
  it('用户取消 → 不发起任何动作', async () => {
    const action = vi.fn(async () => 'never')
    const confirm = vi.fn(async () => false)

    const result = await runWithConfirm({
      title: '移除项目',
      message: '确认？',
      confirm,
      action,
    })

    expect(result).toEqual({ outcome: 'cancelled' })
    expect(confirm).toHaveBeenCalledTimes(1)
    expect(action).not.toHaveBeenCalled()
  })

  it('用户确认 → 执行动作且只执行一次', async () => {
    const action = vi.fn(async () => 'ok')
    const confirm = vi.fn(async () => true)

    const result = await runWithConfirm({
      title: '移除项目',
      message: '确认？',
      confirm,
      action,
    })

    expect(result).toEqual({ outcome: 'done' })
    expect(action).toHaveBeenCalledTimes(1)
  })

  it('先确认后才请求：确认弹窗收到的文案就是展示给用户的那句', async () => {
    const seen: Array<[string, string]> = []
    const confirm = vi.fn(async (message: string, title: string) => {
      seen.push([message, title])
      return true
    })

    await runWithConfirm({
      title: '移除项目',
      message: '确认移除项目「demo」',
      confirm,
      action: async () => undefined,
    })

    expect(seen).toEqual([['确认移除项目「demo」', '移除项目']])
  })

  it('动作失败 → outcome=failed 且带错误文案（操作成败可见）', async () => {
    const result = await runWithConfirm({
      title: '停止',
      message: '确认？',
      confirm: async () => true,
      action: async () => {
        throw new Error('请求失败(409): 项目有 1 个运行中的命令')
      },
    })

    expect(result.outcome).toBe('failed')
    expect(result.error).toContain('请求失败(409)')
    expect(result.error).toContain('运行中的命令')
  })
})

describe('破坏性动作文案', () => {
  it('移除项目：必须写明「不动磁盘文件」+ 可重新添加', () => {
    const msg = removeProjectMessage('demo', 3)
    expect(msg).toContain('demo')
    expect(msg).toContain('3 条运行命令')
    expect(msg).toContain('不会删除磁盘')
    expect(msg).toContain('添加项目')
    // 不能出现 markdown 星号（弹窗按纯文本渲染）
    expect(msg).not.toContain('**')
  })

  it('移除零命令的项目时不提级联数量', () => {
    expect(removeProjectMessage('demo', 0)).not.toContain('级联删除')
  })

  it('删除命令 / 停止会话 / 停止外部进程文案各自含关键提示', () => {
    expect(deleteCommandMessage('dev')).toContain('不可恢复')
    expect(stopSessionMessage('demo', 'dev', 4321)).toContain('4321')
    expect(stopSessionMessage('demo', 'dev', 4321)).toContain('不可恢复')
    const external = stopExternalMessage('demo', 'node', 99)
    expect(external).toContain('并非本面板启动')
    expect(external).toContain('可能波及他人进程')
    expect(external).not.toContain('**')
  })

  it('errorMessage 兼容非 Error 抛出', () => {
    expect(errorMessage(new Error('boom'))).toBe('boom')
    expect(errorMessage('plain')).toBe('plain')
  })
})
