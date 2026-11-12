# mini-task：worktree 门禁基线同步 + globalSetup init-token 重试护栏（task-14）

- **Intent**：db-dev 夜班 9 色别名违规在 wt-db 漏拦（worktree 基线落后 main，合 main 才拦）——拦截口径随 worktree 漂移；lead 补报 fresh worktree 首启 e2e 假红（宿主卡「初始化内置 Agent」→ init-token 空 → 回退 config 不存在 → 全量用例报「ForgeSetting.config 中未找到 ApiToken」，重跑即恢复）。
- **Spec**：① pre-commit 设计门禁段前插入棘轮基线同步：分支自 merge-base 起未改基线且本次暂存未改基线 → 临时取 main 侧基线（design-compliance-baseline.json + style-token-baseline.json）校验，校验后 `git checkout --` 恢复工作区（只动工作区不进暂存区，提交内容不变）；分支/暂存改过基线（合法棘轮抬升）→ 尊重分支版本；无 main 引用 → 跳过保持旧行为。② globalSetup init-token：首抓空 → 等 8s 重试一次；两抓皆空且回退 ForgeSetting.config 不存在 → fail-fast 抛明确错误（指认场景+处置），config 存在时正常回退解密路径不变。
- **Plan**：定位基线读取（check-*.mjs 硬编码 ROOT/scripts 路径，无环境覆盖）→ hook 注入同步段 → worktree 真实验收（人为漂移基线 9999 + 注入 745 合法 hex 色值 2506 > main 基线 2501 → 期望拒绝）→ globalSetup 护栏 → 双档门禁。
- **Task**：Allowed=scripts/hooks/pre-commit, ForgeSelf.Web/e2e/global-setup.ts; Forbidden=check-style-tokens.mjs / check-design-compliance.mjs（无环境覆盖需求，不改）, install-git-hooks.ps1（无需改）, 其他 hook。
- **risk**: low —— 两处均为门禁/基建护栏，fail-open 方向保守（main 不可用→旧行为）
- **expected_files**: scripts/hooks/pre-commit; ForgeSelf.Web/e2e/global-setup.ts
- **feature**: 100-INFRA-1（门禁基线一致性）
- **rollback**: git revert 单提交；同步段独立于 pilot/台账门禁段，可部分回滚

## 无界面交互

本任务不新增 UI；pre-commit 同步段只影响命令行提交拦截文案（「棘轮基线按 main 同步校验」）；globalSetup 护栏只影响 e2e 启动日志（重试警告 + fail-fast 错误指认 backend.log 场景与处置）。
