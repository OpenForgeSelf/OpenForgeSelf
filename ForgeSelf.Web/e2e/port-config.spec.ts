import { test, expect, Page } from '@playwright/test';
import { readFileSync, writeFileSync } from 'node:fs';
import { getRealApiKey } from './helpers/real-auth';
import { backendUrl, forgeSettingConfigPath } from './helpers/e2e-env';

/**
 * 端口配置 E2E 测试 —— 对接真实后端 API。
 *
 * 设计原则：
 * - 注入真实 API 密钥到 localStorage（运行时从 ForgeSetting.config 解密，
 *   不硬编码——密钥轮换后硬编码值会失效导致 status 401），使认证请求通过
 * - 所有 API 调用走真实后端，不 mock
 * - 仅 mock 重启端点（POST /api/api-server/restart）与健康轮询：真实重启会
 *   切换监听端口并中断并行测试、破坏开发环境，属防破坏隔离；其余接口全部真实请求
 * - 保存测试会真实修改后端端口配置，测试后通过 API / 配置文件恢复，保证可重复运行
 * - **端口无关**（PILOT-050 T7）：后端地址走 E2E_BACKEND_URL / current.json 级联，
 *   断言用模板串；不再假设 7102，多 worktree 动态端口下同样成立
 */

// ============================================================
// 真实后端 API 密钥（运行时从 ForgeSetting.config 解密）
// ============================================================
const REAL_API_KEY = getRealApiKey();
const BACKEND_URL = backendUrl();
/** 实际监听端口（= 配置端口的期望值：UI 地址展示取自配置端口，二者一致断言才稳） */
const LISTENING_PORT = Number(new URL(BACKEND_URL).port);
/** 后端配置文件路径（{数据根}/config/ForgeSetting.config），用于恢复被测试保存污染的端口 */
const BACKEND_CONFIG_PATH = forgeSettingConfigPath();

// ============================================================
// 辅助函数
// ============================================================

/** 注入 API 密钥到 localStorage，使认证通过 */
async function injectApiKey(page: Page) {
  await page.addInitScript((key) => {
    localStorage.setItem('forge_api_token', key);
  }, REAL_API_KEY);
}

/** 导航到 API 服务器面板（auto-waiting：等待按钮可点击、面板加载完成） */
async function navigateToApiServer(page: Page) {
  await page.goto('/settings');
  await page.getByRole('button', { name: 'API 服务器' }).click();
  await expect(page.getByText('端口配置')).toBeVisible();
}

/** 从真实后端读取当前配置端口 */
async function getCurrentPort(): Promise<number> {
  const res = await fetch(`${BACKEND_URL}/api/portconfiguration`);
  const json = (await res.json()) as { data: { portNumber: number; }; };
  return json.data.portNumber;
}

/** 通过后端 API 保存端口（真实请求，需要认证） */
async function savePortViaApi(port: number): Promise<boolean> {
  const res = await fetch(`${BACKEND_URL}/api/portconfiguration`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${REAL_API_KEY}`,
    },
    body: JSON.stringify({ PortNumber: port }),
  });
  return res.ok;
}

/** 通过真实后端探测一个空闲端口（排除当前配置端口，避免前端"端口号未改变"短路） */
async function findAvailablePort(): Promise<number> {
  const currentPort = await getCurrentPort();
  for (let p = 8080; p < 8090; p++) {
    if (p === currentPort) continue;
    const res = await fetch(`${BACKEND_URL}/api/portconfiguration/check/${p}`);
    const json = (await res.json()) as { data: { isAvailable: boolean; }; };
    if (json.data.isAvailable === true) return p;
  }
  throw new Error('未找到可用端口');
}

/** 恢复端口配置：API 优先；原端口被监听时 API 会拒绝，改直接写配置文件 */
async function restorePort(originalPort: number): Promise<void> {
  // 已是最新端口，无需恢复
  if ((await getCurrentPort()) === originalPort) return;

  // 尝试 API 恢复
  const ok = await savePortViaApi(originalPort);
  if (ok) return;

  // API 恢复失败（原端口正被本进程监听）→ 直接改配置文件
  try {
    const content = readFileSync(BACKEND_CONFIG_PATH, 'utf8');
    const updated = content.replace(
      /<PortNumber>\d+<\/PortNumber>/,
      `<PortNumber>${originalPort}</PortNumber>`,
    );
    writeFileSync(BACKEND_CONFIG_PATH, updated, 'utf8');
  } catch (e) {
    // 文件回退失败 → 快速失败暴露（挂 cause 保留原始异常，preserve-caught-error）
    throw new Error(
      `恢复端口配置失败（文件回退写 ${BACKEND_CONFIG_PATH}）: ${e instanceof Error ? e.message : e}`,
      { cause: e },
    );
  }
  // 等待配置缓存刷新后验证；超时未刷新 → 快速失败（否则后续断言全部失真）
  for (let i = 0; i < 12; i++) {
    await new Promise((r) => setTimeout(r, 500));
    if ((await getCurrentPort()) === originalPort) return;
  }
  throw new Error(`恢复端口 ${originalPort} 后配置未刷新，当前仍为 ${await getCurrentPort()}`);
}

// ============================================================
// 测试用例 —— 串行执行，避免保存测试并发污染端口配置
// ============================================================

test.describe('端口配置管理（API 服务器面板）', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeAll(async () => {
    // 保证配置端口与实际监听端口一致（e2e 动态口），使地址展示断言稳定
    await restorePort(LISTENING_PORT);
  });

  test('加载 API 服务器页面，端口配置卡片可见', async ({ page }) => {
    await injectApiKey(page);
    await navigateToApiServer(page);

    // 端口配置卡片存在
    await expect(page.getByText('端口配置')).toBeVisible();
    // 显示当前端口地址（真实后端返回）
    await expect(page.getByText(`${BACKEND_URL}/v1`)).toBeVisible();
  });

  test('编辑端口并取消，端口恢复原始值', async ({ page }) => {
    await injectApiKey(page);
    await navigateToApiServer(page);
    await expect(page.getByText('端口配置')).toBeVisible();

    // 点击"编辑"进入编辑模式
    await page.getByRole('button', { name: '编辑' }).click();
    // 修改端口号
    await page.getByRole('spinbutton').fill('9999');
    // 点击"取消"
    await page.getByRole('button', { name: '取消' }).click();

    // 验证显示恢复为原始端口（真实后端监听口）
    await expect(page.getByText(`${BACKEND_URL}/v1`)).toBeVisible();
  });

  test('端口号超出范围时显示错误信息', async ({ page }) => {
    await injectApiKey(page);
    await navigateToApiServer(page);
    await expect(page.getByText('端口配置')).toBeVisible();
    await page.getByRole('button', { name: '编辑' }).click();

    // 输入 0（无效端口）
    await page.getByRole('spinbutton').fill('0');
    await page.getByRole('button', { name: '保存', exact: true }).click();
    await expect(page.getByText('端口号必须在 1-65535 之间')).toBeVisible();
  });

  test('端口可用性检查（真实后端）', async ({ page }) => {
    await injectApiKey(page);
    await navigateToApiServer(page);
    await expect(page.getByText('端口配置')).toBeVisible();
    await page.getByRole('button', { name: '编辑' }).click();

    // 探测一个空闲端口（排除当前配置端口，避免"端口号未改变"短路）
    const availablePort = await findAvailablePort();

    // 输入空闲端口
    await page.getByRole('spinbutton').fill(String(availablePort));
    await page.getByRole('button', { name: '保存', exact: true }).click();

    // 真实后端检查端口可用 → 应出现确认重启弹窗
    const confirmDialog = page.locator('.el-message-box');
    await expect(confirmDialog).toBeVisible({ timeout: 5000 });
    // 取消弹窗，不实际保存
    await confirmDialog.getByRole('button', { name: '取消' }).click();
    await expect(confirmDialog).toBeHidden();
  });

  test('保存端口配置并重启服务（真实 save + mock 重启）', async ({ page }) => {
    await injectApiKey(page);

    // 读取当前端口，选一个不同的测试端口（避免"端口号未改变"跳过流程）
    const originalPort = await getCurrentPort();
    const testPort = originalPort === 9090 ? 9091 : 9090;

    // 真实后端 API 全部走真实请求，不 mock
    // 仅 mock 重启端点，避免实际重启服务
    let restartCalled = false;
    await page.route('**/api/api-server/restart', async (route) => {
      restartCalled = true;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true, message: '服务正在重启...' }),
      });
    });

    // 模拟健康轮询：前几次 503，然后 200
    let healthCheckCount = 0;
    await page.route(`http://localhost:${testPort}/api/health`, async (route) => {
      healthCheckCount++;
      if (healthCheckCount < 4) {
        await route.fulfill({
          status: 503,
          contentType: 'application/json',
          body: JSON.stringify({ status: 'starting' }),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ status: 'healthy', port: testPort }),
        });
      }
    });

    try {
      await navigateToApiServer(page);
      await expect(page.getByText('端口配置')).toBeVisible();
      await page.getByRole('button', { name: '编辑' }).click();

      await page.getByRole('spinbutton').fill(String(testPort));
      await page.getByRole('button', { name: '保存', exact: true }).click();

      // 确认重启弹窗
      await page.getByRole('button', { name: '确定' }).click();

      // 等待成功消息
      await expect(page.getByText('服务已在新端口')).toBeVisible({ timeout: 15000 });
      expect(restartCalled).toBe(true);
    } finally {
      // 恢复端口配置，避免污染后续测试运行
      await restorePort(originalPort);
    }
  });

  test('重启超时处理', async ({ page }) => {
    test.setTimeout(70000);
    await injectApiKey(page);

    // 读取当前端口，选一个不同的测试端口
    const originalPort = await getCurrentPort();
    const testPort = originalPort === 9090 ? 9091 : 9090;

    // mock 重启端点
    await page.route('**/api/api-server/restart', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true, message: '服务正在重启...' }),
      });
    });

    // 健康检查始终返回 503（超时模拟）
    await page.route(`http://localhost:${testPort}/api/health`, async (route) => {
      await route.fulfill({
        status: 503,
        contentType: 'application/json',
        body: JSON.stringify({ status: 'starting' }),
      });
    });

    try {
      await navigateToApiServer(page);
      await expect(page.getByText('端口配置')).toBeVisible();
      await page.getByRole('button', { name: '编辑' }).click();

      await page.getByRole('spinbutton').fill(String(testPort));
      await page.getByRole('button', { name: '保存', exact: true }).click();

      // 确认重启弹窗
      await page.getByRole('button', { name: '确定' }).click();

      // 等待超时警告（pollForRestart 默认 60s 超时）
      await expect(page.getByText('服务重启超时')).toBeVisible({ timeout: 65000 });
    } finally {
      // 恢复端口配置
      await restorePort(originalPort);
    }
  });
});
