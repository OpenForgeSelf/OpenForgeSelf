# 07 Final Report — 发布本地 zip 排障与插件 emptyOutDir 类级修复

## 1 Repository Understanding
宿主前端 Vue3+Vite6、后端 .NET10、插件 Vite lib 模式独立预编译。发布链 `release-local.ps1` 编排 build-frontend（宿主 web → 15 插件 web）→ publish → package-release（zip）。本机 WorkBuddy 沙箱注入 `node-safe-delete-shim`，单次 tool call 删除配额 49。

## 2 Selected Task
用户原始指令「看下发布本地 zip 的命令有什么问题，打个本地包，解决报错、失败」→ 方案 A（用户回复「a」批准）：15 插件 emptyOutDir false + G2 守卫 + PC/Case 归档。

## 3 Changed Files
（见 05-evidence.md §Changed Files；核心 15 配置 + 1 守卫 + 6 文档）

## 4 Validation
- 红灯（Verified）：`.temp/release-local-run2.log` — AgentHub `pnpm build` 被 `SAFE_DELETE_BULK_CONFIRM_REQUIRED` 拦截。
- 绿灯（Verified）：15 插件 emptyOutDir 全 false、G2 守卫 Passed、AgentHub 构建通过（修复生效）、check-problem-classes 37/37、Registry+守卫 6/6。

## 5 Evidence
详见 05-evidence.md。所有结论 Verified。

## 6 Review
APPROVED（详见 06-review.md）。

## 7 Risk
代码侧 L0；沙箱环境 L1（完整 zip 待拍板）。

## 8 Problems Found
1. 15 插件 `emptyOutDir:true` 横切遗漏（已修，PC-047）。
2. 沙箱单次 tool call 删除配额 49 → 15 插件链 pnpm install 临时文件清理再次触发 bulk guard（环境约束，非仓库 bug，TODO 待拍板）。
3. 既有 PC-034 双文件致枚举测试假红（存量，TODO）。
4. 用户外部 `-Sign` 进程死锁 19 分钟（他人资产，未触碰）。

## 9 Process Evaluation
工具默认 120s 超时导致首次复现无真实报错——已学会发布链必须足量 timeout + 重定向日志读正文（AGENTS §5.0 既有规则，本次实证复验）。沙箱 shim 属宿主环境，无法在仓库内根治；已通过 PC-047 lens 固化「区分 vite 清空 dist vs pnpm 临时文件」的判据。

## 10 问题类归档
PC-047（guarded）+ case `2026-11-24-release-local-zip-emptyoutdir`，三处索引更新，`check-problem-classes.ps1` 37/37 通过。存量补：case `2026-11-23-hover-reveal-width-jump` 缺索引行已补（台账守卫要求）。

## 11 重构提案
R0 不重构。15 行同构配置值调整 + G2 守卫已覆盖复发面，无需结构性重构。

## 12 悬空教训清单（必须为空）
空。所有遗留已落 TODO id：
- 沙箱配额 → TODO「safe-delete 配额发布链」。
- PC-034 假红 → TODO「PC-034 双文件假红」。

## 13 用户拍板项
完整 zip 产出的环境层解法，三选一：
| 方案 | 做法 | 前→后 | 影响/风险 |
|------|------|-------|-----------|
| A（推荐） | 发布链拆到多次工具调用执行 | 每次计数重置 → 配额不累计 | 需分段跑，操作稍繁，但不碰安全守卫 |
| B | 调用时临时调高 `CODEBUDDY_SAFE_DELETE_BULK_THRESHOLD` | 单次配额提高 → 一次跑完 | 绕过守卫需谨慎；仅本次子进程 |
| C | 换无 shim 环境（CI/裸终端）执行 | 无配额约束 → 直接出包 | 本机不满足诉求时不可行 |
