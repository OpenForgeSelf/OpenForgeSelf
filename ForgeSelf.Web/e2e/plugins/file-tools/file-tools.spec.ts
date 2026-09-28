import * as fs from 'node:fs'
import * as os from 'node:os'
import * as path from 'node:path'
import { randomUUID } from 'node:crypto'
import type { Page, Response } from '@playwright/test'
import { test, expect } from '../../fixtures/e2e'

/**
 * 文件工具 · 目录大小排行（批次C）插件层 e2e —— 零 mock，真实宿主 + 真实磁盘目录树。
 *
 * 为什么必须自己造一棵「已知字节数」的目录树：FileTools 其余 4 个 tab 的服务层整体是 mock
 * （`Plugins/FileTools/web/src/services/fileToolsApi.ts` 不 import 任何 HTTP 客户端）。若本用例也像它们一样只看界面数字，
 * 就永远证不出「界面真的连到了 api/filetools/folders/*」。故这里同时断言
 * ① 浏览器发出了真实请求（response 事件，不做路由 mock），② 界面数字等于磁盘上写下的字节数。
 *
 * 数字口径：字节数只对**接口返回的整数**（`totalBytes`）精确断言；界面文本则断言它等于**接口自己给的**
 * `totalFormatted` / `percentage.toFixed(2)`。理由：展示串由后端 `FileSizeFormatter` 的 `{d:N2}` 产出、受宿主区域
 * 设置影响，在测试里再写一套格式化 = 造第二套真相。「磁盘整数 → 接口整数 → 界面显示接口原串」三段连起来才是完整链路。
 *
 * 覆盖：
 *  1. 主链路：填路径 → 扫描 → 排行降序 → 数字与已知树一致 → 占比闭合；
 *  2. 钻取：点某子目录「钻取」→ 以该目录为新根再扫；
 *  3. 二次确认的两条路径：取消扫描「点取消 ⇒ 不发 cancel 请求」；「确认取消 ⇒ 任务转 Cancelled」；
 *  4. 空态分级：空目录与不存在路径给出的文案不同；
 *  5. 快照：保存 → 列表出现 → 明细可看 → 删除（只删数据库行）；
 *  6. 截图读图 + 无横向溢出 + 零控制台错误。
 *
 * 运行（统一 globalSetup 全新宿主，前后端真起）：
 *   pnpm exec playwright test --config=playwright.config.ts e2e/plugins/file-tools --output=../.pw-out-filetools
 * 已运行的 publish 实例走查：
 *   $env:E2E_API_TOKEN="<解密明文>"; pnpm exec playwright test --config=playwright.live.config.ts e2e/plugins/file-tools
 *
 * ⚠ 铁律10：临时树只创建、只使用，**永不删除**（留在 %TEMP%/ForgeSelfE2E_FileTools_*）。
 *   用例造的快照数据用 DELETE 接口收掉（那是数据库行，不是文件）。
 */

interface SampleTree {
  root: string
  totalBytes: number
  rootOwnBytes: number
  childABytes: number
  childBBytes: number
}

/** 接口返回的扫描视图（只声明本用例要断言的字段） */
interface ScanViewDto {
  state: number
  rootPath: string
  rootTotalBytes: number
  rootTotalFormatted: string
  rootOwnBytes: number
  rootOwnFormatted: string
  directoryCount: number
  fileCount: number
  otherRow: { totalBytes: number; percentage: number } | null
  items: { name: string; relativePath: string; totalBytes: number; totalFormatted: string; percentage: number }[]
}

function createSampleTree(): SampleTree {
  const root = path.join(os.tmpdir(), `ForgeSelfE2E_FileTools_${randomUUID()}`)
  const write = (file: string, size: number) => {
    fs.mkdirSync(path.dirname(file), { recursive: true })
    fs.writeFileSync(file, Buffer.alloc(size, 0x61))
  }

  // 根本级 2 文件 = 2500；A(含子目录 Sub) = 9216；B = 100 → 根总量 11816
  write(path.join(root, 'root-1.bin'), 1000)
  write(path.join(root, 'root-2.bin'), 1500)
  write(path.join(root, 'A', 'a-1.bin'), 4096)
  write(path.join(root, 'A', 'a-2.bin'), 4096)
  write(path.join(root, 'A', 'Sub', 'deep.bin'), 1024)
  write(path.join(root, 'B', 'b-1.bin'), 100)

  return { root, totalBytes: 11816, rootOwnBytes: 2500, childABytes: 9216, childBBytes: 100 }
}

/**
 * 慢扫描目标：取消用例需要「点取消时任务确实还在跑」的窗口。
 * 1.1.1 时代用 12000 文件合成树（本机首建 40s~150s，固定路径 + 哨兵复用）；
 * 1.1.2 引擎提速后同树 32ms 扫完（单测实测），首轮轮询拿到的已是 Completed，取消键全程置灰 ——
 * 合成树永远撑不起窗口，改扫真实大盘：默认 `C:\Program Files`（每台 Windows 都有，只读元数据，
 * 探针实测新引擎 28.6s / 18 万文件 / 3 万目录），可用 `FT_E2E_SLOW_DIR` 覆盖。零 mock，全真文件系统。
 */
const SLOW_SCAN_DIR = process.env.FT_E2E_SLOW_DIR ?? 'C:\\Program Files'

function createEmptyDir(): string {
  const dir = path.join(os.tmpdir(), `ForgeSelfE2E_FileTools_empty_${randomUUID()}`)
  fs.mkdirSync(dir, { recursive: true })
  return dir
}

async function openFoldersTab(page: Page): Promise<void> {
  await page.goto('/file-tools')
  // 页签是自绘 tablist（`role="tab"` 的 button），不是 el-tabs —— 首跑 6 例全红就栽在这一行选择器上
  await page.getByRole('tab', { name: /目录排行/ }).click()
  await expect(page.locator('.folders-panel')).toBeVisible()
}

/** 发起扫描并证明这一发真的打到了后端（不做路由 mock）；返回受理响应 */
async function startScan(page: Page, dir: string): Promise<Response> {
  await page.locator('.path-input').fill(dir)
  const [response] = await Promise.all([
    page.waitForResponse(r => r.url().includes('/api/filetools/folders/scan') && r.request().method() === 'POST'),
    page.getByRole('button', { name: '扫描', exact: true }).click()
  ])
  return response
}

/** 成功路径：HTTP 200 + success=true，返回 scanId */
async function scan(page: Page, dir: string): Promise<string> {
  const response = await startScan(page, dir)
  expect(response.status()).toBe(200)
  const body = (await response.json()) as { success: boolean; data: { scanId: string } }
  expect(body.success).toBe(true)
  expect(body.data.scanId).toBeTruthy()
  return body.data.scanId
}

/** 失败路径：后端在受理阶段就拒绝（400 + 信封 message），界面必须留痕而非一闪而过的 toast */
async function scanRejected(page: Page, dir: string): Promise<void> {
  const response = await startScan(page, dir)
  expect(response.status()).toBe(400)
  await expect(page.locator('.error-detail')).toBeVisible()
}

/** 点第 index 行的「钻取」= 以该子目录为新根重扫，返回新 scanId */
async function drillInto(page: Page, index: number): Promise<string> {
  const [response] = await Promise.all([
    page.waitForResponse(r => r.url().includes('/api/filetools/folders/scan') && r.request().method() === 'POST'),
    rankRows(page).nth(index).getByRole('button', { name: '钻取' }).click()
  ])
  expect(response.status()).toBe(200)
  const body = (await response.json()) as { data: { scanId: string } }
  return body.data.scanId
}

/** 完成判定：state==2 时面板不再渲染 `.state-tag` 徽标 */
async function waitCompleted(page: Page): Promise<void> {
  await expect(page.locator('.state-tag')).toHaveCount(0, { timeout: 30_000 })
}

/** 用浏览器自己的会话（localStorage 里的真实密钥）回读接口原值，作为「界面数字」的对照基准 */
async function fetchView(page: Page, scanId: string): Promise<ScanViewDto> {
  return page.evaluate(async id => {
    const token = localStorage.getItem('forge_api_token')
    const res = await fetch(`/api/filetools/folders/scan/${id}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {}
    })
    const json = (await res.json()) as { data: ScanViewDto }
    return json.data
  }, scanId) as Promise<ScanViewDto>
}

// 快照表与排行表共用 `.rank-table`；不限定就会把快照行算进排行行数
const rankRows = (page: Page) => page.locator('.folders-panel > .rank-table tbody tr')
const snapRows = (page: Page) => page.locator('.snapshots .rank-table tbody tr')
const msgBox = (page: Page) => page.locator('.el-message-box').last()

test.describe('文件工具 · 目录大小排行', () => {
  test('主链路：真实扫描 → 排行降序 → 数字等于磁盘字节数 → 占比闭合', async ({ page }) => {
    const tree = createSampleTree()
    await openFoldersTab(page)
    // 铁律13：插件自带页面必须显示**已加载**版本（用户「插件页面没显示版本，是不是没更新」的困惑就靠它自证）
    await expect(page.locator('.view-title .ft-version')).toHaveText(/^v\d+\.\d+\.\d+$/)
    const scanId = await scan(page, tree.root)
    await waitCompleted(page)

    // ① 接口整数 = 磁盘上写下的字节数（证明真扫了盘，不是硬编码）
    const view = await fetchView(page, scanId)
    expect(view.state).toBe(2)
    expect(view.rootTotalBytes).toBe(tree.totalBytes)
    expect(view.rootOwnBytes).toBe(tree.rootOwnBytes)
    expect(view.fileCount).toBe(6)
    expect(view.directoryCount).toBe(3) // A、Sub、B
    expect(view.items.map(i => i.relativePath)).toEqual(['A', 'B'])
    expect(view.items.map(i => i.totalBytes)).toEqual([tree.childABytes, tree.childBBytes])
    // 分区守恒：Σ直接子目录 + 本级 = 根总量（Top 默认 50 > 2，不该有「其他」行）
    expect(view.items.reduce((s, i) => s + i.totalBytes, 0) + view.rootOwnBytes).toBe(view.rootTotalBytes)

    // ② 界面显示的串 = 接口自己给的格式化串（证明界面吃的是真实接口，且没二次加工）
    await expect(page.locator('.summary')).toContainText(view.rootTotalFormatted)
    const rows = rankRows(page)
    await expect(rows).toHaveCount(3) // A、B、本级文件（本级恒在最后，便于凑满 100%）
    await expect(rows.nth(0)).toContainText('A')
    await expect(rows.nth(0)).toContainText(view.items[0]?.totalFormatted ?? '')
    await expect(rows.nth(0)).toContainText(`${(view.items[0]?.percentage ?? 0).toFixed(2)}%`)
    await expect(rows.nth(1)).toContainText('B')
    await expect(rows.nth(1)).toContainText(view.items[1]?.totalFormatted ?? '')
    await expect(rows.nth(2)).toContainText('本级文件')
    await expect(rows.nth(2)).toContainText(view.rootOwnFormatted)

    // 降序：A 必须排在 B 前
    expect(view.items[0]?.percentage ?? 0).toBeGreaterThan(view.items[1]?.percentage ?? 0)

    // 守恒告警不得出现（出现即后端算错）
    await expect(page.locator('.warn-line')).toHaveCount(0)
    // 钻取按钮只出现在真实目录行，本级文件行没有
    await expect(rows.nth(2).getByRole('button', { name: '钻取' })).toHaveCount(0)

    await page.screenshot({ path: 'screenshots/e2e/file-tools/folders-ranking.png', fullPage: true })

    // 无横向溢出（窄容器不破版的最低保证）
    const overflow = await page.evaluate(() =>
      document.documentElement.scrollWidth - document.documentElement.clientWidth
    )
    expect(overflow).toBeLessThanOrEqual(0)
  })

  test('钻取：以子目录为新根重扫，只看到它的下级', async ({ page }) => {
    const tree = createSampleTree()
    await openFoldersTab(page)
    await scan(page, tree.root)
    await waitCompleted(page)

    const childId = await drillInto(page, 0) // 第 0 行 = A
    await waitCompleted(page)

    const view = await fetchView(page, childId)
    expect(view.rootPath).toBe(path.join(tree.root, 'A'))
    expect(view.rootTotalBytes).toBe(tree.childABytes)
    expect(view.items.map(i => i.relativePath)).toEqual(['Sub'])
    expect(view.items[0]?.totalBytes).toBe(1024)

    await expect(page.locator('.summary')).toContainText(path.join(tree.root, 'A'))
    await expect(page.locator('.summary')).toContainText(view.rootTotalFormatted)
    const rows = rankRows(page)
    await expect(rows).toHaveCount(2) // Sub + A 的本级文件
    await expect(rows.nth(0)).toContainText('Sub')
    await expect(rows.nth(0)).toContainText(view.items[0]?.totalFormatted ?? '')
  })

  test('二次确认两条路：取消 ⇒ 不发 cancel 请求；确认 ⇒ 任务转 Cancelled', async ({ page }) => {
    // 慢扫描目标保证「点取消时确实还在跑」；300s 覆盖真实大盘的首扫
    test.setTimeout(300_000)
    await openFoldersTab(page)
    const scanId = await scan(page, SLOW_SCAN_DIR)

    // 路径一：弹窗点「取消」→ 一个 cancel 请求都不许发
    let cancelSent = 0
    page.on('request', req => {
      if (req.method() === 'POST' && req.url().endsWith('/cancel')) cancelSent++
    })
    // 限定到本面板工具条：弹窗关掉后其「取消」键可能仍在 DOM，全局 getByRole 会撞 strict mode
    const cancelBtn = page.locator('.folders-panel > .toolbar').getByRole('button', { name: '取消', exact: true })
    await expect(cancelBtn).toBeEnabled()
    await cancelBtn.click()
    const box = msgBox(page)
    await expect(box).toBeVisible()
    // 必须 exact：弹窗里确认键文案是「取消扫描」，子串匹配会同时命中它（实测 strict mode 撞 2 个）
    await box.getByRole('button', { name: '取消', exact: true }).click()
    await expect(box).toBeHidden()
    expect(cancelSent).toBe(0)

    // 路径二：确认取消 → 状态转 Cancelled 且部分结果仍在（「已扫到的不白扫」）
    await cancelBtn.click()
    await msgBox(page).getByRole('button', { name: '取消扫描' }).click()
    await expect(page.locator('.state-tag')).toContainText('Cancelled', { timeout: 15_000 })
    await expect(rankRows(page)).not.toHaveCount(0)

    // 部分结果也必须自洽：旧实现「扫完才归并」，中间态根分母只有根级字节，
    // 界面实测出现过「总占用 29B / 某行 160KB / 占比 564965%」——这条断言就是为它补的。
    const partial = await fetchView(page, scanId)
    expect(partial.state).toBe(4)
    const listed = partial.items.reduce((s, i) => s + i.totalBytes, 0)
    expect(listed + (partial.otherRow?.totalBytes ?? 0) + partial.rootOwnBytes).toBe(partial.rootTotalBytes)
    for (const row of partial.items) expect(row.percentage).toBeLessThanOrEqual(100)
    await expect(page.locator('.warn-line')).toHaveCount(0)

    await page.screenshot({ path: 'screenshots/e2e/file-tools/folders-cancelled.png', fullPage: true })
  })

  test('空态分级：空目录与无权限/不存在路径文案各自独立', async ({ page }) => {
    await openFoldersTab(page)

    await scan(page, createEmptyDir())
    // 排行区的空态与快照区的 `.empty.small` 同类名，必须限定，否则 strict mode 撞两个元素
    await expect(page.locator('.folders-panel > .empty')).toContainText('该目录下没有文件与子目录')

    const bogus = path.join(os.tmpdir(), `ForgeSelfE2E_FileTools_missing_${randomUUID()}`)
    await scanRejected(page, bogus)
    await expect(page.locator('.error-text')).toContainText('目录不存在')
  })

  test('快照：保存 → 列表出现 → 明细可看 → 删除只减数据库行', async ({ page }) => {
    const tree = createSampleTree()
    await openFoldersTab(page)
    await scan(page, tree.root)
    await waitCompleted(page)

    await page.locator('.note-input').fill('e2e 批次C')
    await page.getByRole('button', { name: '保存快照' }).click()
    await msgBox(page).getByRole('button', { name: '保存' }).click()

    const row = snapRows(page).filter({ hasText: 'e2e 批次C' })
    await expect(row.first()).toBeVisible({ timeout: 10_000 })
    await row.first().getByRole('button', { name: '查看' }).click()
    await expect(page.locator('.snap-detail')).toBeVisible()
    await expect(page.locator('.snap-detail .rank-table tbody tr')).toHaveCount(3) // A、B + 本级

    await row.first().getByRole('button', { name: '删除' }).click()
    await msgBox(page).getByRole('button', { name: '删除' }).click()
    await expect(row).toHaveCount(0)
    // 删除动作不许碰磁盘：临时树仍在
    expect(fs.existsSync(path.join(tree.root, 'A', 'a-1.bin'))).toBe(true)
  })

  test('零控制台错误（真实扫描链路不得有未捕获异常）', async ({ page }) => {
    const errors: string[] = []
    page.on('console', msg => {
      if (msg.type() === 'error') errors.push(msg.text())
    })
    page.on('pageerror', err => errors.push(String(err)))

    const tree = createSampleTree()
    await openFoldersTab(page)
    await scan(page, tree.root)
    await waitCompleted(page)
    await page.waitForTimeout(1500)

    expect(errors).toEqual([])
  })
})
