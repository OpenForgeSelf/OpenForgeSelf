/**
 * PC-045 类级守卫（G2）：插件前端 SFC 用 Element Plus 组件必须显式 import。
 *
 * 背景：插件 web 是独立预编译产物（vite lib 构建，无 unplugin 自动导入链），
 * SFC 模板里的 <el-*> 组件若未显式 `import { ElXxx } from 'element-plus'`，
 * 宿主侧构建绿、运行时才暴露组件解析失败（docs/11-troubleshooting/problem-classes/PC-045）。
 * 本守卫静态扫查全部插件 SFC：出现 <el-dialog 用法 → 同文件源码必须含 ElDialog 具名导入。
 * 范围先锁 el-dialog（弹窗迁移 FR5 的主组件）；其余 el-* 组件按迁移批增量纳入。
 */
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, sep } from 'node:path';
import { describe, expect, it } from 'vitest';

const repoRoot = join(__dirname, '..', '..', '..');
const pluginsWebSrc = join(repoRoot, 'Plugins');

function collectVueFiles(dir: string, acc: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    const st = statSync(full);
    if (st.isDirectory()) {
      if (name === 'node_modules' || name === 'dist') continue;
      collectVueFiles(full, acc);
    } else if (name.endsWith('.vue')) {
      acc.push(full);
    }
  }
  return acc;
}

describe('PC-045 插件 SFC 的 Element Plus 组件显式导入守卫', () => {
  it('用到 <el-dialog 的插件 SFC 必须显式 import { ElDialog }', () => {
    const offenders: string[] = [];
    for (const file of collectVueFiles(pluginsWebSrc)) {
      const src = readFileSync(file, 'utf8');
      if (!/<el-dialog[\s>/]/.test(src)) continue;
      if (!/import\s*\{[^}]*\bElDialog\b[^}]*\}\s*from\s*['"]element-plus['"]/.test(src)) {
        offenders.push(file.split(sep).join('/'));
      }
    }
    expect(
      offenders,
      `以下插件 SFC 用了 <el-dialog 但缺 ElDialog 显式导入（插件产物无 unplugin，运行时才会炸）：\n${offenders.join('\n')}`,
    ).toEqual([]);
  });
});
