# 05-evidence：AI-agent 插件文件三件套默认根修复

> 分级：Verified（实跑输出原文）/ Inferred / Unknown。复核锚点：日志落 `.temp/tmp/`（build-api-20261012.log、build-api-gate-redirect.log、test-pwsd-20261012-r1.log、test-pwsd-20261012-r2.log、pwdefault-test-run3.log 等）。

## 1. 后端构建（Verified）
- 命令：`cd ForgeSelf.Api; dotnet build --nologo -v q`
- 首轮：4 error 全为 MSB3027/MSB3021 文件锁（`.NET Host (102508)` = dev-stack 起源码实例 ：7301，禁杀铁律）→ 非 CS 代码错。
- 第 2 轮 `-o .temp\tmp\gate-build-api` 旁路：日志正文 **0 个错误**（1348 个既有警告，全为生成件 CS86xx 类）；产物锚点 `ForgeSelf.dll`/`ForgeSelf.Core.dll`/`ForgeSelf.Abstractions.dll` 三件套落盘确认。

## 2. 过滤集测试（Verified）
- 命令：`dotnet test --filter "FullyQualifiedName~ProjectWorkspaceDefaultRoot" -p:OutDir=<repo>\.temp\tmp\gate-test-outdir`
- 日志原文：`已通过! - 失败: 0，通过: 24，已跳过: 0，总计: 24，持续时间: 198 ms - ForgeSelf.Api.Tests.dll (net10.0)`
- 24 例构成：本批新增 16 例（DefaultRoot 三级候选/缓存、ResolveSafePath 回退/直通/越界/优先级、FailEnvelope 两态、三件套守卫三态）+ 旧 ProjectWorkspaceTests 同名系列回归 8 例。
- 附证：mini-task 早记录的 `WorkflowEnginePluginRegistrationTests.cs:63 CS0234` 已不命中——`WorkflowServiceResolver` 现存于 `Plugins/AIAgent/AIAgentPlugin.cs:172`（internal + InternalsVisibleTo），未被本批触碰。

## 3. 测试红→绿真实修复轮（仅动测试文件）
- Round1：宿主 DLL 锁 → `BuildProjectReferences=false` 迁回（不动实现）。
- Round2：read_file 成功信封无 `root` 字段（实为 `usedRoot`）断言修正 + CS0103 变量绑定。
- Round3：全绿。实现代码 0 修复轮。

## 4. 需求逐条核验（Requirement Check）
| Success Criteria | 结论 | 分级 |
|---|---|---|
| 1 冷启动三件套可用（默认根=env 缺失回退 BaseDirectory 上溯） | 代码落地 + A-F 用例覆盖，24/24 绿 | Verified |
| 2 优先级链 GUI 选定 > env > 上溯祖先 | 测试钉住（env→temp 覆盖 GUI 场景、双层 null 断言） | Verified（单测级） |
| 3 绝对路径根内直达/根外拒（防穿越不放松） | 用例 D（hosts 越界）/E（已选目录绝对越界）覆盖 | Verified（单测级） |
| 4 「尚未选择项目目录」不可达化：两态封套带 hint/usedRoot | 三件套信封用例（F 组）覆盖 | Verified（单测级） |

## 5. 未做与风险（诚实交代）
- **未做（按输入4 范围）**：插件 e2e、dev-stack 起源码走查、发布、运行实例只读复验、Plugin README 回写——②-⑥ 维护闭环留待后续批次。
- **风险 Inferred**：默认根语义目前仅单测级验证，未在真实宿主浏览器链路点过 read/write/list；上溯候选 b 以 `AppContext.BaseDirectory` 起算（publish 布局=宿主根，dev 布局=bin 目录，dev 下首个命中祖先通常即含 .slnx 的仓根；极端包装目录下可能上溯不到 → 封套引导用户 env/选目录）。
- **Unknown**：GUI 长会话中 `ProjectRoot` 重置后三件套是否如设计回退默认根（依赖宿主重新实例化 service 的行为），未实测。
