---
id: 2026-11-24-release-netsdk1151-selfcontained-inference
title: 发布本地 zip 走到 host publish 报 NETSDK1151，UpdateAgent 条件式 SelfContained 求值失效被 SDK 推断为自包含
pc: PC-049
severity: P1
evidence: Verified
---
# Case：2026-11-24 发布本地 zip 报 NETSDK1151

> Case 只讲「这一次发生了什么」；规则/修法/扫描方法一律引用 PC，不复述。

## 1 现象

用户执行发布本地 zip：前端阶段每个插件都弹出依赖重建确认（该现象属 PC-048，由并行会话修复），全部确认后走到 host publish 阶段报错：

```
==> host: dotnet publish (Release, win-x64, selfContained=false, SINGLE-FILE, Version=2.4.22.2611241306)
ForgeSelf.Api net10.0 win-x64 失败，出现 1 错误
Microsoft.NET.Sdk.targets(1340,5): error NETSDK1151: 引用的项目"..\ForgeSelf.UpdateAgent\ForgeSelf.UpdateAgent.csproj"是自包含的可执行文件。自包含可执行文件不能由非自包含可执行文件引用。
```

## 2 复现红灯

我这边用同参数最小复现（`.temp/netsdk1151-diag.log`）：
- `dotnet publish ForgeSelf.Api -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true …` → EXIT=1，NETSDK1151。
- **对照实验**：`dotnet build` 同项目同参数（无 publish）→ EXIT=0。差异锁定在 publish 阶段推断。

## 3 根因（5 Whys + 第 6 问）

1. 为什么 publish 挂？→ 宿主非自包含，但引用项目 UpdateAgent 被判为自包含 exe。
2. 为什么 UpdateAgent 被判自包含？→ SDK 的 `ValidateExecutableReferences`（Microsoft.NET.Sdk.targets:1340）读引用项目回传的 `SelfContained=true`。
3. csproj 不是写了 `SelfContained=false` 吗？→ 那行带 Condition（`RuntimeIdentifier != ''`），但写在 `<RuntimeIdentifier>` 赋值**之前**；求值时 RID 为空 → 条件 false → 属性未设置。
4. 未设置为什么不是默认 false？→ SDK 11 `RuntimeIdentifierInference.targets:67-78`：TFM≥8 且 `PublishSingleFile=true` 且 `SelfContained` 为空 → 推断 `PublishSelfContained=true`；85-89 行在 publish 时回写 `SelfContained=true`。宿主命令行的全局 `PublishSingleFile=true` 传播到了引用项目。
5. 为什么宿主传的 `--self-contained false` 没救场？→ `ValidateExecutableReferences` task 源码显示，项目引用协商阶段全局 `SelfContained` 被忽略（仅 `SelfContained=true` 时在构建阶段强制传播），回传值是引用项目自己求值的。
- **第 6 问**：为什么注释写着"FDD、防止误打自包含"却没做对？→ 设计意图与 MSBuild 求值顺序语义之间的鸿沟：注释表达意图，条件式配置表达"我以为的意图"，无守卫校验两者一致；且 build 阶段不触发推断，开发期全绿无感。

## 4 同类扫查命中表

| 位置 | 同根因？ | 处置 |
|------|----------|------|
| `ForgeSelf.UpdateAgent/ForgeSelf.UpdateAgent.csproj:15`（原） | 是（首发） | 改为无条件 `<SelfContained>false</SelfContained>` |
| `ForgeSelf.Bootstrapper/ForgeSelf.Bootstrapper.csproj:7` | 否 | 已是无条件 `<SelfContained>true</SelfContained>`，且独立发布不被宿主引用，符合设计 |
| 其余 csproj（全仓 grep） | 否 | 无 SelfContained 条件式声明 |

## 5 修复与被否方案

canonical fix（PC-049 §3）：UpdateAgent 的 `SelfContained` 去掉 Condition，无条件 false。被否：调整属性顺序（依赖书写顺序易复发）、关闭宿主校验（`ValidateExecutableReferencesMatchSelfContained=false`，掩盖问题）、移除 ProjectReference（破坏随宿主构建的设计）。

## 6 重构提案与结论

R0 不重构。理由：根因是单个属性声明方式错误，非架构缺陷；无条件显式声明 + G2 守卫已覆盖复发面。

## 7 守卫落地

- G2：`ProblemClassEnumerationTests.Pc049_UpdateAgentCsproj_MustDeclareFddSelfContainedUnconditionally`，断言无条件式 SelfContained 且存在无条件 false，含阳性对照（`.temp/pc049.trx` = Passed）。
- G3：PC-049 signature S1 grep + `scripts/check-problem-classes.ps1`。

## 8 逃逸分析（首见）

N/A 首次。原配置来自 `e5358ab`（2026-11-23 12:30 引入原生自更新代理），此前发布链在前端阶段先卡住（PC-047 / PC-048），从未走到 publish，本类问题被前序故障掩盖。

## 9 基建改进

- PC-049 台账 + G2 守卫，把"MSBuild 属性求值顺序"这一隐形陷阱固化为可扫描签名。
- lens 第 3 条沉淀诊断捷径：build 绿 / publish 红 = 优先查 publish-only 推断逻辑，避免盲目排查编译错误。

## 10 复用赋能

新增被宿主引用的可执行项目、调整 publish 形态（单文件/trim/AOT）、发布链报 NETSDK1151 时必须引用 PC-049。

## 11 证据

| 结论 | 等级 | 出处 |
|------|------|------|
| 原始报错 NETSDK1151 | Verified | 用户贴出的终端输出（14:37） |
| 本地最小复现 EXIT=1，build 对照 EXIT=0 | Verified | `.temp/netsdk1151-diag.log` / `.temp/netsdk1151-diag2.log` |
| SDK 推断规则（67-78 / 85-89 行） | Verified | SDK 安装目录 `Sdks/Microsoft.NET.Sdk/targets/Microsoft.NET.RuntimeIdentifierInference.targets`（11.0.100） |
| 校验 task 读取引用项目回传属性 | Verified | dotnet/sdk 开源 `ValidateExecutableReferences.cs`（WebFetch 官方仓库） |
| 修复后 publish 成功 EXIT=0，产物 FDD 单文件 17MB | Verified | `.temp/netsdk1151-m1.log` / `.temp/publish-m1/` |
| G2 守卫 Passed | Verified | `.temp/pc049.trx` |
| **完整 zip 产出并校验通过** | Verified | `artifacts/release/OpenForgeSelf-2.4.22.2611241442-win-x64.zip`（143 MB），`sha256sum -c` = OK |

## 遗留

完整发布链在沙箱工具调用内仍会因 safe-delete 单次配额（49）卡在前端阶段（PC-047 case §遗留，环境约束非仓库 bug）。本次通过 `-SkipFrontend`（前端产物已齐全）+ 分阶段执行完成出包；用户自己的终端环境有弹窗确认机制，不受该配额约束，可一次性跑完整链。
