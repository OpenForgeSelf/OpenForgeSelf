# 05 · Evidence — 2026-11-04-smart-terminal

> 证据分级：Verified（亲自跑过真实命令拿输出）/ Inferred（凭代码推断）/ Unknown（未验证）。
> 状态标注：骨架期先登记「待执行」，执行后逐条回填真值；禁止伪造。

## 门禁证据
- run-gates（-Base 2aa93aa）：**待执行**（骨架期由工件提交前状态：pilot-gate 在补齐本目录前为 FAIL 13 项，属修复目标本身）。
- style-tokens：**Verified** — `node scripts/check-style-tokens.mjs` 输出「PASS：无新增硬编码色值，较基线减少 741 处」（合计 2501→1760；SmartTerminal/web/src 15→0，15 处逐条替换为 --el-* token）。
- PC-005 lens 扫查：**Verified** — Select-String SmartTerminalPlugin.cs 无 raw CreateScope/急切解析（仅 RegisterTools 签名形参）。

## 构建 / 测试证据（回填区）
- dotnet build：**待执行**
- dotnet test --filter SmartTerminal：**待执行**
- Plugins/SmartTerminal/web pnpm build：**待执行**
- ForgeSelf.Web pnpm run check / test / vue-tsc -b：**待执行**
- 冒烟 e2e（e2e/plugins/smart-terminal）：**待执行**；截图目录 ForgeSelf.Web/screenshots/e2e/smart-terminal/。

## 验收标准逐条（AC 覆盖）
- AC1 多标签操作与会话隔离：**待执行**（e2e + 走查截图）
- AC2 AskWrite 审批批准/拒绝：**待执行**（e2e + 走查截图）
- AC3 审计 JSONL + 脱敏：**待执行**（走查 + JSONL 文件抽查）
- AC4 URL 通用启动 + S5 400 兜底：**待执行**（e2e + CT fixture bbe63d4）
- AC5 交互密码先框后标签：**待执行**（走查）
- AC6 URL 密码 ack + scrub + 审计无明文：**待执行**（走查 + DevTools 核查）
- AC7 刷新恢复 + 存储无密码：**待执行**（走查）
- AC8 门禁与测试全绿：**待执行**（本文件门禁证据小节）

## 用户动线成本
**待走查回填**（目标：主链路 ≤N 步、无不可达入口、无必要重启）。