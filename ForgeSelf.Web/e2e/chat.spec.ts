import { test, expect } from '@playwright/test'

test.describe('Chat Application', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/')
  })

  test('should load the chat page', async ({ page }) => {
    // Check the main title
    await expect(page.locator('h1')).toContainText('铸己匣 - ForgeSelf')
    
    // Check that the chat view structure exists
    await expect(page.locator('.chat-view')).toBeVisible()
    await expect(page.locator('.chat-header')).toBeVisible()
    await expect(page.locator('.chat-main')).toBeVisible()
  })

  test('should display connection status', async ({ page }) => {
    // Check that connection status is visible
    const statusElement = page.locator('.connection-status')
    await expect(statusElement).toBeVisible()
    
    // Initially should show offline status
    await expect(statusElement).toContainText('离线')
  })

  test('should display empty state when no messages', async ({ page }) => {
    // Check that empty state is visible
    await expect(page.locator('.empty-state')).toBeVisible()
    await expect(page.locator('.empty-icon')).toContainText('💬')
    await expect(page.locator('.empty-title')).toContainText('开始对话')
    await expect(page.locator('.empty-description')).toContainText('在下方输入框中输入消息')
  })

  test('should have message input visible', async ({ page }) => {
    // Check that message input is visible
    await expect(page.locator('.message-input')).toBeVisible()
    await expect(page.locator('.input-textarea')).toBeVisible()
    await expect(page.locator('.send-button')).toBeVisible()
  })

  test('should have menu button for sidebar', async ({ page }) => {
    // Check that menu button exists
    await expect(page.locator('.menu-button')).toBeVisible()
  })

  test('should toggle sidebar when clicking menu button', async ({ page }) => {
    // Sidebar should be hidden initially
    await expect(page.locator('.sidebar-overlay')).not.toBeVisible()
    
    // Click menu button to open sidebar
    await page.click('.menu-button')
    await expect(page.locator('.sidebar-overlay')).toBeVisible()
    await expect(page.locator('.sidebar')).toBeVisible()
    
    // Click menu button again to close sidebar
    await page.click('.menu-button')
    await expect(page.locator('.sidebar-overlay')).not.toBeVisible()
  })

  test('should close sidebar when clicking overlay', async ({ page }) => {
    // Open sidebar
    await page.click('.menu-button')
    await expect(page.locator('.sidebar-overlay')).toBeVisible()
    
    // Click overlay to close
    await page.click('.sidebar-overlay')
    await expect(page.locator('.sidebar-overlay')).not.toBeVisible()
  })

  test('should have new chat button in sidebar', async ({ page }) => {
    // Open sidebar
    await page.click('.menu-button')
    
    // Check new chat button
    await expect(page.locator('.new-chat-button')).toBeVisible()
    await expect(page.locator('.new-chat-button')).toContainText('新对话')
  })

  test('should display empty conversations message', async ({ page }) => {
    // Open sidebar
    await page.click('.menu-button')
    
    // Check empty conversations message
    await expect(page.locator('.empty-conversations')).toBeVisible()
    await expect(page.locator('.empty-conversations')).toContainText('暂无对话记录')
  })

  test('should have correct placeholder in textarea', async ({ page }) => {
    const textarea = page.locator('.input-textarea')
    await expect(textarea).toHaveAttribute('placeholder', '输入消息... (Enter发送, Shift+Enter换行)')
  })

  test('should have disabled send button when input is empty', async ({ page }) => {
    const sendButton = page.locator('.send-button')
    await expect(sendButton).toBeDisabled()
  })

  test('should enable send button when input has content', async ({ page }) => {
    const textarea = page.locator('.input-textarea')
    await textarea.fill('Test message')
    
    const sendButton = page.locator('.send-button')
    await expect(sendButton).not.toBeDisabled()
  })

  test('should clear input after clicking send button', async ({ page }) => {
    const textarea = page.locator('.input-textarea')
    await textarea.fill('Test message')
    
    await page.click('.send-button')
    
    // Input should be cleared
    await expect(textarea).toHaveValue('')
  })

  test('should show user message after sending', async ({ page }) => {
    const textarea = page.locator('.input-textarea')
    await textarea.fill('Hello, this is a test message')
    
    await page.click('.send-button')
    
    // Wait for message to appear
    await expect(page.locator('.message-item')).toBeVisible()
    
    // Check that the message content is displayed
    const messageText = page.locator('.message-text').first()
    await expect(messageText).toContainText('Hello, this is a test message')
    
    // Check that it's a user message
    await expect(page.locator('.message-item.message-user')).toBeVisible()
  })

  test('should show loading indicator after sending', async ({ page }) => {
    const textarea = page.locator('.input-textarea')
    await textarea.fill('Hello')
    
    await page.click('.send-button')
    
    // Loading indicator should appear
    await expect(page.locator('.loading-indicator')).toBeVisible()
    await expect(page.locator('.loading-text')).toContainText('AI正在思考')
  })

  test('should show hint text when input is enabled', async ({ page }) => {
    await expect(page.locator('.hint-text')).toContainText('按 Enter 发送')
  })

  test('should send message with Enter key', async ({ page }) => {
    const textarea = page.locator('.input-textarea')
    await textarea.fill('Test message with Enter')
    
    // Press Enter
    await textarea.press('Enter')
    
    // Input should be cleared
    await expect(textarea).toHaveValue('')
    
    // Message should appear
    await expect(page.locator('.message-item')).toBeVisible()
  })

  test('should not send message with Shift+Enter', async ({ page }) => {
    const textarea = page.locator('.input-textarea')
    await textarea.fill('Test message')
    
    // Press Shift+Enter
    await textarea.press('Shift+Enter')
    
    // Input should still have content (new line added)
    await expect(textarea).not.toHaveValue('')
    
    // No message should appear
    await expect(page.locator('.message-item')).not.toBeVisible()
  })

  test('should display user avatar correctly', async ({ page }) => {
    const textarea = page.locator('.input-textarea')
    await textarea.fill('Hello')
    await textarea.press('Enter')
    
    // Wait for message to appear
    await expect(page.locator('.message-item')).toBeVisible()
    
    // Check user avatar
    const avatar = page.locator('.avatar-icon.avatar-user')
    await expect(avatar).toBeVisible()
    await expect(avatar).toContainText('👤')
  })

  test('should display role label correctly', async ({ page }) => {
    const textarea = page.locator('.input-textarea')
    await textarea.fill('Hello')
    await textarea.press('Enter')
    
    // Wait for message to appear
    await expect(page.locator('.message-item')).toBeVisible()
    
    // Check role label
    const roleLabel = page.locator('.message-role').first()
    await expect(roleLabel).toContainText('你')
  })

  test('should display timestamp on message', async ({ page }) => {
    const textarea = page.locator('.input-textarea')
    await textarea.fill('Hello')
    await textarea.press('Enter')
    
    // Wait for message to appear
    await expect(page.locator('.message-item')).toBeVisible()
    
    // Check timestamp exists
    const timestamp = page.locator('.message-time').first()
    await expect(timestamp).toBeVisible()
    
    // Timestamp should be in HH:MM format
    const timeText = await timestamp.textContent()
    expect(timeText).toMatch(/^\d{2}:\d{2}$/)
  })

  test('should handle multiple messages', async ({ page }) => {
    const textarea = page.locator('.input-textarea')
    
    // Send first message
    await textarea.fill('First message')
    await textarea.press('Enter')
    await expect(page.locator('.message-item')).toBeVisible()
    
    // Send second message
    await textarea.fill('Second message')
    await textarea.press('Enter')
    
    // Should have two messages
    await expect(page.locator('.message-item').count()).toBeGreaterThanOrEqual(2)
  })

  test('should have correct page title', async ({ page }) => {
    await expect(page).toHaveTitle(/ForgeSelf/)
  })

  test('should show assistant response placeholder', async ({ page }) => {
    const textarea = page.locator('.input-textarea')
    await textarea.fill('Hello')
    await textarea.press('Enter')
    
    // Wait for assistant message to appear
    await expect(page.locator('.message-item.message-assistant')).toBeVisible()
    
    // Assistant avatar should be visible
    const assistantAvatar = page.locator('.avatar-icon.avatar-assistant')
    await expect(assistantAvatar).toBeVisible()
    await expect(assistantAvatar).toContainText('🤖')
  })

  test('should disable input while loading', async ({ page }) => {
    const textarea = page.locator('.input-textarea')
    await textarea.fill('Hello')
    await textarea.press('Enter')
    
    // While loading, textarea should be disabled
    await expect(textarea).toBeDisabled()
    
    // Hint should show loading message
    await expect(page.locator('.hint-text')).toContainText('AI正在回复中')
  })

  test('should handle whitespace-only input', async ({ page }) => {
    const textarea = page.locator('.input-textarea')
    
    // Try to send whitespace only
    await textarea.fill('   ')
    
    // Send button should still be disabled
    const sendButton = page.locator('.send-button')
    await expect(sendButton).toBeDisabled()
    
    // Pressing Enter should not send
    await textarea.press('Enter')
    
    // No message should appear
    await expect(page.locator('.message-item')).not.toBeVisible()
  })

  test('should trim whitespace from input', async ({ page }) => {
    const textarea = page.locator('.input-textarea')
    
    // Send message with whitespace
    await textarea.fill('  Hello World  ')
    await textarea.press('Enter')
    
    // Wait for message to appear
    await expect(page.locator('.message-item')).toBeVisible()
    
    // Message content should be trimmed
    const messageText = page.locator('.message-text').first()
    await expect(messageText).toContainText('Hello World')
    await expect(messageText).not.toContainText('  Hello World  ')
  })

  test('should have proper accessibility structure', async ({ page }) => {
    // Check that main interactive elements are accessible
    await expect(page.locator('.menu-button')).toBeVisible()
    await expect(page.locator('.input-textarea')).toBeVisible()
    await expect(page.locator('.send-button')).toBeVisible()
    
    // Check that send button has a title attribute
    const sendButton = page.locator('.send-button')
    await expect(sendButton).toHaveAttribute('title', '发送消息')
  })

  test('should maintain input focus behavior', async ({ page }) => {
    const textarea = page.locator('.input-textarea')
    
    // Focus on textarea
    await textarea.focus()
    await expect(textarea).toBeFocused()
    
    // Type some content
    await textarea.fill('Test')
    
    // Should still be focused
    await expect(textarea).toBeFocused()
  })
})