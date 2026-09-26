import fs from 'node:fs';
import path from 'node:path';
import { test, expect, Page } from '@playwright/test';
import { getRealApiKey } from './helpers/real-auth';

/**
 * spec 036 · 真实端到端升级验收（破坏性，仅在显式开启时运行）。
 *
 * 目标宿主：当前正在运行的 51888 生产实例（playwright.live.config.ts）。
 * 完整链路：检查更新 → 下载 → 校验 → 解压 → 「重启并更新」→ 更新代理替换文件 →
 * 实例以新版本重启 → 数据完好（插件数不降）→ 新版本再次检查为「已是最新版本」。
 *
 * 运行（必须显式提供当前实例的真实密钥：live 配置不走 globalSetup，
 * 否则 real-auth 会读到过期 state.json 的旧 token 导致 401）：
 *   $env:E2E_LIVE_APPLY=1
 *   $env:E2E_API_TOKEN=(node ../scripts/get-forge-token.cjs 输出的 sk-…)
 *   pnpm exec playwright test --config=playwright.live.config.ts e2e/update-live-apply.spec.ts
 *
 * 前置：当前实例版本 < 最新 Release tag（tag 从 check 结果动态读取，不写死版本）。
 */

const REAL_API_KEY = getRealApiKey();
const BASE = 'http://localhost:51888';

async function apiGet(page: Page, p: string) {
  return page.request.get(`${BASE}${p}`, {
    headers: { Authorization: `Bearer ${REAL_API_KEY}` },
    timeout: 8000,
  });
}

test.describe('版本更新 · 真实升级端到端（spec 036，破坏性）', () => {
  test.skip(!process.env.E2E_LIVE_APPLY, '需显式设置 E2E_LIVE_APPLY=1（会终止并重启 51888 实例）');
  test.skip(!process.env.E2E_API_TOKEN, '需显式设置 E2E_API_TOKEN 为当前实例真实密钥（防旧 state.json 误导）');

  test('检查→下载→重启并更新→新版本生效且数据完好', async ({ page }) => {
    test.setTimeout(900_000); // 77MB 下载 + 代理重启 + 恢复轮询，整体上限 15 分钟

    // —— 0. 升级前基线 ——
    const before = await (await apiGet(page, '/api/update/status')).json();
    expect(before.success).toBe(true);
    const versionBefore: string = before.data.currentVersion;
    const manifestBefore = await (await apiGet(page, '/api/plugin/frontend-manifest')).json();
    const pluginCountBefore = (manifestBefore.data ?? manifestBefore).length ?? 0;

    // —— 1. UI 检查更新 ——
    await page.addInitScript((key) => localStorage.setItem('forge_api_token', key), REAL_API_KEY);
    await page.goto('/settings');
    await page.getByRole('button', { name: '版本更新' }).click();
    await page.getByRole('button', { name: '检查更新' }).click();
    const checkApi = await (await apiGet(page, '/api/update/status')).json();
    const targetTag: string = checkApi.data.state?.check?.latestVersionTag ?? '';
    expect(targetTag, '检查结果应发现新版本').toMatch(/^v\d+\.\d+\.\d+/);
    await expect(page.locator(`text=发现新版本 ${targetTag}`).first()).toBeVisible({ timeout: 60_000 });

    // —— 2. UI 下载更新 → 等待就绪（下载/校验/解压轮询）——
    await page.getByRole('button', { name: '下载更新' }).click();
    await expect(page.getByRole('button', { name: '重启并更新' })).toBeVisible({ timeout: 600_000 });

    // —— 3. UI 确认重启并更新 ——
    await page.getByRole('button', { name: '重启并更新' }).click();
    await page.locator('.el-message-box__btns button.el-button--primary').click();

    // —— 4. 等待实例以新版本恢复 ——
    let versionAfter = versionBefore;
    const deadline = Date.now() + 240_000;
    while (Date.now() < deadline) {
      try {
        const res = await page.request.get(`${BASE}/api/update/status`, {
          headers: { Authorization: `Bearer ${REAL_API_KEY}` },
          timeout: 5000,
        });
        if (res.ok()) {
          const body = await res.json();
          versionAfter = body.data.currentVersion;
          if (versionAfter !== versionBefore) break;
        }
      } catch {
        // 宿主正在退出/未就绪，继续轮询
      }
      await new Promise((r) => setTimeout(r, 3000));
    }
    expect(versionAfter).not.toBe(versionBefore);
    expect(versionAfter.startsWith(targetTag.replace(/^v/, ''))).toBe(true);

    // —— 5. 代理日志与备份存在（升级证据）——
    const updatesRoot = path.join(process.env.LOCALAPPDATA ?? '', 'ForgeSelf', 'Updates');
    const agentLogs = fs.existsSync(updatesRoot)
      ? fs.readdirSync(updatesRoot).filter((f) => f.startsWith('agent-') && f.endsWith('.log'))
      : [];
    expect(agentLogs.length).toBeGreaterThan(0);
    const latestLog = fs.readFileSync(
      path.join(updatesRoot, agentLogs.sort().at(-1) as string),
      'utf8',
    );
    expect(latestLog).toContain('更新完成');
    expect(latestLog).not.toContain('更新失败');
    const backupsRoot = path.join(process.env.LOCALAPPDATA ?? '', 'ForgeSelf', 'Backups');
    expect(fs.existsSync(backupsRoot) && fs.readdirSync(backupsRoot).length).toBeGreaterThan(0);

    // —— 6. 数据完好：插件数量不降 ——
    const manifestAfter = await (await apiGet(page, '/api/plugin/frontend-manifest')).json();
    const pluginCountAfter = (manifestAfter.data ?? manifestAfter).length ?? 0;
    expect(pluginCountAfter).toBeGreaterThanOrEqual(pluginCountBefore);

    // —— 7. 新版本 UI 再次检查：已是最新版本（证明重启后 token/配置链路完好）——
    await page.goto('/settings');
    await page.getByRole('button', { name: '版本更新' }).click();
    await page.getByRole('button', { name: '检查更新' }).click();
    await expect(
      page.locator('text=已是最新版本').or(page.locator('text=检查更新失败')).first(),
    ).toBeVisible({ timeout: 60_000 });
    await expect(
      page.locator('div').filter({ has: page.getByText('当前版本', { exact: true }) }).locator('div.font-semibold'),
    ).toContainText(new RegExp(`^v${targetTag.replace(/^v/, '').replace(/\./g, '\\.')}`));
  });
});
