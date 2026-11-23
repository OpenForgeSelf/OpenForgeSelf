/**
 * token 迁移器（token-migration-map.json 驱动）
 *
 * 用法：
 *   node scripts/migrate-theme-tokens.mjs --dry-run            # 全项目预览，不写盘
 *   node scripts/migrate-theme-tokens.mjs --apply              # 全项目落盘
 *   node scripts/migrate-theme-tokens.mjs --dry-run --scope Plugins/DesignSystem/web/src
 *
 * 规则（docs/ai/pilot/2026-11-22-token-migrate/02-spec）：
 *   - rename/phantomEl：引用级替换 var(--old… → var(--new…（保留 fallback 串）；
 *     literal 条目把整个 var(--old[,fallback]) 替换为字面量（仅非颜色语义，如字重）。
 *   - deleteDef：引用迁移后，非主题文件中该 token 的定义行删除（--x: ... 整行）。
 *   - themes/*.css（值所有者）永不出现在目标集；测试文件与 dist/node_modules 跳过。
 *   - 报告未映射的自造 token（不在 allow 名单的 var(--x 引用），供波次认领）。
 * 判据看输出正文（per-file 替换计数 + unmapped 清单），不拿 exit code 当证据。
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const apply = args.includes('--apply');
const mapArg = args.includes('--map') ? args[args.indexOf('--map') + 1] : path.join(scriptDir, 'token-migration-map.json');
const scopes = [];
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--scope') scopes.push(path.resolve(args[++i]));
}
// --rules <phantom|rename|all>：默认 all。W1 只落幻影修正时用 --rules phantom。
const rulesIdx = args.indexOf('--rules');
const rulesSel = rulesIdx >= 0 ? args[rulesIdx + 1] : 'all';
// --prune-defs：删除「定义在非主题文件、且 var() 引用已全仓清零」的孤儿定义行
const pruneDefs = args.includes('--prune-defs');
const map = JSON.parse(fs.readFileSync(mapArg, 'utf8'));

const EXT = new Set(['.vue', '.ts', '.tsx', '.css', '.scss']);
const SKIP_DIR = new Set(['node_modules', 'dist', '.temp', 'screenshots', 'coverage', 'playwright-report', 'test-results']);
// 主题文件=值所有者，禁改：按相对路径匹配（2026-11-22 教训：只查 basename 时 themes/ 目录形同虚设）
const SKIP_FILE = /(^|[\\/])themes([\\/])/;
const SKIP_TESTS = /(\.test\.tsx?$|\.spec\.tsx?$|\.d\.tsx?$)/;

// 合并 phantomEl 与 rename 为同一张名字映射表（--rules phantom|rename|all 过滤来源）
const nameMap = new Map();
if (rulesSel === 'phantom' || rulesSel === 'all') for (const [k, v] of Object.entries(map.phantomEl || {})) nameMap.set(k, v);
if (rulesSel === 'rename' || rulesSel === 'all') {
  for (const [k, v] of Object.entries(map.rename || {})) {
    if (nameMap.has(k)) { console.error(`[migrate] 映射冲突: ${k}`); process.exit(1); }
    nameMap.set(k, v);
  }
}
const allowPrefixes = Object.keys(map.allowPrefixes || {});
const allowTokens = new Set(Object.keys(map.allowTokens || {}));
const deleteDef = new Set(map.deleteDef || []);
const pending = [...nameMap.entries()].filter(([, v]) => String(v.to).startsWith('__'));

function* walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    if (e.isDirectory()) { if (!SKIP_DIR.has(e.name)) yield* walk(path.join(dir, e.name)); continue; }
    const p = path.join(dir, e.name);
    if (!EXT.has(path.extname(e.name))) continue;
    if (SKIP_TESTS.test(e.name)) continue;
    if (SKIP_FILE.test(p)) continue; // 按完整路径判（含 themes/ 目录段）
    yield p;
  }
}

function targets() {
  const roots = scopes.length ? scopes : [path.resolve(scriptDir, '../ForgeSelf.Web/src'), ...fs.readdirSync(path.resolve(scriptDir, '../Plugins')).filter((d) => !d.startsWith('.')).map((d) => path.resolve(scriptDir, '../Plugins', d, 'web', 'src')).filter((p) => fs.existsSync(p))];
  const out = [];
  for (const r of roots) {
    if (!fs.existsSync(r)) continue;
    const st = fs.statSync(r);
    if (st.isFile()) out.push(r); else out.push(...walk(r));
  }
  return out;
}

// 构建替换规则：按 token 名长度降序（防 --fg-primary 吃掉 --fg-primary-2 之类前缀碰撞——用负向断言已保证，长度序为双保险）
const entries = [...nameMap.entries()].sort((a, b) => b[0].length - a[0].length);
const rules = entries.map(([from, spec]) => {
  const name = from.replace(/^--/, '');
  const esc = name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  if (spec.literal) {
    return { from, re: new RegExp(`var\\(\\s*--${esc}(?:\\s*,[^)]*)?\\)`, 'g'), to: String(spec.to), literal: true };
  }
  return { from, re: new RegExp(`var\\(\\s*--${esc}(?![\\w-])`, 'g'), to: `var(--${String(spec.to).replace(/^--/, '')}` };
});
const defRules = [...deleteDef].map((t) => {
  const esc = t.replace(/^--/, '').replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  return { from: t, re: new RegExp(`^[ \\t]*--${esc}\\s*:[^;]*;?[ \\t]*\\r?\\n`, 'gm') };
});

let totalRepl = 0, totalDefDel = 0, changedFiles = 0;
const unmapped = new Map();
const perFile = [];
const refCounts = new Map(); // 剪定义用：全仓 var(--x) 引用计数（基于改名后文本）

// 阶段 A：内存改写 + 引用统计（不写盘）
const staged = [];
const refScanFiles = new Set();
if (pruneDefs) {
  // 剪定义的引用计数必须全仓统计：scope 外的消费点（如插件预览面对外壳 token 的
  // 引用）不算数的话，会把仍被引用的定义当孤儿误删（2026-11-23 W5 实证）。
  const allRoots = [path.resolve(scriptDir, '../ForgeSelf.Web/src'), ...fs.readdirSync(path.resolve(scriptDir, '../Plugins')).filter((d) => !d.startsWith('.')).map((d) => path.resolve(scriptDir, '../Plugins', d, 'web', 'src')).filter((p) => fs.existsSync(p))];
  for (const r of allRoots) {
    if (!fs.existsSync(r)) continue;
    if (fs.statSync(r).isFile()) refScanFiles.add(r);
    else for (const f of walk(r)) refScanFiles.add(f);
  }
}
for (const f of targets()) refScanFiles.add(f);
const targetSet = new Set(targets());
for (const file of refScanFiles) {
  const src = fs.readFileSync(file, 'utf8');
  // scope 内文件按改名后文本计数（将被重写）；scope 外按原文计数（保持旧名）。
  let text = src;
  if (targetSet.has(file)) for (const r of rules) text = text.replace(r.re, (m) => r.to);
  for (const m of text.matchAll(/var\(\s*(--[a-zA-Z][\w-]*)/g)) {
    const t = m[1];
    if (t.startsWith('--el-')) continue;
    refCounts.set(t, (refCounts.get(t) || 0) + 1);
  }
}
for (const file of targets()) {
  const src = fs.readFileSync(file, 'utf8');
  let text = src, repl = 0;
  const hits = {};
  for (const r of rules) {
    text = text.replace(r.re, (m) => { hits[r.from] = (hits[r.from] || 0) + 1; repl++; return r.to; });
  }
  totalRepl += repl;
  for (const m of text.matchAll(/var\(\s*(--[a-zA-Z][\w-]*)/g)) {
    const t = m[1];
    if (t.startsWith('--el-')) continue;
    refCounts.set(t, (refCounts.get(t) || 0) + 1);
  }
  staged.push({ file, src, text, repl, hits });
}

// 阶段 B：剪孤儿定义（--prune-defs：仅限 rename 表内 token 且引用已清零）
for (const s of staged) {
  let defDel = 0;
  const isBridge = /tailwind\.css$/.test(String(s.file)); // Tailwind @theme 桥接：结构必需，永不剪
  if (pruneDefs && !isBridge) {
    const prune = [...nameMap.keys()].filter((t) => !refCounts.has(t));
    for (const t of prune) {
      const name = t.replace(/^--/, '').replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      const re = new RegExp(`^[ \\t]*(?:\\/\\*[^*]*\\*\\/\\s*)?--${name}\\s*:[^;]*;[ \\t]*(?:\\/\\*[^*]*\\*\\/[ \\t]*)?\\r?\\n`, 'gm');
      s.text = s.text.replace(re, () => { defDel++; return ''; });
    }
  }
  totalDefDel += defDel;
  if ((s.repl || defDel) && apply) fs.writeFileSync(s.file, s.text);
  if (s.repl || defDel) {
    changedFiles++;
    perFile.push({ file: path.relative(process.cwd(), s.file), repl: s.repl, defDel, top: Object.entries(s.hits).sort((a, b) => b[1] - a[1]).slice(0, 6) });
  }
  // unmapped 盘点（基于最终文本）
  const seen = s.text.matchAll(/var\(\s*(--[a-zA-Z][\w-]*)/g);
  for (const m of seen) {
    const t = m[1];
    if (t.startsWith('--el-') || allowTokens.has(t) || allowPrefixes.some((p) => t.startsWith(p))) continue;
    if (nameMap.has(t)) continue;
    if (!unmapped.has(t)) unmapped.set(t, { n: 0, files: new Set() });
    unmapped.get(t).n++; unmapped.get(t).files.add(path.relative(process.cwd(), s.file));
  }
}

const mode = apply ? 'APPLY' : 'DRY-RUN';
console.log(`[migrate ${mode}] files changed: ${changedFiles}, replacements: ${totalRepl}, definitions deleted: ${totalDefDel}`);
for (const f of perFile.sort((a, b) => b.repl - a.repl).slice(0, 40)) {
  console.log(`  ${f.file}  repl=${f.repl} defDel=${f.defDel}  top: ${f.top.map(([t, n]) => `${t}×${n}`).join(', ')}`);
}
if (pending.length) console.log(`[migrate] 待映射（波次认领）: ${pending.map(([t]) => t).join(' ')}`);
const um = [...unmapped.entries()].sort((a, b) => b[1].n - a[1].n);
console.log(`[migrate] 未映射自造 token（豁免册外）: ${um.reduce((s, [, v]) => s + v.n, 0)} 处 / ${um.length} 种`);
for (const [t, v] of um.slice(0, 30)) console.log(`  ${t}  ${v.n}  ${[...v.files].slice(0, 3).join(', ')}${v.files.size > 3 ? ` …+${v.files.size - 3}` : ''}`);
