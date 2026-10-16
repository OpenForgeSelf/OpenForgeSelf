import { test, expect, type Page } from '@playwright/test';
import { getRealApiKey } from './helpers/real-auth';
import { backendUrl } from './helpers/e2e-env';

/**
 * 网络设置（模型请求代理）E2E —— 2026-10-16-settings-network-proxy AC-3/AC-UX
 * 模式沿用 port-config.spec.ts：真实前后端、零 mock；token 注入 localStorage。
 *
 * 污染控制：本 spec 会真实改写 ForgeSetting 的代理配置（进程级全局），因此：
 * - describe 串行（mode: serial），避免同批用例互相踩；
 * - beforeAll 备份原配置，afterAll 恢复（proxyPassword 不提交=不修改，永不触碰）；
 * - 开启代理的用例窗口尽量短；本机回环目标永不走代理（BR-2），
 *   故并行 spec 对本地模型端点的请求不受本 spec 开启态影响。
 */

const REAL_API_KEY = getRealApiKey();
const BACKEND_URL = backendUrl();

interface NetworkState {
  proxyEnabled: boolean;
  proxyHost: string;
  proxyPort: number;
  proxyUsername: string;
  proxyPasswordSet: boolean;
}

let original: NetworkState;

async function apiGetSettings(): Promise<NetworkState> {
  const res = await fetch(`${BACKEND_URL}/api/settings`, {
    headers: { Authorization: `Bearer ${REAL_API_KEY}` },
  });
  expect(res.ok, `GET /api/settings 应成功（401=token 失效，立即上报）`).toBe(true);
  const json = (await res.json()) as { data: NetworkState };
  return json.data;
}

async function apiPostSettings(payload: Record<string, unknown>): Promise<Response> {
  return fetch(`${BACKEND_URL}/api/settings`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${REAL_API_KEY}`,
    },
    body: JSON.stringify(payload),
  });
}

async function injectApiKey(page: Page) {
  await page.addInitScript((key) => {
    localStorage.setItem('forge_api_token', key);
  }, REAL_API_KEY);
}

async function navigateToNetwork(page: Page) {
  await page.goto('/settings');
  await page.getByRole('button', { name: '网络设置' }).click();
  await expect(page.locator('.network-panel')).toBeVisible();
}

test.describe('网络设置面板（模型请求代理）', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeAll(async () => {
    original = await apiGetSettings();
    // 归零起点：关闭代理（地址/端口保留原值不影响判定）
    if (original.proxyEnabled) {
      const r = await apiPostSettings({ proxyEnabled: false });
      expect(r.ok).toBe(true);
    }
  });

  test.afterAll(async () => {
    // 恢复备份（不提交 proxyPassword=不修改）
    await apiPostSettings({
      proxyEnabled: original.proxyEnabled,
      proxyHost: original.proxyHost,
      proxyPort: original.proxyPort,
      proxyUsername: original.proxyUsername,
    });
  });

  test('关闭态：面板加载显示直连说明，不渲染代理表单', async ({ page }) => {
    await injectApiKey(page);
    await navigateToNetwork(page);

    await expect(page.getByText('当前不走代理')).toBeVisible();
    await expect(page.getByPlaceholder('如 127.0.0.1 或 proxy.example.com')).toHaveCount(0);
    await page.screenshot({ path: 'screenshots/e2e/settings-network-off.png', fullPage: true });
  });

  test('开启但地址非法：行内校验拦截，后端不落盘（AC-UX-2/BR-5）', async ({ page }) => {
    await injectApiKey(page);
    await navigateToNetwork(page);

    await page.getByTestId('proxy-switch').click();
    await expect(page.getByPlaceholder('如 127.0.0.1 或 proxy.example.com')).toBeVisible();

    // 空地址直接保存 → 前端拦截
    await page.getByTestId('save-btn').click();
    await expect(page.getByTestId('host-error')).toContainText('请输入代理服务器地址');

    // 带 scheme 的地址 → 前端文案与后端同规则
    // 回归注意：行内错误文案必须常驻等高占位（NetworkPanel 错误行 h-4），否则端口 blur 提交清错
    // 会在 mousedown/mouseup 之间把保存按钮上移 20px，鼠标点击被布局跳动吞掉（2026-10-16 e2e 实证）
    await page.getByLabel('代理服务器地址').fill('http://proxy.local');
    await page.getByPlaceholder('如 7890').fill('7890');
    await page.getByTestId('save-btn').click();
    await expect(page.getByTestId('host-error')).toContainText('仅支持域名或 IPv4');

    // 后端确认未落盘
    const s = await apiGetSettings();
    expect(s.proxyEnabled).toBe(false);
    await page.screenshot({ path: 'screenshots/e2e/settings-network-invalid.png', fullPage: true });

    // 关回关闭态，避免残留开启表单影响下一条
    await page.getByTestId('proxy-switch').click();
  });

  test('主链路：开启→保存→提示生效→刷新持久化→后端配置一致', async ({ page }) => {
    await injectApiKey(page);
    await navigateToNetwork(page);

    try {
      await page.getByTestId('proxy-switch').click();
      await page.getByLabel('代理服务器地址').fill('proxy.example.com');
      await page.getByPlaceholder('如 7890').fill('7890');
      await page.getByLabel('代理认证用户名').fill('e2e-user');
      await page.getByTestId('save-btn').click();

      // 保存反馈（成败可见）
      await expect(page.getByText('网络设置已保存')).toBeVisible({ timeout: 5000 });

      // 后端真实落盘（GET 回显，密码永不回传）
      const s = await apiGetSettings();
      expect(s.proxyEnabled).toBe(true);
      expect(s.proxyHost).toBe('proxy.example.com');
      expect(s.proxyPort).toBe(7890);
      expect(s.proxyUsername).toBe('e2e-user');
      expect(s.proxyPasswordSet).toBe(false);

      // 刷新重进仍在（点即保存落盘）
      await page.reload();
      await page.getByRole('button', { name: '网络设置' }).click();
      await expect(page.getByLabel('代理服务器地址')).toHaveValue('proxy.example.com');
      await page.screenshot({ path: 'screenshots/e2e/settings-network-on.png', fullPage: true });
    } finally {
      await apiPostSettings({ proxyEnabled: false });
    }
  });
});
