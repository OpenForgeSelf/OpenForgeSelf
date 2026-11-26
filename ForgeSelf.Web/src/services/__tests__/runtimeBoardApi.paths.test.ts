import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/**
 * 运行实况取数必须打到各来源已经在用的生产路由。
 * 打错路径时宿主回 404，页面会把「插件在跑」标成「未启用」。
 */
describe('runtimeBoardApi 生产路由', () => {
  const src = readFileSync(resolve(process.cwd(), 'src/services/runtimeBoardApi.ts'), 'utf8')

  it('脚本执行走 api/scripts，待办状态用 Pending', () => {
    expect(src).toContain('/api/scripts/executions?status=Running&pageSize=50')
    expect(src).not.toContain('/api/script-runner/executions')
    expect(src).toContain('/api/todos?status=Pending&page=1&pageSize=100')
    expect(src).not.toContain('/api/todos?status=pending')
  })

  it('文件工具前缀是 api/filetools', () => {
    expect(src).toContain('/api/filetools/folders/scans')
    expect(src).not.toContain('/api/file-tools/')
  })

  it('终端是否在跑要再读命令块', () => {
    expect(src).toContain('/api/smart-terminal/sessions/${encodeURIComponent(id)}/blocks')
  })
})
