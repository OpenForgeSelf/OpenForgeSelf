#!/usr/bin/env node
/**
 * probe-dll-string —— 二进制文件字符串探针（.NET 元数据字符串是 UTF-16LE，
 * strings/grep 只扫 ASCII 单字节序列会漏检，曾致误判「发布产物是旧的」）。
 *
 * 用法: node scripts/probe-dll-string.cjs <文件路径> <目标字符串> [--expect-absent]
 *   - 默认: FOUND → exit 0；ABSENT → exit 1。
 *   - --expect-absent: ABSENT → exit 0（验证「某实现已从二进制移除」，如 watcher 类名 absent）。
 * 输出: FOUND: <目标字符串> (<文件>) / ABSENT: <目标字符串> (<文件>)
 * 用途（发布验证标准步骤的一部分，替代每次现写一次性 Node 探针）：
 *   - 验证新代码真的进了 publish 产物（UTF-16LE 命中）；
 *   - 验证被删除的实现真的不在产物里（--expect-absent）。
 */
const fs = require('node:fs')

const raw = process.argv.slice(2)
const expectAbsent = raw.includes('--expect-absent')
const positional = raw.filter((a) => a !== '--expect-absent')
const [file, target] = positional
if (!file || !target) {
  console.error('用法: node scripts/probe-dll-string.cjs <文件> <目标字符串> [--expect-absent]')
  process.exit(2)
}

const buf = fs.readFileSync(file)
const hit =
  buf.includes(Buffer.from(target, 'utf8')) || buf.includes(Buffer.from(target, 'utf16le'))

if (hit) {
  console.log(`FOUND: ${target} (${file})`)
  process.exit(expectAbsent ? 1 : 0)
}
console.log(`ABSENT: ${target} (${file})`)
process.exit(expectAbsent ? 0 : 1)
