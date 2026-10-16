# AI-Native Pilot Result（收尾汇报）

> 状态：已实测，禁止模糊表述。本任务为轻量通道（mini-task.md + 05/06/07）。

## 1. Repository Understanding

已确认：McpCenter 为带独立 web 界面的后端插件；dsh CRUD 服务已完成 Writer→Service 演进但装配注册行未跟上；软依赖 IToolRegistry 装配期不可解析（ListToolsToolFunction 注释言明）；401 报错语义经 ApiKeyAuthenticationHandler 源码定性。

## 2. Selected Task

修复「网关配置」页签点击报错：Kestrel 500（DI 缺注册）+ 运行期 IToolRegistry 永久降级刷 Warn；401 定性为非缺陷；修后打包发布本地包（用户明令）。

## 3. Changed Files

- `Plugins/McpCenter/McpCenterPlugin.cs`（:51 惰性解析、:71 DI 换 DshMcpConfigService）
- `Plugins/McpCenter/Services/McpService.cs`（Func provider + WithDeferredRegistry 工厂）
- `Plugins/McpCenter/McpCenter.csproj` / `plugin.json`（→ 2.3.2）
- `ForgeSelf.Web/e2e/mcp-tools.spec.ts`（存量失效修正：/mcp-tools → /mcp-center）
- `docs/02-features/034-mcp-center.md`（v2.3.2 回写 + 真源纠旧）

## 4. Validation

Build: `dotnet build Plugins/McpCenter/McpCenter.csproj` → 0 错误
Unit Test: `dotnet test ForgeSelf.Api.Tests --filter ~McpCenter` → 108/108
E2E: `pnpm exec playwright test e2e/mcp-tools.spec.ts` → 4/4 绿（零 mock、隔离实例）

## 5. Evidence

见 `05-evidence.md`：V1-V7 全部实跑通过（来源等级 Verified）——dsh 500→200、无 token 仍 401、日志「同步 116 个真实工具」、浏览器页签无红条（截图 `screenshots/mcp-gateway-fixed.png` / `mcp-tools-fixed.png`）；发布产物 `OpenForgeSelf-2.4.6.2610162203-win-x64.zip` 签名 3/3、L1 包内 McpCenter=2.3.2。

## 6. Review

见 `06-review.md` Final Decision：**放行（PASS）**——根因命中、四层验证完整、范围控制 Pass（含 e2e spec 计划外修改申报）。

## 7. Risk

L（低）——插件内部 DI 注册与服务构造改造，无公共契约/数据库变更；12 处 `new McpService(null)` 测试兼容未动。

## 8. Problems Found

- 走查发现 3 个插件前端缺陷（选中态对比度≈1.05、form-hint 竖排、v-loading 未注册静默失效）→ TODO P2；
- `DshMcpConfigWriter` 旧类已成孤儿 → TODO P2；
- 宿主 e2e `mcp-tools.spec.ts` 因页面迁插件长期失效无人发现 → 冒烟归属已随本次修正，插件间路由迁移应联动扫 e2e（复盘入 §9）。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS（Task 2 偏差：工厂替代公开重载，CS0121 实证纠偏） |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

## 10. 本次要沉淀的事

- 「装配期 ctx.Get 缓存软依赖」是插件系统性陷阱（ListToolsToolFunction 早言明，McpService 却缓存）——新插件软依赖一律运行期现取；
- 宿主页面迁插件时，指向旧路由的 e2e spec 必须同批迁移（本次 `mcp-tools.spec.ts` 曾长期红灯无人管）。

## 11. 下一步建议

用户在 :51888 设置页更新到 2.4.6 后，执行 §0 门禁⑤ 运行实例只读复验（本任务唯一未竟步骤）。
