# Plan — db-admin 增加 Oracle 类型

> 阶段：Stage 3｜Task ID：`2026-11-06-db-admin-oracle-provider`｜日期：2026-11-06

## Implementation Steps（具体到真实文件）

### S1 后端 provider 目录 + 连接串
- `Plugins/DbAdmin/Services/DriverCatalog.cs` — `Providers` 数组末位追加 oracle 项（reason: provider 唯一真源；`ListProviders`/`Find`/`Tested` 全部自动生效）。
- `Plugins/DbAdmin/Services/DbConnectionService.cs` — `BuildConnStr` switch 加 `case "oracle"`（EZConnect 形态；reason: form 模式连 Oracle 的服务端唯一构造点）。

### S2 插件前端枚举与预填
- `Plugins/DbAdmin/web/src/types.ts` — `ProviderId` 联合加 `'oracle'`（reason: 类型层显式收录新 id）。
- `Plugins/DbAdmin/web/src/connForm.ts` — `DEFAULT_PORTS.oracle=1521`、`PROVIDER_LABELS.oracle`、`buildConnStr` oracle 分支（reason: 端口联动/展示名/连接串模式预填）。

### S3 契约样例与 CT 同步
- `Plugins/DbAdmin/web/src/__fixtures__/backend-samples.json` — providers.body 追加 oracle 项（reason: 「fixture 即契约样例」）。
- `Plugins/DbAdmin/web/src/contract.test.ts` — provider 清单断言补 `'oracle'` + oracle 事实断言（reason: 该测试断言清单恰好相等，不改必红）。
- `Plugins/DbAdmin/web/src/connForm.test.ts` — 补 oracle 端口/展示名/连接串断言（reason: 新增映射的回归锁）。
- `ForgeSelf.Web/src/components/DbAdmin.story.ts` — providers fixture 追加 oracle 项（reason: CT 演示数据与真实后端对齐；不动其余 4 项演示值）。
- `ForgeSelf.Web/src/components/DbAdmin.ct.spec.ts` — `cf-provider` option 断言 4→5（reason: 新增选项后计数断言必红）。

### S4 清单与文档
- `Plugins/DbAdmin/plugin.json` — Version 0.2.1 + Description 补 Oracle。
- `Plugins/DbAdmin/docs/00-目录.md`、`01-产品需求说明.md`、`02-交互说明.md`、`03-架构与接口.md`（§3.1 provider id 清单）、`05-已知风险-多引擎与密码.md`、`README.md`、`docs/ui-design-brief.md`（如涉 provider 清单）— 补 Oracle（标未实测）。
- `docs/02-features/044-db-admin.md` — §1 概述与 §决策段补 Oracle。

### S5 验证与发布
- 门禁（见 Verification）→ dev-stack 走查 → `release-local.ps1 -Sign` 本地 zip → 验包（版本串/SHA256/db-admin 清单 0.2.1）。

## Test Plan

| 层 | 内容 |
| --- | --- |
| 后端单测（新增/修改） | `DbConnectionServiceTests` 新增：① `Oracle_ProviderRegistered_AndConnStrBuilt`（目录项断言 + providers 列表含 oracle + Create/BuildConnStr/Resolve + 测连失败路径）；既有用例回归（`ListProviders_AllAvailable_WhenDriverProbeDisabled` 等） |
| 插件前端单测 | `contract.test.ts` 清单断言、`connForm.test.ts` oracle 断言 |
| 宿主前端 | CT `DbAdmin.ct.spec.ts` S9 option=5；`pnpm run check/test` 回归；`design:check`（无新裸色值） |
| e2e 冒烟 | `e2e/plugins/db-admin`（下拉无「需驱动」断言天然覆盖 5 项；SQLite 主链路回归） |
| 守卫 | `verify-pilot-artifacts.ps1`；`check-problem-classes.ps1`（触及文档索引文件时） |

## Verification

```powershell
# ① 插件前端
cd Plugins/DbAdmin/web; pnpm run test; pnpm run build
# ② 后端
cd <repo>; dotnet build ForgeSelf.Api; dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DbAdmin"
# ③ 宿主前端（触及 story/ct.spec）
cd ForgeSelf.Web; pnpm run check; pnpm run test; pnpm run design:check; pnpm run test:ct
# ④ e2e
CI=1 E2E_CLEAN=1 npx playwright test e2e/plugins/db-admin
# ⑤ 走查
pwsh scripts/dev-stack.ps1  → 浏览器走查 + 截图 → pwsh scripts/dev-stack.ps1 -Stop
# ⑥ 发布（用户已授权）
pwsh scripts/release/release-local.ps1 -Sign
```

判定：各命令 exit 0 + 日志正文 PASS/通过计数（不拿 exit code 当唯一证据）；CT 若仅 DbAdmin 基线像素漂移，只对 DbAdmin 重生成基线并复跑确认。

## 闸门1 记录

用户指令即端到端授权（「增加oracle类型，改完打本地zip」），实现路径唯一且完全跟随既有 4-provider 模式，无开放设计分叉（驱动策略沿用 2026-11-06 决策、连接串形态采用 ODP.NET 标准 EZConnect、默认端口 1521 行业默认值）。按 AGENTS §8.5 自动迭代模式记录留档，不再中断等待二次确认；如用户对细节有异议，验收时回炉。
