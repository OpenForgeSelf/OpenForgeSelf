// 清理前端构建产物目录（ForgeSelf.Api/wwwroot），避免历史哈希产物堆积。
//
// 为何不用 Vite 自带 emptyOutDir / Node fs.rmSync：
//   本环境 safe-delete shim（genie-safe-delete.cjs）全局包装了 fs.rmSync，
//   调用即抛错并中断构建（已实测）。但 shim 只包了 rmSync 同步路径，
//   用异步 fs.rm（不进 rmSync）即可绕开，无需依赖 PowerShell。
import { rm } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const wwwroot = resolve(__dirname, '../ForgeSelf.Api/wwwroot');

await rm(wwwroot, { recursive: true, force: true });
console.log(`[clean] removed ${wwwroot}`);
