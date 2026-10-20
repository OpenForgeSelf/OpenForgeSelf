import { test, expect, type Page } from '@playwright/test';
import { getRealApiKey } from './helpers/real-auth';
import { backendUrl } from './helpers/e2e-env';

/**
 * 网络设置（模型请求代理）E2E —— 2026-10-16-settings-proxy-mode-bypass S5（AC-7/AC-UX）
 * 模式沿用 port-config.spec.ts：真实前后端、零 mock；token 注入 localStorage。
 *
 * 污染控制：本 spec 会真实改写 ForgeSetting 的代理配置（进程级全局），因此：
 * - describe 串行（mode: serial），避免同批用例互相踩；
 * - beforeAll 备份原配置并归零到 system，afterAll 恢复（proxyPassword 不提交=不修改，永不触碰）；
 * - 开启手动代理的用例窗口尽量短；本机回环永不走代理（BR-2），
 *   故并行 spec 对本地模型端点的请求不受本 spec 开启态影响。
 */

const REAL_API_KEY = getRealApiKey();
const BACKEND_URL = backendUrl();

interface NetworkState {
  proxyMode: 'system' | 'manual' | 'direct';
  proxyBypass: string[];
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

async function selectMode(page: Page, label: '跟随系统' | '手动配置' | '直连·不走代理') {
  await page.getByTestId('proxy-mode-item').filter({ hasText: label }).click();
}

test.describe('网络设置面板（模型请求代理·三模式）', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeAll(async () => {
    original = await apiGetSettings();
    // 归零起点：跟随系统（地址/端口/旁路保留原值不影响渲染判定；旁路清空防串扰）
    if (original.proxyMode !== 'system' || original.proxyBypass.length > 0) {
      const r = await apiPostSettings({ proxyMode: 'system', proxyBypass: [] });
      expect(r.ok).toBe(true);
    }
  });

  test.afterAll(async () => {
    // 恢复备份（不提交 proxyPassword=不修改）
    await apiPostSettings({
      proxyMode: original.proxyMode,
      proxyBypass: original.proxyBypass,
      proxyHost: original.proxyHost,
      proxyPort: original.proxyPort,
      proxyUsername: original.proxyUsername,
    });
  });

  test('① system 默认态：显示系统代理说明 + 旁路区，无手动表单（FR-1/FR-2）', async ({ page }) => {
    await injectApiKey(page);
    await navigateToNetwork(page);

    await expect(page.getByTestId('proxy-mode-group')).toBeVisible();
    await expect(page.getByText('按 Windows 系统代理设置')).toBeVisible();
    await expect(page.getByPlaceholder('如 proxy.example.com')).toHaveCount(0);
    await expect(page.locator('#bypass-list')).toBeVisible();
    await page.screenshot({ path: 'screenshots/e2e/settings-network-system.png', fullPage: true });

    // 直连态：旁路区隐藏（全部直连时旁路无意义）
    await selectMode(page, '直连·不走代理');
    await expect(page.getByText('当前不发任何代理')).toBeVisible();
    await expect(page.locator('#bypass-list')).toHaveCount(0);
    await selectMode(page, '跟随系统');
  });

  test('② manual 非法地址：行内校验拦截不落盘（BR-5/AC-UX-2）', async ({ page }) => {
    await injectApiKey(page);
    await navigateToNetwork(page);

    await selectMode(page, '手动配置');
    await expect(page.getByPlaceholder('如 proxy.example.com')).toBeVisible();

    // 空地址直接保存 → 前端拦截，不发 POST
    await page.getByTestId('save-btn').click();
    await expect(page.getByTestId('host-error')).toContainText('请输入代理服务器地址');

    // 带 scheme 的地址 → 前端文案与后端同规则
    // 回归注意：行内错误文案必须常驻等高占位（NetworkPanel 错误行 h-4），否则端口 blur 提交清错
    // 会在 mousedown/mouseup 之间把保存按钮上移 20px，鼠标点击被布局跳动吞掉（2026-10-16 e2e 实证）
    await page.getByLabel('代理服务器地址').fill('http://proxy.local');
    await page.getByLabel('代理端口').fill('7890');
    await page.getByTestId('save-btn').click();
    await expect(page.getByTestId('host-error')).toContainText('仅支持域名或 IPv4');

    // 后端确认未落盘（仍是 beforeAll 归零后的 system）
    const s = await apiGetSettings();
    expect(s.proxyMode).toBe('system');
    await page.screenshot({ path: 'screenshots/e2e/settings-network-invalid.png', fullPage: true });
  });

  test('③ 主链路：manual 保存 + 旁路添加/删除 + 刷新持久化（FR-1/FR-2/FR-4）', async ({ page }) => {
    await injectApiKey(page);
    await navigateToNetwork(page);

    try {
      await selectMode(page, '手动配置');
      await page.getByLabel('代理服务器地址').fill('proxy.example.com');
      await page.getByLabel('代理端口').fill('7890');
      await page.getByLabel('代理认证用户名').fill('e2e-user');

      // 添加旁路条目（交互规格：点「添加条目」→ 尾部新行）
      await page.getByTestId('bypass-add-btn').click();
      await page.getByLabel('不走代理的条目 1').fill('*.e2e.internal');

      await page.getByTestId('save-btn').click();
      await expect(page.getByText('网络设置已保存')).toBeVisible({ timeout: 5000 });

      // 后端真实落盘（GET 回显，密码永不回传）
      const s = await apiGetSettings();
      expect(s.proxyMode).toBe('manual');
      expect(s.proxyHost).toBe('proxy.example.com');
      expect(s.proxyPort).toBe(7890);
      expect(s.proxyUsername).toBe('e2e-user');
      expect(s.proxyBypass).toEqual(['*.e2e.internal']);
      expect(s.proxyPasswordSet).toBe(false);

      // 刷新重进仍在（点即保存落盘）
      await page.reload();
      await page.getByRole('button', { name: '网络设置' }).click();
      await expect(page.getByLabel('代理服务器地址')).toHaveValue('proxy.example.com');
      await expect(page.getByTestId('bypass-row')).toHaveCount(1);

      // 删除条目 → 保存 → 后端旁路清空
      await page.getByTestId('bypass-remove-btn').click();
      await page.getByTestId('save-btn').click();
      await expect(page.getByText('网络设置已保存')).toBeVisible({ timeout: 5000 });
      const s2 = await apiGetSettings();
      expect(s2.proxyBypass).toEqual([]);
      await page.screenshot({ path: 'screenshots/e2e/settings-network-manual.png', fullPage: true });
    } finally {
      await apiPostSettings({ proxyMode: 'system', proxyBypass: [] });
    }
  });

  test('④ 测试按钮：不可达代理 → 行内红色错误文本（FR-3/BR-4）', async ({ page }) => {
    await injectApiKey(page);
    await navigateToNetwork(page);

    await selectMode(page, '手动配置');
    // 127.0.0.1:1 恒拒绝连接（不依赖外网）；未保存的表单值直接测（BR-4 零副作用）
    await page.getByLabel('代理服务器地址').fill('127.0.0.1');
    await page.getByLabel('代理端口').fill('1');
    await page.getByTestId('proxy-test-btn').click();

    // 后端探活失败透出最内层原因（如「由于目标计算机积极拒绝」）；判据=不得出现「代理可用」
    await expect(page.getByTestId('proxy-test-feedback')).toContainText(/拒绝|refused|失败|无法连接|超时/i, { timeout: 15000 });
    await expect(page.getByTestId('proxy-test-feedback')).not.toContainText('代理可用');

    // 测试不产生配置副作用
    const s = await apiGetSettings();
    expect(s.proxyMode).toBe('system');
    await page.screenshot({ path: 'screenshots/e2e/settings-network-test-fail.png', fullPage: true });
  });
});
