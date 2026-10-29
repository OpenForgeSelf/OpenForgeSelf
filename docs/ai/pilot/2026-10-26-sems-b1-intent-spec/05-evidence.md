# Evidence

> 阶段：Stage 7｜只记录实际发生的事情。来源等级：Verified（有真实输出）/ Inferred（凭代码或清单推断）/ Unknown（未验证）。

## Task

2026-10-26-sems-b1-intent-spec（Sems B1：意图与规格层，首批落文件、无界面）

## Changed Files

新增：
- Plugins/Sems/Services/SpecFileStore.cs
- Plugins/Sems/Services/SpecService.cs
- Plugins/Sems/Controllers/SpecsController.cs
- Plugins/Sems/SpecTools.cs（5 个 sems_*_spec 工具）
- ForgeSelf.Api.Tests/Plugins/Sems/SpecServiceTests.cs

修改：
- Plugins/Sems/SemsPlugin.cs（注册 ISpecService 与 5 个新工具，88 行）
- Plugins/Sems/plugin.json（版本 1.2.0，Description 改为 18 个工具）
- ForgeSelf.Api.Tests/Plugins/Sems/SemsControllerTests.cs（第 113-114 行：控制器数量断言 3 改 4，用户已同意）
- docs/ai/pilot/2026-10-26-sems-b1-intent-spec/04-task.md（头部 risk 改为单行写法，第 4 行）

## 与 04-task expected_files 的偏差（如实记录，未改 04 的清单）

- 04 写了改 ToolExtensions.cs、新建 SpecService.cs。实际没有改 ToolExtensions.cs（新工具放在新文件 SpecTools.cs）。该点来源等级为 Inferred：我没有跑 git diff 核对。
- 实际多出 SpecFileStore.cs、SpecTools.cs、SpecServiceTests.cs、SemsControllerTests.cs 四个文件，04 未列。
- Plugins/Sems/TODO.md、Plugins/Sems/docs/requirements.md 在 04 里列为回写目标，截至本文写成时尚未回写。

## Build

Command: dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj

Result: PASS（来源等级：Verified）

    exitCode 0，用时约 25.6 秒；输出含 Sems -> ...\Plugins\Sems\bin\Debug\net10.0\Sems.dll
    警告：NU1510、NU1903（Microsoft.OpenApi 2.3.0）、若干 CS8601/CS8603/CS8618/CS8765/CS0414、CA1416，均不在本次改动文件内

测试工程的编译随下面的 dotnet test 完成（不带 --no-build），无编译错误。

## Unit Test

第一次运行：

    dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~Sems --logger trx --results-directory .temp/results-sems -clp:ErrorsOnly

Result: FAIL（来源等级：Verified）。总数 80，通过 79，失败 1。

    SemsControllerTests.Every_Sems_Controller_Class_Carries_ApiKeyPolicy_Authorization
    Expected controllers to contain 3 item(s), but found 4（多出 SpecsController）

原因：既有测试把控制器数量写死为 3。修复：第 113-114 行改为 4。

第二次运行（修复后）：

    dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~Sems --logger trx --results-directory .temp/results-sems2 -clp:ErrorsOnly

Result: PASS（来源等级：Verified）

    .temp/results-sems2/12504_DGCWIAX10_2026-10-26_14_00_22_net10.0.trx 第 494 行：
    total=80 executed=80 passed=80 failed=0；搜索 outcome="Failed" 无匹配
    SpecServiceTests 共 11 条结果，全部 Passed

架构守卫：

    dotnet test ForgeSelf.Api.Tests --no-build --filter FullyQualifiedName~PluginToolConstructorDiGuard --logger trx --results-directory .temp/results-guard2 -clp:ErrorsOnly

Result: PASS（Verified）：通过 1，失败 0。该次 --no-build 使用的 dll 来自上一条不带 --no-build 的构建，晚于全部文件写入。

SpecServiceTests 的 11 条结果覆盖：创建后加版本得到 v1、v2 且保留 v1；20 路并发加版本不丢不重；specId 为空、ZZZ、../evil 被拒；未知项目返回 NotFound；标题或内容为空返回 Invalid；项目根变化后孤儿规格不被列出；关联记录只追加且坏输入被拒；5 个新工具 Id 唯一且 schema 合法；工具往返与缺参数容错。

## 2026-10-30 复跑（本节覆盖上文 80/80、84/84 与「AC2 未实现」）

Command:

    dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~Sems|FullyQualifiedName~PluginToolConstructorDiGuard --logger trx --results-directory .temp/results-sems-b1b

TEMP/TMP 设为仓库 `.temp/tmp`。同一条命令先构建再测。

Result: PASS（Verified）

    .temp/results-sems-b1b/sems-b1b.trx：total=103 executed=103 passed=103 failed=0
    控制台正文：已通过，失败 0，通过 103，已跳过 0，总计 103，持续时间 29 s
    其中含 Sems 过滤集与 PluginToolConstructorDiGuard。更早一轮仅 Sems 过滤为 .temp/results-sems-b1/sems-b1.trx：total=101 passed=101 failed=0（当时还没有注册守卫）。

本轮相对上文多出的文件：SpecCoverageTests.cs、SemsPluginRegistrationTests.cs。ToolExtensions.cs 未改。插件注册 13 个既有工具 + 9 个规格工具。plugin.json Version 为 1.2.0，描述写 22 个 sems_* 工具。

## 按验收标准逐条（02-spec，以 2026-10-30 复跑为准）

- AC1（B1-AC1，规格查关联变更；变更查所依据规格及版本）：满足。SpecServiceTests 断言同一 commit 反查得到 Version，升到 v2 后旧标识仍是版本 1、新标识是版本 2（Verified，含在 103/103 内）。
- AC2（B1-AC2，每条验收标准是否已关联用例）：满足。SpecService.GetCoverage / LinkCase、sems_link_spec_case、sems_get_spec_coverage、SpecCoverageTests.Coverage_Shows_Each_Criterion_Linked_Or_Not 在 103/103 内 Passed（Verified）。上文「未实现」作废。
- AC3（B1-AC3，旧版本仍可查看；关联变更指向生成时的版本）：满足。Create_Then_AddVersion_Yields_V1_V2_And_Keeps_V1 与「升版本后关联仍指向旧版本」的用例在 103/103 内 Passed（Verified）。
- AC4（既有 13 个工具与端点行为不变，既有测试通过）：满足。SemsToolExtensionTests 仍只构造原来 13 个工具并跑通；SemsPluginRegistrationTests 断言这 13 个名字都在 Apply 的注册结果里，且具体工具类型集合与程序集里的非抽象 IToolFunctionExtension 相等（Verified，103/103）。
- AC5（新增写端点类级 [Authorize("ApiKeyPolicy")]，匿名返回未授权）：部分。类级 ApiKeyPolicy、SpecsController 无 AllowAnonymous、全插件仅 ProjectsController.GetCount 带 AllowAnonymous，均在 103/103 内 Passed（Verified）。没有发起匿名 HTTP 请求，运行时是否返回未授权仍是 Unknown。
- AC6（构建通过，新增测试通过，判据看日志正文）：满足。本轮 dotnet test 先编译 Sems 与 ForgeSelf.Api.Tests，再给出 103/103（Verified）。

## Integration Test

SpecCoverageTests 直接调用 SpecsController 的创建、登记用例与覆盖端点，断言 OkObjectResult，未知标准为 400，不存在的规格与项目为 404（Verified，含在 103/103）。响应体字段封套与运行时匿名 HTTP 未测（Unknown）。

## E2E

未执行（Unknown）。六步门禁的第 2 步（本插件 1 个冒烟 e2e）未做。

## Static Analysis

N/A。本次没有改 web/，前端 pnpm run check / design:check 不适用。

## Screenshots

N/A。K3 首批无界面。

## Known Limitations

- 六步门禁第 2-5 步均未执行：冒烟 e2e、dev-stack.ps1 源码走查、发布、:51888 只读复验。因此不能写成已验收或已发布。
- AC5 的运行时匿名未授权仍是 Unknown。
- 上文「与 04-task expected_files 的偏差」写于工具还是 20 个、AC2 未实现时。2026-10-30 已把头部 expected_files 改成实际文件（含 SpecFileStore、SpecTools、三份测试；不含未改的 ToolExtensions.cs）。

## Unresolved Issues

- 运行时匿名 401，以及六步门禁第 2-5 步。单测失败数为 0。