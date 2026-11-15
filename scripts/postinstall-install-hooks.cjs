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
 *   - pwsh 不在 PATH（ENOENT）→ 打提示并附手动兜底命令，exit 0（不阻塞还原；pwsh 铁律见 AGENTS §2.3）。
 *   - install-git-hooks.ps1 真实失败（源文件缺失、复制失败等）→ 透传非零退出码，还原显式失败。
 */
'use strict';

const { spawnSync } = require('node:child_process');
const { existsSync } = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..');

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
  console.log(
    '[postinstall] skip: pwsh not found on PATH, git hooks not installed ' +
      '(manual fallback: pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/install-git-hooks.ps1)',
  );
  process.exit(0);
}

if (result.error) {
  console.error('[postinstall] failed to spawn pwsh:', result.error.message);
  process.exit(1);
}

process.exit(result.status ?? 1);
