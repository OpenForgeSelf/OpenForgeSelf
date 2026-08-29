import { test, expect } from '@playwright/test';

/**
 * SPA Fallback E2E 测试 —— 验证发布模式下后端能正确 serve 前端 SPA。
 *
 * 测试目标（对应 build.ps1 + AppBuilder.cs 中的 SPA Fallback 中间件）：
 * - 前端路由（无后缀、非 API 前缀）→ 返回 index.html，Vue Router 接管
 * - API 请求 → 不走 Fallback，正常返回 JSON
 * - 静态资源请求 → 不走 Fallback，正常返回文件
 * - 首页 → 正常 200
 *
 * 本文件由 playwright.e2e-published.config.ts 驱动，
 * 该配置的 webServer 直接启动后端发布产物（dotnet publish 后的 exe），
 * 不依赖前端 dev server，模拟生产部署环境。
 *
 * 设计原则（对齐 Playwright 官方最佳实践）：
 * - ✅ 使用语义化 Locator：getByRole / getByText，避免 CSS selector
 * - ✅ 无 waitForTimeout：全部 auto-waiting（expect().toBeVisible() / toHaveTitle()）
 * - ✅ 无状态依赖：每个用例独立，无共享状态
 * - ✅ 测试每种场景：前端路由 / API / 静态资源 / 404
 * - ❌ 不 mock API：本测试验证的是发布后端的集成行为，需要真实后端响应
 */

test.describe('SPA Fallback - 发布模式前端路由', () => {
  // ── 首页 ──
  test('首页 / 返回 200，页面标题正确', async ({ page }) => {
    const resp = await page.goto('/');
    expect(resp?.status()).toBe(200);
    await expect(page).toHaveTitle(/ForgeSelf/);
  });

  // ── 前端路由（Vue Router 路径）──
  // 这些路径在后端没有对应静态文件，需由 SPA Fallback 返回 index.html
  const frontendRoutes = [
    { path: '/ai-agent', desc: '智能体' },
    { path: '/agents', desc: '智能体管理' },
    { path: '/settings', desc: '设置' },
    { path: '/todo', desc: '待办' },
    { path: '/memory', desc: '记忆' },
    { path: '/workflows', desc: '工作流' },
    { path: '/system-monitor', desc: '系统监控' },
    { path: '/mcp-tools', desc: 'MCP 工具' },
    { path: '/code-snippets', desc: '代码片段' },
    { path: '/prompts', desc: '提示词' },
    { path: '/skills', desc: '技能' },
    { path: '/all-features', desc: '所有功能' },
    { path: '/plugins', desc: '插件商店' },
    { path: '/chat-records', desc: '聊天记录' },
    { path: '/profile', desc: '个人页' },
  ];

  for (const route of frontendRoutes) {
    test(`前端路由 ${route.path} (${route.desc}) 返回 200 且含 Vue 挂载标记`, async ({ page }) => {
      const resp = await page.goto(route.path);
      // 状态码 200 —— SPA Fallback 正确返回 index.html
      expect(resp?.status()).toBe(200);
      // Vue 挂载点存在 —— 说明返回了真正的 index.html 而非后端 404 页面
      await expect(page.locator('#app')).toBeAttached();
    });
  }

  // ── 深层前端路由（多级路径）──
  test('深层前端路由 /plugins/scaffolder 返回 200', async ({ page }) => {
    const resp = await page.goto('/plugins/scaffolder');
    expect(resp?.status()).toBe(200);
    await expect(page.locator('#app')).toBeAttached();
  });

  // ── 未注册的前端路由 ──
  test('任意未注册路径也返回 index.html（不会 404 白页）', async ({ page }) => {
    const resp = await page.goto('/some-random-path-that-does-not-exist');
    expect(resp?.status()).toBe(200);
    await expect(page.locator('#app')).toBeAttached();
  });
});

test.describe('SPA Fallback - API 请求不被拦截', () => {
  test('GET /api/health 返回 200 且为 JSON', async ({ page }) => {
    const resp = await page.goto('/api/health');
    expect(resp?.status()).toBe(200);
    const contentType = resp?.headers()['content-type'] ?? '';
    expect(contentType).toContain('application/json');
  });

  test('GET /api/todos 返回 JSON（非 HTML）', async ({ page }) => {
    const resp = await page.goto('/api/todos');
    // 即使没有数据也应该返回 JSON，而非 SPA Fallback 的 index.html
    const contentType = resp?.headers()['content-type'] ?? '';
    expect(contentType).toContain('application/json');
  });

  test('GET /v1/models 不走 Fallback（需 API Key 认证，返回 401）', async ({ page }) => {
    const resp = await page.goto('/v1/models');
    // /v1 是已知 API 前缀，不走 SPA Fallback（返回 401 而非 200 index.html）。
    expect(resp?.status()).toBe(401);
  });
});

test.describe('SPA Fallback - 静态资源正常返回', () => {
  test('JS 包返回 200 且 content-type 正确', async ({ page }) => {
    // 先访问首页让 Vite 生成的文件能被发现
    await page.goto('/');
    // 从页面找到 script src
    const scripts = await page.locator('script[src]').all();
    expect(scripts.length).toBeGreaterThan(0);
    for (const script of scripts) {
      const src = await script.getAttribute('src');
      if (!src) continue;
      // 用 page.goto 直接访问 JS 资源
      const resp = await page.goto(src);
      expect(resp?.status()).toBe(200);
      const ct = resp?.headers()['content-type'] ?? '';
      expect(ct).toContain('javascript');
    }
  });

  test('CSS 包返回 200 且 content-type 正确', async ({ page }) => {
    await page.goto('/');
    const links = await page.locator('link[rel="stylesheet"]').all();
    expect(links.length).toBeGreaterThan(0);
    for (const link of links) {
      const href = await link.getAttribute('href');
      if (!href) continue;
      const resp = await page.goto(href);
      expect(resp?.status()).toBe(200);
      const ct = resp?.headers()['content-type'] ?? '';
      expect(ct).toContain('css'); // text/css 或 text/css; charset=utf-8
    }
  });
});

test.describe('SPA Fallback - 控制台无异常', () => {
  test('前端路由 /ai-agent 无控制台报错', async ({ page }) => {
    const consoleErrors: string[] = [];
    page.on('console', msg => {
      if (msg.type() === 'error') {
        consoleErrors.push(msg.text());
      }
    });

    await page.goto('/ai-agent');
    // 等待 Vue 挂载并渲染核心内容（替代固定等待，收集渲染期错误）
    await expect(page.locator('.agent-view')).toBeVisible();

    // 允许超时类错误（如网络请求超时），不允许其他错误
    const criticalErrors = consoleErrors.filter(e => !e.includes('net::ERR_TIMEOUT'));
    expect(criticalErrors).toEqual([]);
  });
});
