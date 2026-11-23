/**
 * 从已安装的 element-plus 中提取全部合法的 --el-* CSS 变量名，
 * 生成 scripts/element-tokens.json 供 check-theme-tokens.mjs 相对路径导入使用。
 *
 * 来源：自 NewLife.Cube/NewLife.Cube.Vue/web/scripts/gen-element-tokens.mjs 复制适配
 * （2026-11-20 输入11）：本仓为 pnpm workspace，element-plus 装在 ForgeSelf.Web/node_modules，
 * 故按候选起点列表搜索。升级 element-plus 后重跑 `pnpm run check:theme:gen` 同步。
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, '..');

// 候选起点：仓根与 ForgeSelf.Web（pnpm workspace 实际安装位置）
const startDirs = [repoRoot, path.join(repoRoot, 'ForgeSelf.Web')];

function findElementPlus(fromDir) {
  let current = path.resolve(fromDir);
  const root = path.parse(current).root;
  while (true) {
    const candidate = path.join(current, 'node_modules', 'element-plus');
    if (fs.existsSync(candidate)) return candidate;
    if (current === root) break;
    current = path.dirname(current);
  }
  return null;
}

let epDir = null;
for (const start of startDirs) {
  epDir = findElementPlus(start);
  if (epDir) break;
}

if (!epDir) {
  console.log(`[gen-element-tokens] 未找到 element-plus（已从 ${startDirs.join('、')} 向上搜索至根目录），跳过。`);
  process.exit(0);
}

console.log(`[gen-element-tokens] 使用 element-plus 路径: ${epDir}`);

const tokens = new Set();

function walk(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      walk(full);
      continue;
    }
    if (!entry.name.endsWith(".css")) continue;
    const text = fs.readFileSync(full, 'utf8');
    const re = /--el-[\w-]+/g;
    let m;
    while ((m = re.exec(text))) tokens.add(m[0]);
  }
}

// theme-chalk 内含各组件及 dark 主题，dist 内含打包后的全量 css
walk(path.join(epDir, 'theme-chalk'));
walk(path.join(epDir, 'dist'));

const outFile = path.join(scriptDir, 'element-tokens.json');
fs.writeFileSync(outFile, JSON.stringify([...tokens].sort(), null, 2) + '\n', 'utf8');
console.log(`[gen-element-tokens] 已生成 ${outFile}（共 ${tokens.size} 个合法 --el- token）`);
