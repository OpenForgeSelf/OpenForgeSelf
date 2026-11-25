# 02 Spec — NETSDK1151 根因与修法

## Functional Requirements
- FR1：`ForgeSelf.UpdateAgent.csproj` 的 `SelfContained` 必须无条件显式为 `false`，消除 SDK publish 推断链。
- FR2：新增 G2 守卫测试，断言无条件声明存在且无条件式写法不存在，带阳性对照。
- FR3：产出本地 zip 并通过 SHA256 与内容校验。

## Input / Output
- 输入：`release-local.ps1` 默认参数（本地版本从 csproj 三段 VersionPrefix 补时间码）。
- 输出：`artifacts/release/OpenForgeSelf-<ver>-win-x64.zip` + SHA256SUMS.txt + RELEASE-NOTES。

## Business Rules
- BR1：UpdateAgent 设计形态是 FDD 单文件（csproj 注释 + publish-updateagent.ps1 体积断言双声明）。
- BR2：宿主编排要求 UpdateAgent 随宿主构建（ProjectReference ReferenceOutputAssembly=false）。
- BR3：修复只动属性声明，不改 publish 命令、不改校验开关。

## Boundary Conditions
- 不改 SDK、shim、发布脚本、宿主 csproj。
- 不碰并行会话在途文件（AGENTS.md / 工程流程文档 / hooks / build-frontend.ps1）。
- 本工具环境完整链卡 safe-delete 配额时，用脚本已有 `-SkipFrontend` 分阶段出包，不调阈值不关守卫。

## Error Handling
- 若最小 publish 仍失败：对照 build 结果区分推断问题与编译问题（本次 build 绿 publish 红 → 推断确凿）。
- 若 MSBuild 并行节点崩溃（MSB4166，本沙箱环境）：`-m:1` 规避，非产品改动。
- 若 zip SHA256 校验报文件不存在：检查 CRLF，规整后重校（本次为换行符误报，非产物损坏）。

## Compatibility
- .NET SDK 11.0.100 + TFM net10.0（本机实测）；规则在 .NET 8+ SDK 通用。
- 与 PC-047（emptyOutDir）、PC-048（pnpm 非 TTY）同链不同根因，修复互不干扰。

## Failure Modes（缺陷类必写，源于 P0 命中的 lens）
- FM1：条件式 SelfContained 写在 RID 赋值前 → 求值空 → SDK 推断自包含 → NETSDK1151（本次根因）。
- FM2：宿主引用协商忽略全局 SelfContained=false → 命令行 `--self-contained false` 救不了引用项目。
- FM3：注释声明意图但无守卫校验 → 注释与行为悄悄分叉（G2 补齐）。

## Acceptance Criteria
- AC1：UpdateAgent 无条件显式 SelfContained=false，全仓无条件式 SelfContained 遗留。
- AC2：同参数 `dotnet publish` 最小复现 EXIT=0，产物 FDD 单文件（无自包含运行时目录）。
- AC3：G2 守卫 `Pc049_…` 通过，check-problem-classes 与 Registry 全绿。
- AC4：本地 zip 完整产出，SHA256 校验通过，关键文件齐备。

## Interaction Design
无界面交互。本任务是后端构建配置 + 测试 + 文档修复，无 UI 变更、无用户交互面；验证通过命令行（dotnet publish / sha256sum）完成。
