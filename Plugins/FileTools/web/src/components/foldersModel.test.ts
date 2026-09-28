import { describe, expect, it } from 'vitest'
import {
  EMPTY_STATE_TEXT,
  displayRows,
  partitionOk,
  partitionRemainder,
  pickEmptyState,
  runAfterConfirm,
  shouldApplyProgress
} from './foldersModel'
import type { FolderScanView, FolderSizeRow } from '../types/fileTools'

function row(name: string, relativePath: string, totalBytes: number, directBytes = totalBytes): FolderSizeRow {
  return {
    relativePath,
    name,
    totalBytes,
    totalFormatted: `${totalBytes} B`,
    directBytes,
    directFormatted: `${directBytes} B`,
    fileCount: 1,
    dirCount: 0,
    percentage: 0,
    isOther: false
  }
}

function view(overrides: Partial<FolderScanView> = {}): FolderScanView {
  return {
    scanId: 'scan-1',
    state: 2,
    rootPath: 'C:\\repo',
    rootTotalBytes: 0,
    rootTotalFormatted: '0 B',
    rootOwnBytes: 0,
    rootOwnFormatted: '0 B',
    directoryCount: 0,
    fileCount: 0,
    durationMs: 10,
    inaccessibleCount: 0,
    skippedReparseCount: 0,
    truncated: false,
    capNote: '',
    partial: false,
    top: 50,
    childCount: 0,
    items: [],
    otherRow: null,
    rootOwnRow: null,
    error: '',
    ...overrides
  }
}

describe('目录排行 · 分区守恒', () => {
  it('Σ排行行 + 其他 + 根本级 == 根总量（余量恒为 0）', () => {
    const v = view({
      rootTotalBytes: 11816,
      rootOwnBytes: 2500,
      items: [row('A', 'A', 9216, 8192), row('B', 'B', 100)],
      rootOwnRow: row('本级文件', '', 2500)
    })
    expect(partitionRemainder(v)).toBe(0)
    expect(partitionOk(v)).toBe(true)
  })

  it('被 Top 挤掉的目录必须由「其他」行补回，否则守恒失败', () => {
    const items = [row('A', 'A', 5000), row('B', 'B', 3000)]
    const withoutOther = view({ rootTotalBytes: 10000, rootOwnBytes: 0, items })
    expect(partitionOk(withoutOther)).toBe(false)

    const withOther = view({
      rootTotalBytes: 10000,
      rootOwnBytes: 0,
      items,
      otherRow: { ...row('其他（3 个目录）', '', 2000), isOther: true }
    })
    expect(partitionRemainder(withOther)).toBe(0)
    expect(partitionOk(withOther)).toBe(true)
  })

  it('空根目录（总量 0）不得产生 NaN，且守恒成立', () => {
    const v = view()
    expect(partitionRemainder(v)).toBe(0)
    expect(Number.isNaN(partitionRemainder(v))).toBe(false)
    expect(partitionOk(v)).toBe(true)
  })

  it('展示行顺序：排行行 → 其他 → 本级文件', () => {
    const v = view({
      rootTotalBytes: 6,
      rootOwnBytes: 1,
      items: [row('A', 'A', 3), row('B', 'B', 2)],
      otherRow: row('其他', '', 0),
      rootOwnRow: row('本级文件', '', 1)
    })
    expect(displayRows(v).map(r => r.name)).toEqual(['A', 'B', '其他', '本级文件'])
  })
})

describe('目录排行 · 空态分级', () => {
  it('从未扫描 / 扫描中 / 空目录 / 无权限 / 失败 / 任务失效 各给不同文案', () => {
    const states = [
      [pickEmptyState(null), 'expired'],
      [pickEmptyState(view({ state: 0 })), 'running'],
      [pickEmptyState(view({ state: 1 })), 'running'],
      [pickEmptyState(view()), 'emptyDir'],
      [pickEmptyState(view({ inaccessibleCount: 7 })), 'noPermission'],
      [pickEmptyState(view({ state: 3, error: 'boom' })), 'failed'],
      [pickEmptyState(view({ rootTotalBytes: 123 })), 'idle']
    ] as const
    for (const [actual, expected] of states) expect(actual).toBe(expected)

    const texts = new Set(Object.values(EMPTY_STATE_TEXT))
    expect(texts.size).toBe(Object.keys(EMPTY_STATE_TEXT).length)
  })
})

describe('目录排行 · 轮询防闪', () => {
  it('仅 durationMs 变化视为无变化（跳过赋值）', () => {
    const prev = view({ durationMs: 10, rootTotalBytes: 100, items: [row('A', 'A', 100)] })
    const next = { ...prev, durationMs: 999 }
    expect(shouldApplyProgress(prev, next)).toBe(false)
  })

  it('字节数/状态/任务变化必须刷新', () => {
    const prev = view({ rootTotalBytes: 100, items: [row('A', 'A', 100)] })
    expect(shouldApplyProgress(prev, { ...prev, rootTotalBytes: 101 })).toBe(true)
    expect(shouldApplyProgress(prev, { ...prev, state: 1 })).toBe(true)
    expect(shouldApplyProgress(prev, { ...prev, scanId: 'other' })).toBe(true)
    expect(shouldApplyProgress(null, prev)).toBe(true)
  })
})

describe('目录排行 · 二次确认', () => {
  it('用户取消 ⇒ 一个请求都不发', async () => {
    let calls = 0
    const executed = await runAfterConfirm(
      async () => false,
      async () => {
        calls += 1
      }
    )
    expect(executed).toBe(false)
    expect(calls).toBe(0)
  })

  it('用户确认 ⇒ 执行一次', async () => {
    let calls = 0
    const executed = await runAfterConfirm(
      async () => true,
      async () => {
        calls += 1
      }
    )
    expect(executed).toBe(true)
    expect(calls).toBe(1)
  })
})
