#!/usr/bin/env node
/**
 * postinstall 粘合层：还原包（pnpm/npm install）时自动安装 git hooks。
 *
 * 为什么不直接在 package.json 里内嵌 pwsh 命令：
 *   JSON 内嵌多层引号在 Windows cmd 与 POSIX shell 下语义不同，CI（ubuntu）易碎；
 *   本脚本集中处理「是否该装」的判断，真正的安装逻辑单一真源 = scripts/install-git-hooks.ps1。
 *
 * 行为约定（失败模式显式）：
 *   - 非 git 检出（无 <repoRoot>/.git；常规克隆为目录、worktree 为 gitdir 文件，均兼容）→ 打提示，exit 0（不阻塞还原）。
 *   - pwsh 不在 PATH（ENOENT）：
 *       · CI（GITHUB_ACTIONS/CI=true）→ exit 1（不允许静默无钩子）
 *       · 本地 → 打提示并附手动兜底命令，exit 0（不阻塞还原；pwsh 铁律见 AGENTS §2.3）
 *   - install-git-hooks.ps1 真实失败（源文件缺失、复制失败、不可执行等）→ 透传非零退出码
 *   - 安装成功后二次校验：git-common-dir/hooks/{pre-commit,commit-msg} 必须存在且可执行，否则 exit 1
 *     （防止 Cursor hooksPath / 644 导致「装了等于没装」——2026-11-19 实证）
 */
'use strict';

const { spawnSync } = require('node:child_process');
const { existsSync, accessSync, constants } = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..');
const inCi = process.env.GITHUB_ACTIONS === 'true' || process.env.CI === 'true';

function resolveHooksDir() {
  const r = spawnSync('git', ['-C', repoRoot, 'rev-parse', '--git-common-dir'], {
    encoding: 'utf8',
  });
  if (r.status !== 0 || !r.stdout) {
    return null;
  }
  const common = r.stdout.toString().trim();
  const abs = path.isAbsolute(common) ? common : path.resolve(repoRoot, common);
  return path.join(abs, 'hooks');
}

function assertHooksExecutable(hooksDir) {
  const names = ['pre-commit', 'commit-msg'];
  for (const name of names) {
    const p = path.join(hooksDir, name);
    if (!existsSync(p)) {
      console.error(`[postinstall] FAIL: missing hook after install: ${p}`);
      process.exit(1);
    }
    try {
      accessSync(p, constants.X_OK);
    } catch {
      console.error(
        `[postinstall] FAIL: hook not executable (dispatcher will skip it): ${p}`,
      );
      process.exit(1);
    }
  }
  console.log(`[postinstall] git hooks ready under ${hooksDir} (executable)`);
}

// .git 可能是目录（常规克隆）或文件（git worktree），两种都算 git 检出。
if (!existsSync(path.join(repoRoot, '.git'))) {
  console.log('[postinstall] skip: not a git checkout, git hooks not installed');
  process.exit(0);
}

const result = spawnSync(
  'pwsh',
  ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', path.join('scripts', 'install-git-hooks.ps1')],
  { cwd: repoRoot, stdio: 'inherit' },
);

if (result.error && result.error.code === 'ENOENT') {
  const msg =
    '[postinstall] pwsh not found on PATH, git hooks not installed ' +
    '(manual fallback: pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/install-git-hooks.ps1)';
  if (inCi) {
    console.error(`[postinstall] FAIL (CI): ${msg}`);
    process.exit(1);
  }
  console.log(`[postinstall] skip: ${msg}`);
  process.exit(0);
}

if (result.error) {
  console.error('[postinstall] failed to spawn pwsh:', result.error.message);
  process.exit(1);
}

if ((result.status ?? 1) !== 0) {
  process.exit(result.status ?? 1);
}

const hooksDir = resolveHooksDir();
if (!hooksDir) {
  console.error('[postinstall] FAIL: cannot resolve git-common-dir/hooks');
  process.exit(1);
}
assertHooksExecutable(hooksDir);
process.exit(0);
