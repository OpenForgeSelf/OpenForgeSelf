# 03 复现优先测试（P1 / P4）

## 复现分层（选最低成本层，能低不高）

| 层 | 何时用 | 本仓入口 |
|---|---|---|
| 后端单测/集成 | 逻辑/DI/注册/数据类缺陷 | `dotnet test --filter FullyQualifiedName~<名>`（ForgeSelf.Api.Tests / Core.Tests） |
| 前端单测 | 组件/纯函数 | `cd ForgeSelf.Web && pnpm run test`（vitest） |
| e2e（真实前后端） | 页面表现/链路/鉴权态 | `ForgeSelf.Web/e2e/**`，起栈一律 `pwsh scripts/dev-stack.ps1`（PC-007），地址走 `e2e/helpers/e2e-env.ts`，禁硬编码端口 |
| 现场只读走查 | 只有运行实例能复现（升级/发布态） | 运行实例只读复验流程（plugin-publish-verify），**不许停/启/杀宿主** |

## 先红后绿（铁律 1）

1. 写复现测试 → **真跑一次，拿到红灯**，把命令+关键失败行存进 05-evidence（等级 Verified）。
2. 红灯复刻要点：错误串**逐字锚定**（参考 `UniversalGatewayListTodosScopeReproTests` 把 `No service for type 'IServiceScopeFactory'...` 整句断言进去）；时序类缺陷要复刻真实时序（Apply 期 vs seed 期），不要 mock 掉出问题的那一环。
3. 修完 → 同一测试转绿；再按 §A4 决策表跑对应整档验证（P5）。
4. **复现不了**：case §2 如实写"未复现 + 原因 + 现有最强证据等级（Inferred/Unknown）"，不得假造红灯；PC 状态最多到 `open`/`guarded-partial`。

## 枚举式类级测试（G2 的标准形态）

模板（参考 `ProblemClassEnumerationTests.cs` 与 `RepositoryScriptTests.cs`）：

1. `FindRepoRoot()`：从 `AppContext.BaseDirectory` 上溯找仓库根（勿靠 cwd）；
2. 枚举集合：`Directory.EnumerateFiles` + glob 过滤（**两棵插件树都要扫**）；
3. 阳性对照：`scanned.Should().BeGreaterThan(N)` ——扫描集合为空/过小直接红（防 glob 写错假绿）；
4. 逐条断言不变量，失败消息带 `文件:行号` 与命中的证据片段；
5. allowlist 从 PC front-matter 读（单一来源），过期条目直接红。

## 守卫的守卫（防台账腐烂，Registry 测试职责）

`ProblemClassRegistryTests` 断言（机器执行，人不自觉）：
- 每个 PC 文件 front-matter 必填键齐全、status/id 合法；
- `guards` 中 `status: active` 的 `path` 文件真实存在；`path: none` 必须带 `todo`（TODO.md 逐字可检索）+ `expires`；
- case 的 `pc:` 指向存在的 PC；PC `cases`/`recurrences` 反向指向存在的 case 文件；
- `problem-classes/README.md` 台账行 ↔ PC 文件一一对应；`cases/README.md` ↔ case 文件一一对应。

> 为什么需要它：`docs/11-troubleshooting/index.md` 曾停在 2026-08-12 六行没人更新——纯文档索引必然腐烂，腐烂的知识库比没有更危险（给人虚假的安全感）。
