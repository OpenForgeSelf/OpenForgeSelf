/**
 * SmartTerminal 组件视觉回归（CT）
 * 运行：pnpm run test:ct        更新基线：pnpm run test:ct:update
 * 含终验：custom 红条、S-R 刷新恢复前后对照。
 */
import { test, expect } from '@playwright/test'
import { openStory } from './ct-support'

const root = (page: import('@playwright/test').Page) => page.locator('[data-testid="smart-terminal-root"]')

test.describe('SmartTerminal · 视觉与契约', () => {
  test('空标签：引导新建或 URL 深链', async ({ page }) => {
    await openStory(page, 'SmartTerminal/EmptySessions', '[data-testid="smart-terminal-root"]')
    await expect(root(page)).toContainText('新建标签')
    await expect(page.locator('[data-testid="tab-new"]')).toHaveCount(1)
    await expect(root(page)).toHaveScreenshot()
  })

  test('多标签：本地 + SSH 标签共存', async ({ page }) => {
    await openStory(page, 'SmartTerminal/MultiTabs', '[data-testid="tab-bar"]')
    await expect(page.locator('[data-testid="tab-bar"] .tab')).toHaveCount(3)
    await expect(root(page)).toContainText('生产跳板')
    await expect(root(page)).toContainText('ops@10.0.0.8')
    await expect(root(page)).toHaveScreenshot()
  })

  test('标签重命名输入态', async ({ page }) => {
    await openStory(page, 'SmartTerminal/TabRename', '[data-testid="tab-rename-input"]')
    await expect(page.locator('[data-testid="tab-rename-input"]')).toHaveValue('旧名称')
    await expect(root(page)).toHaveScreenshot()
  })

  test('AskWrite 实时输出', async ({ page }) => {
    await openStory(page, 'SmartTerminal/LiveOutputAskWrite', '[data-testid="smart-terminal-root"]')
    await expect(page.locator('[data-testid="mode-badge"]')).toContainText('写询问')
    await expect(page.locator('[data-testid="console"]')).toContainText('hello from bash')
    await expect(root(page)).toHaveScreenshot()
  })

  test('Auto 徽章', async ({ page }) => {
    await openStory(page, 'SmartTerminal/ModeAuto', '[data-testid="mode-badge"]')
    await expect(page.locator('[data-testid="mode-badge"]')).toContainText('自动执行')
    await expect(page.locator('[data-testid="mode-badge"]')).toHaveClass(/badge-auto/)
    await expect(root(page)).toHaveScreenshot()
  })

  test('AskHighRisk 徽章', async ({ page }) => {
    await openStory(page, 'SmartTerminal/ModeAskHighRisk', '[data-testid="mode-badge"]')
    await expect(page.locator('[data-testid="mode-badge"]')).toContainText('仅询高危')
    await expect(page.locator('[data-testid="mode-badge"]')).toHaveClass(/badge-high/)
    await expect(root(page)).toHaveScreenshot()
  })

  test('待审批弹窗', async ({ page }) => {
    await openStory(page, 'SmartTerminal/PendingApproval', '[data-testid="approval-modal"]')
    await expect(page.locator('[data-testid="approval-modal"]')).toContainText('需要审批')
    await expect(page.locator('[data-testid="approval-modal"]')).toContainText('rm -rf ./tmp-build')
    await expect(root(page)).toHaveScreenshot()
  })

  test('拒绝与批准历史', async ({ page }) => {
    await openStory(page, 'SmartTerminal/DeniedAndApproved', '[data-testid="command-history"]')
    await expect(page.locator('[data-testid="command-history"]')).toContainText('denied')
    await expect(page.locator('[data-testid="command-history"]')).toContainText('completed')
    await expect(root(page)).toHaveScreenshot()
  })

  test('审计抽屉过滤', async ({ page }) => {
    await openStory(page, 'SmartTerminal/AuditDrawer', '[data-testid="audit-drawer"]')
    await expect(page.locator('[data-testid="audit-drawer"]')).toBeVisible()
    await expect(page.locator('[data-testid="audit-kind-filter"]')).toHaveValue('command')
    await expect(page.locator('[data-testid="audit-drawer"]')).toContainText('echo hello')
    await expect(page.locator('[data-testid="audit-drawer"]')).not.toContainText('permission_mode')
    await expect(root(page)).toHaveScreenshot()
  })

  test('密钥脱敏行', async ({ page }) => {
    await openStory(page, 'SmartTerminal/SecretRedacted', '[data-testid="console"]')
    await expect(page.locator('[data-testid="console"]')).toContainText('***REDACTED***')
    await expect(root(page)).toHaveScreenshot()
  })

  test('401 未授权', async ({ page }) => {
    await openStory(page, 'SmartTerminal/ErrorUnauthorized', '[data-testid="banner-unauthorized"]')
    await expect(page.locator('[data-testid="banner-unauthorized"]')).toContainText('401')
    await expect(root(page)).toHaveScreenshot()
  })

  test('通用错误横幅', async ({ page }) => {
    await openStory(page, 'SmartTerminal/ErrorGeneric', '[data-testid="banner-error"]')
    await expect(page.locator('[data-testid="banner-error"]')).toContainText('工作目录不存在')
    await expect(root(page)).toHaveScreenshot()
  })

  test('Linux 多壳选择器（新建面板）', async ({ page }) => {
    await openStory(page, 'SmartTerminal/LinuxShellPicker', '[data-testid="shell-picker"]')
    await expect(page.locator('[data-testid="shell-picker"]')).toHaveValue('zsh')
    await expect(page.locator('[data-testid="new-tab-panel"]')).toBeVisible()
    await expect(root(page)).toHaveScreenshot()
  })

  test('密码深链知情确认：未勾选时不可继续', async ({ page }) => {
    await openStory(page, 'SmartTerminal/PasswordRiskAck', '[data-testid="password-ack-modal"]')
    await expect(page.locator('[data-testid="password-ack-modal"]')).toContainText('我已知晓风险')
    await expect(page.locator('[data-testid="password-ack-mode"]')).toHaveText('always-ack')
    await expect(page.locator('[data-testid="password-ack-continue"]')).toBeDisabled()
    await expect(root(page)).toHaveScreenshot()
  })

  test('密码深链知情确认：已勾选可继续', async ({ page }) => {
    await openStory(page, 'SmartTerminal/PasswordRiskAckChecked', '[data-testid="password-ack-modal"]')
    await expect(page.locator('[data-testid="password-ack-checkbox"]')).toBeChecked()
    await expect(page.locator('[data-testid="password-ack-continue"]')).toBeEnabled()
    await expect(page.locator('[data-testid="password-ack-mode"]')).toHaveText('intranet')
    await expect(root(page)).toHaveScreenshot()
  })

  test('URL 本地启动横幅', async ({ page }) => {
    await openStory(page, 'SmartTerminal/UrlLaunchLocal', '[data-testid="launch-banner"]')
    await expect(page.locator('[data-testid="launch-banner"]')).toContainText('profile=local')
    await expect(root(page)).toHaveScreenshot()
  })

  test('URL SSH 启动待连接 + 密码警告', async ({ page }) => {
    await openStory(page, 'SmartTerminal/UrlLaunchSshPending', '[data-testid="launch-warning"]')
    await expect(page.locator('[data-testid="launch-banner"]')).toContainText('password=***REDACTED***')
    await expect(page.locator('[data-testid="launch-warning"]')).toContainText('已从 URL 擦除')
    await expect(page.locator('[data-testid="console"]')).not.toContainText('SuperSecret')
    await expect(root(page)).toHaveScreenshot()
  })

  test('交互式密码遮罩输入', async ({ page }) => {
    await openStory(page, 'SmartTerminal/InteractivePassword', '[data-testid="interactive-password-modal"]')
    await expect(page.getByTestId('interactive-password-input')).toHaveAttribute('type', 'password')
    await expect(page.getByTestId('interactive-password-confirm')).toBeDisabled()
    await expect(page.getByTestId('interactive-password-target')).toContainText('root@10.0.0.1')
  })

  test('custom 不可用红条', async ({ page }) => {
    await openStory(page, 'SmartTerminal/CustomUnsupported', '[data-testid="custom-unsupported-banner"]')
    const banner = page.getByTestId('custom-unsupported-banner')
    await expect(banner).toBeVisible()
    await expect(banner).toContainText('本里程碑不支持 custom')
    await expect(banner).toContainText('custom:build-box')
    await expect(banner).toContainText('不会静默当作 local')
    await expect(root(page)).toHaveScreenshot()
  })

  test('S-R 刷新恢复前（对照）', async ({ page }) => {
    await openStory(page, 'SmartTerminal/SessionRestoreBefore', '[data-testid="tab-bar"]')
    await expect(page.locator('[data-testid="tab-bar"] .tab')).toHaveCount(2)
    await expect(root(page)).toContainText('跳板 SSH')
    await expect(root(page)).toContainText('ops@10.0.0.8')
    await expect(page.getByTestId('console')).toContainText('Welcome to Ubuntu')
    await expect(page.getByTestId('process-alive-yes')).toBeVisible()
    await expect(root(page)).toHaveScreenshot()
  })

  test('S-R 刷新恢复后：缓冲仍在且 sessionStorage 无密码', async ({ page }) => {
    await openStory(page, 'SmartTerminal/SessionRestored', '[data-testid="reconnect-banner"]')
    await expect(page.locator('[data-testid="tab-bar"] .tab')).toHaveCount(2)
    await expect(page.getByTestId('console')).toContainText('已从本页会话恢复')
    await expect(page.getByTestId('reconnect-banner')).toContainText('缺密 SSH')
    await expect(page.getByTestId('process-alive-no')).toBeVisible()
    await expect(page.getByTestId('reconnect-password-btn')).toBeVisible()

    const raw = await page.evaluate(() =>
      sessionStorage.getItem('forge.self.smart-terminal.session-restore.v1'),
    )
    expect(raw, '应写入恢复快照').toBeTruthy()
    expect(raw!).not.toMatch(/SuperSecretCT/)
    expect(raw!).not.toMatch(/"password"\s*:/)
    expect(raw!).toContain('sess-ssh')
    expect(raw!).toContain('10.0.0.8')

    await expect(root(page)).toHaveScreenshot()
  })

})
