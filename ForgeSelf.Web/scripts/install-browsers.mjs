// 安装 Playwright 浏览器二进制到「仓库内固定路径」<repo>/.playwright-browsers。
//
// 背景：Playwright 的 npm 包（@playwright/test）由 pnpm 正常安装，无需重复下载；
// 但浏览器二进制（chromium ~180MB）是独立产物，默认落在用户级缓存 ~/.cache/ms-playwright。
// 沙箱只持久化工作区、用户级缓存属临时态，重置即清空 → 每次都要重下。
//
// 解法：PLAYWRIGHT_BROWSERS_PATH 指向仓库内 .playwright-browsers（已在根 .gitignore 忽略），
// 随工作区一起留存，重置后无需再下。此值与 playwright.config.ts /
// playwright.e2e-published.config.ts / playwright.live.config.ts 顶部的默认设置保持一致。
//
// 用法：pnpm run browsers:install
import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'

const __dirname = dirname(fileURLToPath(import.meta.url))
// scripts/ → ForgeSelf.Web/ → 仓库根
const browsersPath = resolve(__dirname, '..', '..', '.playwright-browsers')
process.env.PLAYWRIGHT_BROWSERS_PATH = browsersPath

console.log(`[install-browsers] 目标路径：${browsersPath}`)
try {
  execFileSync('playwright', ['install', 'chromium'], { stdio: 'inherit', shell: true })
  console.log('[install-browsers] chromium 已安装到仓库内 .playwright-browsers')
} catch (err) {
  console.error('[install-browsers] 安装失败：', err?.message ?? err)
  process.exit(1)
}
