/**
 * SmartTerminal 组件视觉回归（CT）
 *
 * 运行：pnpm run test:ct        更新基线：pnpm run test:ct:update
 */
import { test, expect } from '@playwright/test'
import { openStory } from './ct-support'

const root = (page: import('@playwright/test').Page) => page.locator('[data-testid="smart-terminal-root"]')

test.describe('SmartTerminal · 视觉与契约', () => {
  test('空会话：展示 Linux 壳选项与空列表提示', async ({ page }) => {
    await openStory(page, 'SmartTerminal/EmptySessions', '[data-testid="smart-terminal-root"]')
    await expect(root(page)).toContainText('暂无会话')
    const picker = page.locator('[data-testid="shell-picker"]')
    await expect(picker.locator('option[value="bash"]')).toHaveCount(1)
    await expect(picker.locator('option[value="zsh"]')).toHaveCount(1)
    await expect(picker.locator('option[value="sh"]')).toHaveCount(1)
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
    await expect(page.locator('[data-testid="command-history"]')).toContainText('***REDACTED***')
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

  test('Linux 多壳选择器', async ({ page }) => {
    await openStory(page, 'SmartTerminal/LinuxShellPicker', '[data-testid="shell-picker"]')
    await expect(page.locator('[data-testid="shell-picker"]')).toHaveValue('zsh')
    await expect(root(page)).toContainText('Zsh 会话')
    await expect(root(page)).toHaveScreenshot()
  })
})
