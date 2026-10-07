#!/usr/bin/env node
// probe-esbuild-write —— 探测「esbuild 原生二进制能否写输出文件」（dev-stack.ps1 环境自适应用）。
//
// 背景：沙箱 safe-delete shim 会拦截 esbuild（Go 二进制）的写盘调用，报
//   「Failed to write to output file: open <path>: Access is denied」
// 与目录权限无关（实测 os.tmpdir() 与项目内目录同样失败），而同目录用 Node fs 写是成功的。
// 影响面：
//   - `vite build`：产物由 rollup（Node 侧）写出 → 不受影响，能正常构建；
//   - `vite dev`：optimizeDeps 调 esbuild 时是 `write: true`（Go 侧写盘）→ 必失败，dev server 起不来。
//
// 结论用途：探测通过 → 用默认 optimizeDeps（预打包，依赖解析更快）；
//           探测失败 → dev-stack.ps1 生成的配置里关掉预打包（noDiscovery），绕开 esbuild 写盘。
//
// 用法: node scripts/probe-esbuild-write.mjs [宿主前端目录]
// 输出: JSON { canWrite, esbuildPath, esbuildVersion, error }；退出码恒为 0（探测失败不是错误）。

import { createRequire } from 'node:module'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'

const webDir = path.resolve(process.argv[2] || path.join(process.cwd(), 'ForgeSelf.Web'))

/** 在 <webDir>/node_modules/.pnpm 下定位 esbuild（它是 vite 的间接依赖，直接 require 解析不到）。 */
function locateEsbuild(dir) {
  const pnpmDir = path.join(dir, 'node_modules', '.pnpm')
  if (!fs.existsSync(pnpmDir)) return null
  const entry = fs
    .readdirSync(pnpmDir)
    .filter((n) => n.startsWith('esbuild@'))
    .sort()
    .pop()
  if (!entry) return null
  return path.join(pnpmDir, entry, 'node_modules', 'esbuild', 'lib', 'main.js')
}

function probe() {
  const esbuildPath = locateEsbuild(webDir)
  if (!esbuildPath || !fs.existsSync(esbuildPath)) {
    return { canWrite: null, esbuildPath, esbuildVersion: null, error: 'esbuild not found under .pnpm' }
  }

  // 注意：require 失败时 esbuild 仍处于 TDZ，catch 里不得访问它 —— 先取出版本号再进探测
  const require = createRequire(import.meta.url)
  let esbuild = null
  let version = null
  try {
    esbuild = require(esbuildPath)
    version = esbuild.version ?? null
  } catch (e) {
    return { canWrite: null, esbuildPath, esbuildVersion: null, error: String(e.message).split('\n')[0].slice(0, 200) }
  }

  const tmpDir = path.join(os.tmpdir(), `esbuild-write-probe-${process.pid}`)
  const srcFile = path.join(tmpDir, 'in.js')
  const outFile = path.join(tmpDir, 'out.js')
  try {
    fs.mkdirSync(tmpDir, { recursive: true })
    fs.writeFileSync(srcFile, 'export const a = 1\n')
    // write:true → 由 Go 侧写盘，正是 vite optimizeDeps 走的那条路径
    esbuild.buildSync({ entryPoints: [srcFile], outfile: outFile, bundle: true, write: true, logLevel: 'silent' })
    return { canWrite: fs.existsSync(outFile), esbuildPath, esbuildVersion: version, error: null }
  } catch (e) {
    return {
      canWrite: false,
      esbuildPath,
      esbuildVersion: version,
      error: String(e.message).split('\n')[0].slice(0, 200),
    }
  } finally {
    fs.rmSync(tmpDir, { recursive: true, force: true })
  }
}

console.log(JSON.stringify(probe(), null, 2))
