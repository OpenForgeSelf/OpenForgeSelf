# 05 Evidence — AIAgent Web 五子批合并落地（2026-10-16）

> 来源等级：Verified = 本会话（2026-10-16 接续验收会话）亲自在集成工作树上实跑；历史 Verified = 各子批 pilot 05 留档。

## 1. 本会话实跑门禁（集成工作树，来源等级：Verified）

| 门禁 | 命令 | 结果 | EXIT |
| --- | --- | --- | --- |
| 功能清单校验 | `node ForgeSelf.Web/scripts/check-features.mjs` | ✅ 22 项全一致，无假红 | 0 |
| 宿主静态检查 | `cd ForgeSelf.Web && pnpm run check`（vue-tsc + eslint） | ✅ **0 errors / 148 warnings**（warnings 全为既有 `vue/max-attributes-per-line` 等格式项） | 0 |
| 宿主全量单测 | `cd ForgeSelf.Web && pnpm run test`（vitest） | ✅ **85 文件 / 985 测试全通过**（32.78s；含本批 `agent-core.test.ts`/`trigger.test.ts`/`composerContext.test.ts`/`MessageItem.test.ts`/`*.states.test.ts`/`SessionPanel.test.ts` 等） | 0 |
| 设计门禁 | `pnpm run design:check` | ✅ style-tokens 1763（基线 2501，减 738）；design-compliance a=209（=基线）b/c/d=0 | 0 |
| 插件前端构建 | `cd Plugins/AIAgent/web && pnpm run build` | ✅ 97 modules；`dist/index.js` 261.57 kB（gzip **71.69 kB**）/ `dist/style.css` 173.16 kB | 0 |

> 前序会话报 pnpm 在 `Plugins/AIAgent/web` realpath 阶段 EPERM、宿主 vitest 无输出中断——**本会话复跑全部正常通过，该环境阻塞已不复现**（记录：非代码缺陷，属临时沙箱权限）。

## 2. 本批 pilot 工件门禁复跑（来源等级：Verified）

```text
pwsh -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-14-aiagent-widget-migration -EnforceHeader -EnforceAcCoverage  => EXIT=0
pwsh -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-15-aiagent-composer-upgrade -EnforceHeader -EnforceAcCoverage => EXIT=0
pwsh -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-15-chat-display-polish      -EnforceHeader -EnforceAcCoverage => EXIT=0
```

（三个子批目录补齐关键节后由 FAIL 转 PASS；本目录 + `2026-10-16-check-features-plugins-root` 新建即绿。）

## 3. 历史 e2e / 现场证据（来源等级：历史 Verified，本批未复跑）

- 冒烟 e2e：`ai-agent.spec.ts` 聊天主链路 `1 passed`（10-14/10-15 子批各跑，见对应 05）；本批未复跑（需起宿主实例，属发布/维护闭环③④，发布已暂缓）。
- dev-stack :7399 现场走查：S1 三栏/hydrate/错误态、S2 状态条/刻度/折叠、S3 `/`+`@`+chips、S4 上下文图标行/权限胶囊/授权询问、S5 五失败态——各自 05 均有截图 + DOM/计算样式实测（历史 Verified）。
- 本批 AC4 明确：**不做 e2e/现场刷新的宣称**，仅提交经前端四门禁验证的集成代码态。

## 4. AC 覆盖对照

| AC | 结论 | 证据 | 来源等级 |
| --- | --- | --- | --- |
| AC1 前端四门禁全绿 | ✅ | §1 全表 EXIT=0 | Verified |
| AC2 pilot 工件门禁全绿 | ✅ | §2 三目录 EXIT=0 | Verified |
| AC3 无后端/网络/aimodel 文件 | ✅ | §5 暂存清单对表 | Verified |
| AC4 局限如实登记 | ✅ | §3 + mini-task 表格 S4/S5 标注 | Verified |
| AC5 TODO 状态更新 | ✅ | 提交后回写完成：迁移主条目勾账 + 后端批次挂起项入 TODO.md（C4 批） | Verified |

## 5. 精确暂存清单（AC3 对表，来源等级：Verified，git ls-files/status 实读）

入库文件（仅 AIAgent web + e2e + 设计守卫 + 工件）：
- `Plugins/AIAgent/plugin.json`、`Plugins/AIAgent/web/package.json`、`Plugins/AIAgent/web/src/**`（含新增 agent-core/、agentBridge.ts、composer*.ts + 各 test）
- `ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts`、`session-management.spec.ts`
- `scripts/check-design-compliance.mjs`、`scripts/design-compliance-baseline.json`
- `docs/ai/pilot/{2026-10-14-aiagent-widget-migration,2026-10-15-aiagent-composer-upgrade,2026-10-15-chat-display-polish,2026-10-16-aiagent-web-batches-landing}/**`
- `docs/02-features/041-ai-agent-composer-context-bar.md`

**排除**（保留工作树未提交）：`ForgeSelf.Api/**`（含 `ModelProxy.cs`、AIModel 实体/服务/控制器、PersistentSessionStore、ForgeSetting、SettingsController、AIProviderRegistry）、`ForgeSelf.Web/src/components/settings/NetworkPanel.vue` 及网络/aimodel 前端服务与类型、`docs/02-features/042-*.md`、`docs/ai/pilot/2026-10-16-{settings-network-proxy,persistent-session-store-init-race}/**`、`docs/README.md`、`docs/04-standards/**`、`ForgeSelf.Web/components.d.ts`、`ForgeSelf.Web/src/**`（settings/aimodel）、`ForgeSelf.Api.Tests/**`、`.agents/**`。
