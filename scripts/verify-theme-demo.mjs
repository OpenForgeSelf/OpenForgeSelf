/**
 * check:theme:demo —— 反模式 fixture 回归验证（AC2，2026-11-23 W7）。
 *
 * 对 scripts/theme-tokens-antipattern-demo/ 单独跑 check-theme-tokens.mjs，
 * 断言违规**恰为 32 处**且三类构成精确匹配：
 *   - 未知的 Element token = 4
 *   - 未授权的自定义 token = 16
 *   - 硬编码颜色 = 12
 * 总数不是 32（多=合法对照被误伤/少=样本漏检）或构成错位即红灯。
 * 用法：node scripts/verify-theme-demo.mjs（pnpm run check:theme:demo）
 */
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const demoDir = path.join(scriptDir, 'theme-tokens-antipattern-demo');

const res = spawnSync(process.execPath, [path.join(scriptDir, 'check-theme-tokens.mjs'), demoDir], {
  encoding: 'utf8',
});
const output = `${res.stdout ?? ''}${res.stderr ?? ''}`;

const failureLines = output.split(/\r?\n/).filter((l) => /:\d+: /.test(l));
const byType = (kw) => failureLines.filter((l) => l.includes(kw)).length;

const total = failureLines.length;
const unknownEl = byType('未知的 Element token');
const custom = byType('未授权的自定义 token');
const hardcoded = byType('硬编码颜色');

const problems = [];
if (res.status === 0) problems.push('检查器对 demo 未报红——fixture 样本失效应修 fixture');
if (total !== 32) problems.push(`总违规 ${total} ≠ 32`);
if (unknownEl !== 4) problems.push(`未知 Element token ${unknownEl} ≠ 4`);
if (custom !== 16) problems.push(`未授权自定义 token ${custom} ≠ 16`);
if (hardcoded !== 12) problems.push(`硬编码颜色 ${hardcoded} ≠ 12`);

if (problems.length) {
  console.error('[check:theme:demo] 未通过：');
  for (const p of problems) console.error(`  - ${p}`);
  console.error(failureLines.join('\n'));
  process.exit(1);
}

console.log(
  `[check:theme:demo] PASS：32 处全捕获（未知 el 4 + 自定义 16 + 硬编码 12），合法对照零误伤`,
);
