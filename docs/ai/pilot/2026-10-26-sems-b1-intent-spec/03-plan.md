# Plan

> 阶段：Stage 3｜具体到真实文件。
> Task ID：2026-10-26-sems-b1-intent-spec
> 状态：草案。闸门 1 要拍板的 K1（存储方式）与 K3（是否含界面）决定文件清单，所以本 Plan 按分支列出，拍板后定稿。定稿前不得进入 Implement。

## Files To Change

以下文件的路径来自已读的目录与规则，类名为拟新建名，不代表仓库已存在。

公共部分，不论存储方式：

- file: `Plugins/Sems/Services/SpecService.cs`（拟新建）。reason: 规格的记录、版本化、关联查询，委托存储层，沿用 SemsResult 封套。
- file: `Plugins/Sems/Controllers/SpecsController.cs`（拟新建）。reason: HTTP 端点，类级 [Authorize("ApiKeyPolicy")]。HTTP 方法以 [Http*] 特性为准。
- file: `Plugins/Sems/ToolExtensions.cs`（修改）。reason: 追加规格相关 sems_* 工具，入参描述含示例；既有 13 个工具不改。
- file: `Plugins/Sems/SemsPlugin.cs`（修改）。reason: Apply 中注册 SpecService 及新工具。
- file: `Plugins/Sems/plugin.json`（修改）。reason: 版本递增，description 中的工具数量同步。
- file: `Plugins/Sems/TODO.md`（修改）。reason: 登记本任务进度与遗留。
- file: `Plugins/Sems/docs/requirements.md`（修改）。reason: B1 的「Sems 现状」回填，每条指到文件。

分支 K1=落文件：

- file: `Plugins/Sems/Services/SpecFileStore.cs`（拟新建）。reason: 读写规格文件，落位在运行时数据根下的插件目录，不放源码 docs/（该目录不进包）。具体落位须先查 docs/04-standards/packaging-upgrade-backup.md 的落位裁决。

分支 K1=插件自有实体库（须闸门 1 明文授权库结构变更）：

- file: `Plugins/Sems/Data/Model.xml`（拟新建）。reason: 实体真源，对齐 Plugins/Scheduler/Data/Model.xml 的 ConnName、Namespace、Output=Entities。
- file: `Plugins/Sems/Data/SemsTables.cs`（拟新建）。reason: 建表真源，EntityFactory.InitConnection 加探活，异常不吞。
- file: `ForgeSelf.Api/Data/XCodeConfig.cs`（修改）。reason: 登记连接名进 PluginDbs。
- file: `ForgeSelf.Api/ForgeSelf.Api.csproj`（修改）。reason: 补插件引用，ReferenceOutputAssembly=false。
- file: `ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj`（修改，有单测时）。reason: 三处登记齐全。
- file: `Plugins/Sems/Sems.csproj`（修改）。reason: 补 XCode 引用。

分支 K3=含界面：

- file: `Plugins/Sems/web/`（修改，具体文件待读 web/ 后定）。reason: 规格页签。须先补读 web/，当前 Unknown。

测试：

- file: `ForgeSelf.Api.Tests/` 下新增 Sems 规格测试文件（路径与命名待读现有 Sems 测试后定，K7）。reason: AC1 到 AC5 的单测或集成测试。

## Implementation Steps

1. 闸门 1：用户批准 00 到 04，并回答 K1、K2、K3、K4。
2. 补读 Plugins/Sems/Controllers/*.cs、web/、现有 Sems 测试，回填 K7、K8，核对既有契约。
3. 读 docs/04-standards/packaging-upgrade-backup.md 落位裁决，确定数据落位。
4. 依回答定稿本 Plan 的文件清单，偏差记入下表。
5. 先写测试（红），再实现至绿，沿用 NewLife.XCode 等现有方式，不引入另一套。
6. 插件六步门禁：门禁构建与测试，1 个冒烟 e2e，pwsh -NoProfile -File scripts/dev-stack.ps1 起源码验收，发布，运行实例只读复验，更新文档与日志。
7. 回写 requirements.md 的 B1「Sems 现状」，并在 TODO.md 移除本待办。

## Test Plan

- AC1：单测——建规格，登记变更关联，正向与反向查询一致。
- AC2：单测——规格含 N 条验收标准，部分关联用例，返回逐条状态。
- AC3：单测——修改后旧版本可查，关联仍指向旧版本。
- AC4：既有 Sems 相关测试全跑，保持通过。
- AC5：集成测试——匿名调用写端点被拒。
- 边界：0 条验收标准、多对多关联、存储不可用。
- 若新增库：测试夹具连接串指向随机后缀隔离目录，每个测试类独立数据库，唯一性校验直查 DB，不读 Meta.Cache。

## Verification

- Build：`dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj`。依据：AGENTS.md。若新增库，还须核对宿主产物 DLL 存在且 md5 相同。
- Unit Test：`dotnet test ForgeSelf.Api.Tests`，筛选范围待 K7 后定。
- Integration Test：AC5 的鉴权测试，同上。
- E2E：Sems 1 个冒烟 e2e，仅在 K3=含界面时需要；不含界面写 N/A，依据是无界面变更。
- 门禁：pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-26-sems-b1-intent-spec -EnforceHeader -EnforceAcCoverage
- 所有判据看日志正文，不拿 exit code 当证据；执行类命令须用户明确同意。

## Other Checks

- pnpm run design:check：仅 K3=含界面。
- 禁止 agent 停、启、杀宿主进程；发布走本地目录更新源，由用户在运行实例点自动更新。
- 若命中缺陷类发现，按 problem-class-closure 同类扫查。

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先记录再修正。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-10-26 | 存储方式未定，文件清单无法一次具体到位 | 要求具体到真实文件 | 按分支列出，闸门 1 拍板后定稿；定稿前不 Implement |

## 增补方案：补 B1-FR2 查询侧（2026-10-26，待用户批准后才实现）

> 触发：读码发现 SpecService 与 SpecFileStore 只写 links.jsonl、没有任何读取方法，AC1 两个方向都做不到（依据：Services/SpecService.cs、Services/SpecFileStore.cs）。这是实现缺口，不只是测试缺口。本增补超出闸门 1 当时批准的 8 项写入，须用户批准。

1. 目标：AC1 成立。给定规格能列出其全部关联；给定 kind 与 ref 能反查所依据的规格及版本。判据：新增测试通过，Sems 过滤集全绿，判据取自 .trx 正文。
2. 改动（5 条）：
   - Services/SpecFileStore.cs → 增 ReadLinks(projectId, specId)，读 links.jsonl，文件不存在返回空，坏行跳过不抛。
   - Services/SpecService.cs → ISpecService 与实现增 ListLinks(projectId, specId) 与FindByRef(projectId, kind, ref)，沿用 SemsResult 封套，反查范围限定在同一 projectId 内。
   - Controllers/SpecsController.cs → 增 GET {specId}/links 与 GET by-ref?kind=&ref=，类级鉴权不变。
   - Plugins/Sems/SpecTools.cs、SemsPlugin.cs、plugin.json → 增 sems_list_spec_links 与 sems_find_spec_by_ref 两个工具并注册，plugin.json 描述 18 个改 20 个，版本仍为未发布的 1.2.0。
   - ForgeSelf.Api.Tests/Plugins/Sems/SpecServiceTests.cs → 增 AC1 双向、多对多、AC3（升版本后关联仍指向旧版本）、坏行跳过的测试。
3. 不改：AC2 与 B1-FR3（验收标准与用例关联）、既有 13 个工具与既有端点、web/、宿主与 scripts。读码发现的四个风险（AddVersion 在锁外读最新版、Get/AddVersion/Link 不做孤儿检查、损坏 JSON 抛 500、ApiController 自动 400 不走既有封套）只记 TODO，不在本批修。
4. 验证（快档）：dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~Sems 与 --filter FullyQualifiedName~PluginToolConstructorDiGuard 各一条（执行器不允许 | 与链式），不带 --no-build 先编译；读 .trx 的 Counters 与 SpecServiceTests 各条 outcome。完成后重跑 verify-pilot-artifacts。
5. 代价与风险：新增约 5 个文件的改动，工具数从 18 变 20；既有 SemsToolExtensionTests 为手工构造 13 个实例，预计不受影响（Inferred）。回滚点为 git revert，当前尚未提交。
6. 待你拍板：
   - 同意补 B1-FR2 查询侧吗？默认推荐：同意。
   - AC2 是否属 B1 首批范围？默认推荐：不算，仍记 B1-1。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-10-26 | Plan 的 AC1 测试写「正向与反向查询一致」，但已实现的 SpecService 没有读取关联的方法 | 首批含关联的正反查询 | 增补方案补查询侧；批准前不实现 |

## 增补方案二：B1-AC2 验收标准与用例关联 + 三项测试守卫（2026-10-26，待用户批准后才实现）

> 触发：用户 2026-10-26 回复「按默认」，B1-AC2 划入首批；用例标识首批手工登记。读码发现现有 SpecVersion 只有 Title 与 Content 两个内容字段，没有验收标准条目，AC2 要先补结构（依据：Services/SpecFileStore.cs 第 7-10 行、Services/SpecService.cs）。本增补超出闸门 1 批准的范围，须用户批准。

1. 目标：AC2 成立，给定含 N 条验收标准的规格版本，能显示每条标准是否已登记用例。同批补注册守卫测试、控制器授权断言动态化、AC1/AC3/AC5 与端点测试。判据：新增测试通过，Sems 过滤集全绿，判据取自 .trx 正文。
2. 改动（7 条）：
   - Services/SpecFileStore.cs → SpecVersion 末尾加可空参数 Criteria（每条含 Id 与 Text，同版本内 Id 唯一）；新增 SpecCase 记录与 cases.jsonl 的追加、读取方法，坏行跳过；不复用 links.jsonl，避免 FindByRef 混入用例记录。
   - Services/SpecService.cs → Create 与 AddVersion 增加可选 criteria 入参；新增 LinkCase（校验版本存在且 CriterionId 属于该版本）与 GetCoverage（版本默认最新，返回每条标准的 Id、Text、已登记用例列表、是否已关联，以及 total 与 linked 汇总）。
   - Controllers/SpecsController.cs → 新增 POST {specId}/cases 与 GET {specId}/coverage；Create 与 AddVersion 的请求体增加可选 Criteria；类级鉴权不变。
   - SpecTools.cs、SemsPlugin.cs、plugin.json → 新增 sems_link_spec_case 与 sems_get_spec_coverage 并注册；ShapeSpec 输出 criteria；create 与 add_version 的 schema 加可选 criteria；plugin.json 描述 20 个改 22 个，版本仍为未发布的 1.2.0。
   - ForgeSelf.Api.Tests/Plugins/Sems/SpecServiceTests.cs → 增 AC2、旧版本文件缺 Criteria 字段时可读（须用测试证明）、重复登记同一用例读取去重、CriterionId 不属于该版本返回 Invalid、AC1 反查带出规格版本、AC3 升版本后关联仍指向旧版本。
   - ForgeSelf.Api.Tests/Plugins/Sems/SemsPluginRegistrationTests.cs（新建）→ 注册守卫，全部动态断言：程序集内所有实现 IToolFunctionExtension 的非抽象类与 Apply 实际注册的类型集合相等；Id 唯一且以 sems.tool. 开头；plugin.json 描述里「N 个 sems_* 工具」的 N（正则取出）等于实际注册数；注册数大于 0。经公开入口 Apply 调用，用宽松 Mock&lt;IContext&gt;（Inferred：Apply 对 services 用 ?. 保护，未跑过）。
   - ForgeSelf.Api.Tests/Plugins/Sems/SemsControllerTests.cs → 控制器授权断言改动态：枚举 Sems 命名空间全部控制器，每个都带 [Authorize("ApiKeyPolicy")]，匿名端点只允许 GET api/projects/count，扫到的控制器数大于 0。另补 SpecsController 端点的状态码与封套测试，匿名未授权（AC5）按能在单测内证明的程度写，运行时匿名行为仍记 Unknown。
3. 不改：既有 13 个工具与既有端点行为、ToolExtensions.cs、web/、宿主与 scripts；自动扫描测试项目生成用例关联（后续待办）；读码发现的四个风险（AddVersion 在锁外读最新版、Get/AddVersion/Link 不做孤儿检查、损坏 JSON 抛 500、ApiController 自动 400 不走既有封套）只记 TODO。
4. 验证（快档）：dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~Sems 与 --filter FullyQualifiedName~PluginToolConstructorDiGuard 各一条（执行器不允许 | 与链式，30 秒超时后轮询 .temp 下的结果目录读 .trx）；先 dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj；完成后重跑 verify-pilot-artifacts。六步门禁后四步另算，未做前不得写已验收、已发布。
5. 代价与风险：约 8 个文件改动，工具数 20 变 22；「已关联」只表示有人登记了字符串，不表示用例存在或通过，工具描述与文档须写明，否则易被误读为验证结果；SemsControllerTests.cs 属既有测试，须在 04 的 expected_files 补记；旧 v*.json 缺字段时的反序列化行为靠测试证明，不靠推断。回滚点为 git revert，当前尚未提交。
6. 待你拍板：
   - 同意按上述 7 条实现吗？默认推荐：同意。
   - 用例标识约定为「测试文件路径::用例名」，只校验非空、不检查文件是否存在，可以吗？默认推荐：可以。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-10-26 | 02-spec 把 AC2 写成「显示每条标准是否已关联用例」，但规格版本没有验收标准条目 | 首批只做规格自身记录与关联 | 增补方案二先补结构化 Criteria，再做用例关联；批准前不实现 |