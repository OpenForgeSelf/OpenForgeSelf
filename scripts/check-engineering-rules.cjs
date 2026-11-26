#!/usr/bin/env node
/**
 * 工程规则守卫（lint/CI 用）：把高频 prose 规则下沉为可验证约束。
 * 规则来源：AGENTS.md 红线 / §2.3 / docs/04-standards/agent-workflow。
 * 输出：每条违规一行 `ruleId file:line: message`，退出码 0=通过 1=有违规。
 */
const fs = require('fs');
const path = require('path');

// 支持 --root <dir> 便于测试注入临时仓库根；缺省用脚本上级目录。
const args = process.argv.slice(2);
let repoRoot = path.resolve(__dirname, '..');
const rootIdx = args.indexOf('--root');
if (rootIdx >= 0 && args[rootIdx + 1]) {
  repoRoot = path.resolve(args[rootIdx + 1]);
}
const violations = [];

// ---------- 规则 1：禁止一次性 temp/*.cjs 验证脚本 ----------
function checkTempProbes() {
  const tempDir = path.join(repoRoot, 'temp');
  if (!fs.existsSync(tempDir)) return;
  const stack = [tempDir];
  while (stack.length) {
    const dir = stack.pop();
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) stack.push(full);
      else if (entry.isFile() && entry.name.endsWith('.cjs')) {
        violations.push({
          rule: 'eng.no-temp-probe',
          file: path.relative(repoRoot, full),
          line: 0,
          message: '禁止 temp/*.cjs 一次性验证脚本；用 §5.0 测试体系',
        });
      }
    }
  }
}

// ---------- 规则 2：脚本一律 pwsh（禁 powershell 5.1） ----------
const PWShell_ALLOWED = new Set([
  'scripts/hooks/pre-commit',
  'scripts/hooks/commit-msg',
  'scripts/check-engineering-rules.cjs', // 本守卫自身
]);

function stripComments(content, ext) {
  // 简化注释剥离：单行注释 + 块注释。足够覆盖本仓脚本形态。
  if (ext === '.ps1' || ext === '.yml' || ext === '.yaml') {
    return content.replace(/#.*$/gm, '');
  }
  if (ext === '.cjs' || ext === '.js' || ext === '.mjs') {
    return content
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/\/\/.*$/gm, '');
  }
  return content; // package.json 等无注释
}

function checkPwshOnly() {
  const targets = [];
  function walk(dir, filter) {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        if (entry.name === 'node_modules' || entry.name === '.git') continue;
        walk(full, filter);
      } else if (entry.isFile() && filter(full)) targets.push(full);
    }
  }
  walk(path.join(repoRoot, 'scripts'), (f) => /\.(ps1|cjs|mjs|js)$/.test(f));
  walk(path.join(repoRoot, '.github', 'workflows'), (f) => /\.ya?ml$/.test(f));
  const pkg = path.join(repoRoot, 'package.json');
  if (fs.existsSync(pkg)) targets.push(pkg);

  const forbidden = 'power' + 'shell'; // 避免自匹配
  for (const file of targets) {
    const rel = path.relative(repoRoot, file).replace(/\\/g, '/');
    if (PWShell_ALLOWED.has(rel)) continue;
    const ext = path.extname(file).toLowerCase();
    const content = fs.readFileSync(file, 'utf8');
    const stripped = stripComments(content, ext);
    const lines = stripped.split('\n');
    // 只拦 5.1 调用形态：powershell.exe 或 powershell 后跟参数（- / /）。
    // “PowerShell 7”这类提及不拦。
    const invokeRe = new RegExp(`\\b${forbidden}(?:\\.exe)?\\s+[-/]`, 'i');
    for (let i = 0; i < lines.length; i++) {
      if (invokeRe.test(lines[i])) {
        violations.push({
          rule: 'eng.pwsh-only',
          file: rel,
          line: i + 1,
          message: '脚本须用 pwsh，禁 powershell 5.1 调用（白名单见 PWShell_ALLOWED）',
        });
      }
    }
  }
}

// ---------- 规则 3：.ps1 必须带 UTF-8 BOM ----------
function checkPs1Bom() {
  const stack = [path.join(repoRoot, 'scripts')];
  while (stack.length) {
    const dir = stack.pop();
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) stack.push(full);
      else if (entry.isFile() && entry.name.endsWith('.ps1')) {
        const fd = fs.openSync(full, 'r');
        const buf = Buffer.alloc(3);
        fs.readSync(fd, buf, 0, 3, 0);
        fs.closeSync(fd);
        if (!(buf[0] === 0xef && buf[1] === 0xbb && buf[2] === 0xbf)) {
          violations.push({
            rule: 'eng.ps1-bom',
            file: path.relative(repoRoot, full).replace(/\\/g, '/'),
            line: 1,
            message: '.ps1 须带 UTF-8 BOM（PowerShell 5.1 兼容）',
          });
        }
      }
    }
  }
}

checkTempProbes();
checkPwshOnly();
checkPs1Bom();

if (violations.length === 0) {
  console.log('check-engineering-rules: PASS');
  process.exit(0);
}
for (const v of violations) {
  console.log(`[eng] FAIL ${v.rule} ${v.file}:${v.line}: ${v.message}`);
}
console.log(`check-engineering-rules: FAIL (${violations.length} violations)`);
process.exit(1);
