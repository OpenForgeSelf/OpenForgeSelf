/**
 * DbAdmin 数据库管理 · 组件视觉与契约回归（CT）
 * 覆盖 Plugins/DbAdmin/docs/06 验收清单 S0–S9 + S-R（S9 三张、S-R 前后各一张）；
 * fixture 为后端原始 JSON（docs/03 §3.1），另含 202 / 200 rejected / 409 / 424 的应答形态。
 * 运行：pnpm run test:ct        更新基线：pnpm run test:ct:update（基线不入库）
 */
import { test, expect, type Page } from '@playwright/test'
import { openStory } from './ct-support'

const ROOT = '[data-testid="db-admin-root"]'
const root = (page: Page) => page.locator(ROOT)
const tid = (page: Page, id: string) => page.locator(`[data-testid="${id}"]`)

/** 全页文本 + 所有输入值里不得出现「password=非掩码值」 */
async function expectNoPlaintextSecret(page: Page) {
  const text = await root(page).innerText()
  const values = await page.locator(`${ROOT} input, ${ROOT} textarea`).evaluateAll(els =>
    els.map(e => (e as HTMLInputElement).value))
  const all = [text, ...values].join('\n')
  const m = /\b(password|pwd)\s*=\s*([^;\s,)'"]*)/gi
  for (const hit of all.matchAll(m)) expect(hit[2], `明文：${hit[0]}`).toBe('****')
}

test.describe('DbAdmin · 五态', () => {
  test('加载：版本徽标「加载中」+ 树骨架', async ({ page }) => {
    await openStory(page, 'DbAdmin/Loading', ROOT)
    await expect(tid(page, 'version-badge')).toContainText('加载中')
    await expect(tid(page, 'tree-skeleton')).toBeVisible()
    await expect(root(page)).toHaveScreenshot()
  })

  test('S0 空态：引导可点；系统库带 🔒', async ({ page }) => {
    await openStory(page, 'DbAdmin/Empty', ROOT)
    await expect(tid(page, 'empty-guide')).toContainText('还没有自建连接')
    await expect(tid(page, 'conn-sys:ForgeSelf')).toContainText('🔒')
    await expect(tid(page, 'conn-sys:FileTools')).toContainText('🔒')
    await expect(root(page)).toHaveScreenshot()
    await tid(page, 'empty-new-connection').click()
    await expect(tid(page, 'conn-form')).toBeVisible()
  })

  test('无权限：401 横幅，不渲染数据区', async ({ page }) => {
    await openStory(page, 'DbAdmin/Unauthorized', ROOT)
    await expect(tid(page, 'banner-401')).toContainText('401')
    await expect(tid(page, 'conn-tree')).toHaveCount(0)
    await expect(root(page)).toHaveScreenshot()
  })
})

test.describe('DbAdmin · S1 连接表单', () => {
  test('SQLite 文件路径表单', async ({ page }) => {
    await openStory(page, 'DbAdmin/ConnFormSqlite', '[data-testid="conn-form"]')
    await expect(tid(page, 'cf-path')).toHaveValue('/data/demo.db')
    await expect(tid(page, 'cf-untested')).toHaveCount(0)
    await expect(root(page)).toHaveScreenshot()
  })

  test('校验失败留窗；编辑态密码遮罩、留空不改', async ({ page }) => {
    await openStory(page, 'DbAdmin/ConnFormInvalid', '[data-testid="conn-form"]')
    await expect(tid(page, 'cf-error')).toContainText('请填写数据库名')
    await expect(tid(page, 'cf-password')).toHaveAttribute('type', 'password')
    await expect(tid(page, 'cf-password')).toHaveAttribute('placeholder', /\*\*\*\*（留空 = 不改）/)
    await tid(page, 'cf-save').click()
    await expect(tid(page, 'conn-form')).toBeVisible()
    await expect(root(page)).toHaveScreenshot()
  })
})

test('S2 Schema 树：连接 → 表 → 列（类型 / PK）+ 只读徽标', async ({ page }) => {
  await openStory(page, 'DbAdmin/SchemaTree', ROOT)
  await expect(tid(page, 'col-1-users-id')).toContainText('INTEGER')
  await expect(tid(page, 'col-1-users-id')).toContainText('PK')
  await expect(tid(page, 'table-sys:ForgeSelf-Agent')).toBeVisible()
  await expect(tid(page, 'readonly-badge')).toContainText('只读')
  await expect(root(page)).toHaveScreenshot()
})

test('S2 点击连接加载表：显示局部 loading，空态不抢先', async ({ page }) => {
  await openStory(page, 'DbAdmin/TablesLoading', '[data-testid="tables-loading-1"]')
  const loading = tid(page, 'tables-loading-1')
  await expect(loading).toContainText('加载表')
  await expect(loading.locator('.spinner')).toBeVisible()
  // 空态（无表）只能在加载结束后出现，不能在 loading 期间抢先渲染
  await expect(page.locator('.node.column.muted', { hasText: '（无表）' })).toHaveCount(0)
  await expect(root(page)).toHaveScreenshot()
})

test('S3 只读结果：末页边界、状态栏、类型提示', async ({ page }) => {
  await openStory(page, 'DbAdmin/ReadResult', '[data-testid="result-grid"] table')
  await expect(tid(page, 'page-label')).toHaveText('3 / 3')
  await expect(tid(page, 'page-next')).toBeDisabled()
  await expect(tid(page, 'page-prev')).toBeEnabled()
  await expect(tid(page, 'result-stats')).toContainText('120 行 · 4 ms')
  await expect(tid(page, 'result-range')).toHaveText('第 101–120 行 / 共 120')
  await expect(page.locator('[data-testid="result-grid"] tbody tr')).toHaveCount(20)
  await expect(root(page)).toHaveScreenshot()
})

test.describe('DbAdmin · S4 写审批', () => {
  test('审批弹窗：语句 / 类型 / 来源；拒绝不执行', async ({ page }) => {
    await openStory(page, 'DbAdmin/WriteApproval', '[data-testid="approval-modal"]')
    await expect(tid(page, 'approval-sql')).toContainText('INSERT INTO users')
    await expect(tid(page, 'approval-modal')).toContainText('UI 编辑器')
    await expect(tid(page, 'approval-destructive')).toHaveCount(0)
    await expect(tid(page, 'approval-modal')).toContainText('写')
    await expect(root(page)).toHaveScreenshot()
    await tid(page, 'approval-reject').click()
    await expect(tid(page, 'approval-modal')).toHaveCount(0)
    await expect(tid(page, 'banner')).toContainText('已拒绝')
  })

  test('破坏性：批准后弹二次确认；取消则不执行', async ({ page }) => {
    await openStory(page, 'DbAdmin/DestructiveApproval', '[data-testid="approval-modal"]')
    await expect(tid(page, 'approval-destructive')).toContainText('不可恢复')
    await expect(tid(page, 'approval-modal')).toContainText('写 · 破坏性')
    await expect(root(page)).toHaveScreenshot()
    await tid(page, 'approval-approve').click()
    await expect(tid(page, 'destructive-confirm')).toBeVisible()
    await expect(tid(page, 'approval-modal')).toHaveCount(0)
    await tid(page, 'destructive-cancel').click()
    await expect(tid(page, 'destructive-confirm')).toHaveCount(0)
    await expect(tid(page, 'banner')).toContainText('语句未执行')
    await expect(tid(page, 'pending-approvals')).toContainText('待审批 1')
  })

  test('二次确认框 → 确认后审计有记录', async ({ page }) => {
    await openStory(page, 'DbAdmin/DestructiveConfirm', '[data-testid="destructive-confirm"]')
    await expect(tid(page, 'destructive-confirm')).toContainText('DROP TABLE orders')
    await expect(root(page)).toHaveScreenshot()
  })

  test('破坏性未二次确认：resolve 409 confirmation_required，审批仍待处理', async ({ page }) => {
    await openStory(page, 'DbAdmin/ConfirmRequired409', '[data-testid="result-error"]')
    await expect(tid(page, 'result-error-code')).toHaveText('[confirmation_required · HTTP 409]')
    await expect(tid(page, 'result-error')).toContainText('审批仍待处理')
    await expect(tid(page, 'pending-approvals')).toContainText('待审批 1')
    await expect(tid(page, 'approval-modal')).toHaveCount(0)
    await expect(root(page)).toHaveScreenshot()
  })
})

test.describe('DbAdmin · S5 失败（红条，不弹审批）', () => {
  for (const [id, code, text, http] of [
    ['DbAdmin/SqlError', 'sql_error', 'SQL 错误', 400],
    ['DbAdmin/HardReject', 'rejected_hard', '硬拒', 200],
    ['DbAdmin/ReadonlyWriteRejected', 'readonly_connection', '只读连接', 200],
    ['DbAdmin/DriverMissing424', 'driver_missing', 'Npgsql', 424],
  ] as const) {
    test(`${code} · HTTP ${http}`, async ({ page }) => {
      await openStory(page, id, '[data-testid="result-error"]')
      await expect(tid(page, 'result-error')).toContainText(text)
      await expect(tid(page, 'result-error-code')).toHaveText(`[${code} · HTTP ${http}]`)
      await expect(tid(page, 'approval-modal')).toHaveCount(0)
      await expect(root(page)).toHaveScreenshot()
    })
  }
})

test('S6 审计抽屉：含 query / approval / rejected，过滤生效', async ({ page }) => {
  await openStory(page, 'DbAdmin/AuditDrawer', '[data-testid="audit-drawer"]')
  const drawer = tid(page, 'audit-drawer')
  await expect(drawer.locator('[data-kind="query"]')).toHaveCount(1)
  await expect(drawer.locator('[data-kind^="approval_"]')).toHaveCount(2)
  await expect(drawer.locator('[data-kind="rejected"]')).toHaveCount(1)
  await expect(root(page)).toHaveScreenshot()
  await tid(page, 'audit-filter').selectOption('approval')
  await expect(drawer).toContainText('筛选 2/5')
})

test('S7 脱敏：连接 / 历史 / 审计全页搜不到明文', async ({ page }) => {
  await openStory(page, 'DbAdmin/Redaction', ROOT)
  await expect(tid(page, 'conn-bar')).toContainText('Password=****')
  await expect(tid(page, 'history-list')).toContainText("api_key = '****'")
  await expectNoPlaintextSecret(page)
  await expect(root(page)).toHaveScreenshot()
  await tid(page, 'open-audit').click()
  await expectNoPlaintextSecret(page)
})

test.describe('DbAdmin · S8 Agent 工具回显', () => {
  test('db_* 调用回显 + Agent 写挂起出审批', async ({ page }) => {
    await openStory(page, 'DbAdmin/AgentToolEcho', '[data-testid="approval-modal"]')
    await expect(tid(page, 'approval-from-agent')).toContainText('来自 Agent')
    await expect(tid(page, 'agent-echo-list')).toContainText('db_execute_write')
    await expect(tid(page, 'agent-echo-list')).toContainText('awaiting_approval')
    await expect(tid(page, 'agent-echo-list')).toContainText('truncated=true')
    await expect(tid(page, 'agent-echo-list')).toContainText('审计 kind=agent')
    await expect(tid(page, 'agent-echo-list').locator('[data-status="awaiting_approval"]')).toContainText('审批 21')
    await expect(tid(page, 'agent-echo-list').locator('[data-status="rejected"]')).toContainText('[rejected_hard]')
    await expect(root(page)).toHaveScreenshot()
  })

  test('Agent 截断回显 truncated=true（仅 kind=agent 条目进回显）', async ({ page }) => {
    await openStory(page, 'DbAdmin/AgentTruncated', '[data-testid="agent-echo-list"] [data-status="ok"]')
    const rows = tid(page, 'agent-echo-list').locator('.echo')
    await expect(rows).toHaveCount(1)
    await expect(rows.first()).toContainText('truncated=true')
    await expect(rows.first()).toContainText('1000 行')
    await expect(page.locator('[data-testid="result-grid"] tbody tr')).toHaveCount(12)
    await expect(root(page)).toHaveScreenshot()
  })
})

test.describe('DbAdmin · S9 多 provider 测连', () => {
  test('成功（绿 + 服务器版本）+ 未实测', async ({ page }) => {
    await openStory(page, 'DbAdmin/ProviderTestOk', '[data-testid="test-result"]')
    await expect(tid(page, 'test-result')).toHaveClass(/tr-ok/)
    await expect(tid(page, 'test-result')).toContainText('服务器 8.0.36')
    await expect(tid(page, 'cf-untested')).toContainText('未实测')
    await expect(tid(page, 'cf-provider').locator('option')).toHaveCount(4)
    await expect(root(page)).toHaveScreenshot()
  })

  test('失败（红，原因已脱敏）+ 连接串遮罩', async ({ page }) => {
    await openStory(page, 'DbAdmin/ProviderTestError', '[data-testid="test-result"]')
    await expect(tid(page, 'test-result')).toHaveClass(/tr-error/)
    await expect(tid(page, 'test-result')).toContainText('Connection refused')
    await expect(tid(page, 'cf-connstr')).toHaveAttribute('type', 'password')
    await expectNoPlaintextSecret(page)
    await expect(root(page)).toHaveScreenshot()
  })

  test('驱动缺失（黄，指明包名）+ 需驱动灰标', async ({ page }) => {
    await openStory(page, 'DbAdmin/ProviderDriverMissing', '[data-testid="test-result"]')
    await expect(tid(page, 'test-result')).toHaveClass(/tr-driver_missing/)
    await expect(tid(page, 'test-result')).toContainText('Npgsql')
    await expect(tid(page, 'cf-needs-driver')).toContainText('需驱动')
    await expect(root(page)).toHaveScreenshot()
  })
})

test.describe('DbAdmin · 多标签查询（FR-1~7 / AC-1~4 / AC-13 / AC-21）', () => {
  test('两标签互不覆盖；切换保留内容；重复开同表不新建不重置', async ({ page }) => {
    await openStory(page, 'DbAdmin/QueryTabs', '[data-testid="query-tabs"]')
    await expect(tid(page, 'qtab-t:1:users')).toHaveClass(/on/)
    await expect(tid(page, 'qtab-t:1:orders')).toHaveCount(1)
    await expect(tid(page, 'sql-editor')).toHaveValue('SELECT * FROM users ORDER BY id')
    await expect(tid(page, 'result-stats')).toContainText('120 行 · 4 ms')

    await tid(page, 'qtab-t:1:orders').click()
    await expect(tid(page, 'qtab-t:1:orders')).toHaveClass(/on/)
    await expect(tid(page, 'sql-editor')).toHaveValue('SELECT * FROM orders')
    await expect(tid(page, 'result-stats')).toContainText('3 行 · 2 ms')

    await tid(page, 'qtab-t:1:users').click()
    await expect(tid(page, 'sql-editor')).toHaveValue('SELECT * FROM users ORDER BY id')
    await expect(tid(page, 'result-stats')).toContainText('120 行 · 4 ms')
    await expect(root(page)).toHaveScreenshot()

    // 已打开的表再点：仍为 2 个标签，且用户改过的 SQL 不被重置（FR-2/FR-3）
    await tid(page, 'sql-editor').fill('SELECT id FROM users')
    await tid(page, 'table-1-users').click()
    await expect(tid(page, 'qtab-t:1:users')).toHaveCount(1)
    await expect(tid(page, 'qtab-t:1:orders')).toHaveCount(1)
    await expect(tid(page, 'sql-editor')).toHaveValue('SELECT id FROM users')

    // 展开箭头只切列、不开标签（AC-13）
    await tid(page, 'table-1-orders').locator('.caret').click()
    await expect(tid(page, 'qtab-t:1:orders')).toHaveCount(1)
    await expect(tid(page, 'sql-editor')).toHaveValue('SELECT id FROM users')

    // 表过滤：本地过滤 + 无匹配（AC-21）
    await tid(page, 'table-filter').fill('ord')
    await expect(tid(page, 'table-1-users')).toHaveCount(0)
    await expect(tid(page, 'table-1-orders')).toHaveCount(1)
    await tid(page, 'table-filter').fill('zzz')
    await expect(tid(page, 'table-filter-empty')).toContainText('无匹配表')
    await tid(page, 'table-filter').fill('')
    await expect(tid(page, 'table-1-users')).toHaveCount(1)
  })

  test('关闭标签：右邻激活；关到 0 显示工作区空态', async ({ page }) => {
    await openStory(page, 'DbAdmin/QueryTabs', '[data-testid="query-tabs"]')
    await tid(page, 'qtab-t:1:users').locator('[data-testid="close-tab"]').click()
    await expect(tid(page, 'qtab-t:1:orders')).toHaveClass(/on/)
    await tid(page, 'qtab-t:1:orders').locator('[data-testid="close-tab"]').click()
    await expect(tid(page, 'query-tabs')).toHaveCount(0)
    await expect(tid(page, 'workspace-empty')).toContainText('还没有打开的查询')
    await expect(tid(page, 'sql-editor')).toHaveCount(0)
    await expect(root(page)).toHaveScreenshot()
  })
})

test.describe('DbAdmin · 多语句与选区执行（FR-14~19 / AC-15~19）', () => {
  test('多语句 → 结果标签条；切换结果不串数据；单语句不渲染标签条', async ({ page }) => {
    await openStory(page, 'DbAdmin/QueryTabsMultiResult', '[data-testid="result-tabs"]')
    await expect(tid(page, 'result-tab-0')).toContainText('结果 1')
    await expect(tid(page, 'result-tab-1')).toContainText('结果 2')
    await expect(tid(page, 'result-tab-0')).toHaveClass(/on/)
    await expect(page.locator('[data-testid="result-grid"] tbody tr')).toHaveCount(10)
    await expect(root(page)).toHaveScreenshot()

    await tid(page, 'result-tab-1').click()
    await expect(tid(page, 'result-tab-1')).toHaveClass(/on/)
    await expect(tid(page, 'result-grid')).toContainText('120')
    await expect(page.locator('[data-testid="result-grid"] tbody tr')).toHaveCount(1)

    // 单语句回归（AC-19）
    await openStory(page, 'DbAdmin/ReadResult', '[data-testid="result-grid"] table')
    await expect(tid(page, 'result-tabs')).toHaveCount(0)
  })

  test('批量遇写审批：暂停 → 批准续跑；拒绝则其余「未执行」', async ({ page }) => {
    await openStory(page, 'DbAdmin/QueryRunApproval', '[data-testid="sql-editor"]')
    await tid(page, 'run').click()
    await expect(tid(page, 'approval-modal')).toBeVisible()
    await expect(tid(page, 'result-tab-1').locator('.rtab-dot')).toHaveClass(/d-awaiting/)
    await expect(tid(page, 'result-tab-2').locator('.rtab-dot')).toHaveClass(/d-pending/)
    await expect(root(page)).toHaveScreenshot()

    await tid(page, 'approval-approve').click()
    await expect(tid(page, 'approval-modal')).toHaveCount(0)
    await expect(tid(page, 'result-tab-1').locator('.rtab-dot')).toHaveClass(/d-ok/)
    await expect(tid(page, 'result-tab-2').locator('.rtab-dot')).toHaveClass(/d-ok/)

    // 拒绝分支：第 2 条标失败、第 3 条标未执行
    await openStory(page, 'DbAdmin/QueryRunApproval', '[data-testid="sql-editor"]')
    await tid(page, 'run').click()
    await expect(tid(page, 'approval-modal')).toBeVisible()
    await tid(page, 'approval-reject').click()
    await expect(tid(page, 'result-tab-1').locator('.rtab-dot')).toHaveClass(/d-error/)
    await expect(tid(page, 'result-tab-2').locator('.rtab-dot')).toHaveClass(/d-skipped/)
  })

  test('全选执行：失败即停，后续标「未执行」；选区执行只跑选中语句', async ({ page }) => {
    await openStory(page, 'DbAdmin/QueryRunBatch', '[data-testid="sql-editor"]')
    await tid(page, 'run').click()
    await expect(tid(page, 'result-tabs')).toBeVisible()
    await expect(tid(page, 'result-tab-0')).toContainText('结果 1')
    await expect(tid(page, 'result-tab-1')).toContainText('结果 2')
    await expect(tid(page, 'result-tab-2')).toContainText('结果 3')
    await expect(tid(page, 'result-tab-0').locator('.rtab-dot')).toHaveClass(/d-ok/)
    await expect(tid(page, 'result-tab-1').locator('.rtab-dot')).toHaveClass(/d-error/)
    await expect(tid(page, 'result-tab-2').locator('.rtab-dot')).toHaveClass(/d-skipped/)
    // 默认停在结果 1（成功行可见）
    await expect(page.locator('[data-testid="result-grid"] tbody tr')).toHaveCount(2)
    // 失败结果 → 红条；未执行结果 → 未执行说明（不显示结果格）
    await tid(page, 'result-tab-1').click()
    await expect(tid(page, 'result-error')).toContainText('SQL 错误')
    await tid(page, 'result-tab-2').click()
    await expect(tid(page, 'result-skip')).toContainText('未执行（第 2 条失败）')
    await expect(root(page)).toHaveScreenshot()

    // 选区执行（AC-14）：选中第 3 条 → 只产生 1 个结果，不出现结果标签条
    await openStory(page, 'DbAdmin/QueryRunBatch', '[data-testid="sql-editor"]')
    await tid(page, 'sql-editor').evaluate((el, needle) => {
      const ta = el as HTMLTextAreaElement
      const start = ta.value.indexOf(needle)
      ta.focus()
      ta.setSelectionRange(start, start + needle.length)
      ta.dispatchEvent(new Event('select', { bubbles: true }))
    }, 'SELECT 1;')
    await expect(tid(page, 'selection-hint')).toContainText('仅执行选中')
    await tid(page, 'run').click()
    await expect(tid(page, 'result-tabs')).toHaveCount(0)
    await expect(page.locator('[data-testid="result-grid"] tbody tr')).toHaveCount(2)
    await expect(tid(page, 'result-error')).toHaveCount(0)
  })
})

test.describe('DbAdmin · 结果工具（FR-21~23 / AC-22~24）', () => {
  test('双击单元格 → 详情浮层（可复制）；整行复制随选中启用；Esc 关闭', async ({ page }) => {
    await openStory(page, 'DbAdmin/ReadResult', '[data-testid="result-grid"] table')
    await expect(tid(page, 'copy-row')).toBeDisabled()
    await tid(page, 'row-0').click()
    await expect(tid(page, 'copy-row')).toBeEnabled()
    await tid(page, 'cell-0-1').dblclick()
    await expect(tid(page, 'cell-detail')).toBeVisible()
    await expect(tid(page, 'cell-detail-value')).toHaveText('user_101')
    await expect(root(page)).toHaveScreenshot()
    await page.keyboard.press('Escape')
    await expect(tid(page, 'cell-detail')).toHaveCount(0)
  })

  test('导出 CSV：当前结果标签当前页，文件名含 -p<page>', async ({ page }) => {
    await openStory(page, 'DbAdmin/ReadResult', '[data-testid="result-grid"] table')
    const [download] = await Promise.all([
      page.waitForEvent('download'),
      tid(page, 'export-csv').click(),
    ])
    // 手写标签无表名 → 文件名基底回落连接名；-r<result>-p<page>-<yyMMddHHmm>
    expect(download.suggestedFilename()).toMatch(/^demo\.db-r1-p3-\d{10}\.csv$/)
  })
})

test.describe('DbAdmin · S-R 刷新恢复', () => {
  test('刷新前：草稿落 localStorage，结果集与密码不落盘', async ({ page }) => {
    await openStory(page, 'DbAdmin/RestoreBefore', '[data-testid="result-grid"] table')
    await tid(page, 'sql-editor').fill('SELECT id FROM users -- 刷新前草稿')
    await page.waitForTimeout(50)
    const raw = await page.evaluate(() => localStorage.getItem('forge.self.db-admin.restore.v1') ?? '')
    expect(raw).toContain('刷新前草稿')
    expect(raw).not.toMatch(/"(password|connStr)"/i)
    expect(raw).not.toContain('user_007')
    expect(new TextEncoder().encode(raw).length).toBeLessThanOrEqual(64 * 1024)
    await expect(root(page)).toHaveScreenshot()
  })

  test('刷新后：连接 / 当前连接 / 草稿恢复；无结果集；待审批重拉', async ({ page }) => {
    await openStory(page, 'DbAdmin/RestoreAfter', '[data-testid="restore-hint"]')
    await expect(tid(page, 'restore-hint')).toContainText('3 个连接')
    await expect(tid(page, 'conn-bar')).toContainText('demo.db')
    await expect(tid(page, 'sql-editor')).toHaveValue(/WHERE email IS NULL/)
    await expect(page.locator('[data-testid="result-grid"] table')).toHaveCount(0)
    await expect(tid(page, 'pending-approvals')).toContainText('待审批 1')
    const raw = await page.evaluate(() => localStorage.getItem('forge.self.db-admin.restore.v1') ?? '')
    expect(raw).not.toMatch(/"password"/i)
    await expect(root(page)).toHaveScreenshot()
  })
})
