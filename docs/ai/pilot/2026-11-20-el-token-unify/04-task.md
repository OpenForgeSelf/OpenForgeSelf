---
task_id: 2026-11-20-el-token-unify
feature: none                   # 审计批：产出矩阵/门禁套件/盘点，无对应功能文档
risk: { level: L2, triggers: ["package.json", "scripts/"], raised_by_agent: true }
gate1: { mode: user, at: 2026-11-20T20:30, ref: "输入11 批准审计批" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - scripts/check-theme-tokens.mjs
  - scripts/gen-element-tokens.mjs
  - scripts/element-tokens.json
  - scripts/theme-check.mjs
  - scripts/fixtures/theme-tokens-anti-patterns.vue
  - package.json
rollback: "git revert <commit>"
writeback: { feature_doc: none, reason: "审计批无功能页面；R1-R6 规则回流 DESIGN.md 与 agent-workflow" }
---
# 04 Task — el-token-unify 审计批（本批）

> feature: 无对应 02-features 编号（横切样式规范治理，非单一功能）；闸门1 = 输入11 用户直接指令。
> 本批 Allowed 范围严格限定为「门禁脚本引入 + 检查 + 工件」，**零业务代码改动**。

## Allowed
1. `scripts/` 新增 6 件门禁套件（check-theme-tokens.mjs / gen-element-tokens.mjs / theme-check.mjs / custom-tokens-allow.json / element-tokens.json / fixtures/theme-tokens-anti-patterns.vue）+ 文档 theme-tokens-check.md——自 NewLife.Cube.Vue 复制适配，**检查器逻辑与上游一致**。
2. 根 `package.json` 增加 `check:theme` / `check:theme:gen` / `check:theme:demo` 三个独立别名（**不**改动现有 `check`/`design:check` 链）。
3. `docs/ai/pilot/2026-11-20-el-token-unify/` 工件 00-05 产出。
4. 只读检查：`node scripts/check-theme-tokens.mjs` 全量跑 + 静态盘点（自造弹窗/定义点）。

## Forbidden
- 修改任何 `.vue`/`.css`/`.ts` 业务文件（含 themes/）——修复属后续批次。
- 把 `check:theme` 接入 `check` 链或 pre-commit（存量 6007 处未清，接入即全红）。
- 删除或改写既有门禁（check-style-tokens / check-design-compliance）。
- git commit / push（闸门3 未授权）。

## 验证命令（本批）
```bash
node scripts/gen-element-tokens.mjs        # 生成 568 token 清单
node scripts/check-theme-tokens.mjs scripts/fixtures/theme-tokens-anti-patterns.vue   # 灵敏度：预期红（32 处全捕获）
node scripts/check-theme-tokens.mjs        # 全项目审计：预期红（6007 处）→ 数字即工件 05 输入
```
判据看日志正文（发现 N 处问题 / Theme token check: OK），不拿 exit code 当证据（本套件 1=有违反属预期）。
