// 探测 esbuild（Go 二进制）能否在 <web>/node_modules/.vite 下真实写盘。
// 背景（AGENTS.md §2.3 / dev-stack.ps1 头注）：沙箱环境下 Node fs 同目录写成功、
// esbuild 写 deps_temp_* 却 Access is denied —— 故必须让 esbuild 本尊写，不能用 fs 写代替。
// 用法：node scripts/probe-esbuild-write.mjs <ForgeSelf.Web 目录>
// 输出：stdout 单行 JSON { "canWrite": bool, "esbuildVersion": string, "error"?: string }
// 消费方：scripts/dev-stack.ps1 —— canWrite=false 时生成 noDiscovery 配置关闭依赖预打包。
// ⚠ 本文件必须随 git 入库（历史上因未入库遗失，导致 dev-stack 静默降级为无预包模式，
//   element-plus 直出裸 pnpm 路径 → 其 import 'dayjs' 解析到 UMD 无 default 导出 → SPA 白屏）。
import { mkdirSync, mkdtempSync, rmSync, realpathSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { createRequire } from 'node:module';

const webRoot = resolve(process.argv[2] || '.');
const viteDir = join(webRoot, 'node_modules', '.vite');
let esbuildVersion = 'unknown';
try {
  mkdirSync(viteDir, { recursive: true });
  // esbuild 是 vite 的传递依赖（pnpm 隔离，node_modules/vite 为 Junction 且
  // createRequire 不做 realpath —— 必须先 realpath 到 .pnpm 真身再解析其私有依赖）。
  let esbuild;
  try {
    const require = createRequire(join(webRoot, 'package.json'));
    esbuild = require('esbuild');
  } catch {
    const viteReal = realpathSync(join(webRoot, 'node_modules', 'vite'));
    const viteRequire = createRequire(join(viteReal, 'package.json'));
    esbuild = viteRequire('esbuild');
  }
  esbuildVersion = esbuild.version || 'unknown';
  const outdir = mkdtempSync(join(viteDir, 'probe-'));
  try {
    await esbuild.build({
      stdin: { contents: 'export const probe = 1;', loader: 'js', resolveDir: outdir },
      outfile: join(outdir, 'probe.js'),
      bundle: true,
      write: true,
      logLevel: 'silent',
    });
  } finally {
    rmSync(outdir, { recursive: true, force: true });
  }
  console.log(JSON.stringify({ canWrite: true, esbuildVersion }));
} catch (err) {
  console.log(JSON.stringify({ canWrite: false, esbuildVersion, error: String((err && err.message) || err) }));
}