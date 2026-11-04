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

test('S3 只读结果：末页边界、耗时、类型提示', async ({ page }) => {
  await openStory(page, 'DbAdmin/ReadResult', '[data-testid="result-grid"] table')
  await expect(tid(page, 'page-label')).toHaveText('3 / 3')
  await expect(tid(page, 'page-next')).toBeDisabled()
  await expect(tid(page, 'page-prev')).toBeEnabled()
  await expect(tid(page, 'result-stats')).toContainText('耗时 4 ms')
  await expect(tid(page, 'result-stats')).toContainText('第 101–120 行 / 共 120')
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
    await expect(tid(page, 'approval-destructive')).toContainText('破坏性')
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
