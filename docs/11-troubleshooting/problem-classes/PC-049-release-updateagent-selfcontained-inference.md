---
id: PC-049
title: 被宿主引用的可执行项目条件式声明 SelfContained 求值失效，SDK 推断自包含触发 NETSDK1151 阻断 publish
tags: [发布链, dotnet-publish, MSBuild, SelfContained, NETSDK1151]
invariant: 被宿主 ProjectReference 的可执行项目必须无条件显式声明 SelfContained——条件式声明若依赖 RuntimeIdentifier 且写在 RID 赋值之前，求值时 RID 为空导致属性不生效；宿主 publish 传全局 PublishSingleFile=true 时 SDK 推断 PublishSelfContained=true 并回写 SelfContained=true → NETSDK1151「非自包含 exe 不能引用自包含 exe」
status: guarded
first_seen: 2026-11-24
recurrences: []
cases: [2026-11-24-release-netsdk1151-selfcontained-inference]
signature:
  - {id: S1, kind: grep, glob: "ForgeSelf.UpdateAgent/ForgeSelf.UpdateAgent.csproj", regex: "<SelfContained\\s+Condition\\s*=", expect: no-match}
  - {id: S2, kind: test, path: "ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs"}
allowlist: []
guards:
  - {level: G2, path: "ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs", status: active}
lens:
  - 新增被宿主 ProjectReference 的可执行项目时，SelfContained 必须无条件显式声明（true 或 false），不得用 Condition 依赖 RuntimeIdentifier——MSBuild 按属性书写顺序求值，条件里引用的属性可能还未赋值。
  - 发布链报 NETSDK1151 时，先查「被引用 exe 的 SelfContained 是否显式声明」及「引用方 publish 是否传 PublishSingleFile=true」——后者会触发 SDK11 的 PublishSelfContained 推断（RuntimeIdentifierInference.targets:67-89），把未声明的 SelfContained 变成 true。
  - dotnet build 成功而 dotnet publish 失败（同一 csproj）是该类问题的强特征——推断逻辑只在 _IsPublishing=true 时生效；排查时先做这个对照实验，可快速区分 MSBuild 推断问题与编译/引用错误。
---
# PC-049 条件式 SelfContained 声明求值失效 → SDK 推断自包含 → NETSDK1151

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。
> 状态机与守卫等级语义见技能 `.agents/skills/problem-class-closure/references/07-recording-spec.md`。

## 1 不变量与诱因

**不变量**：被宿主以 `ProjectReference` 引用的可执行项目（`OutputType=Exe`），其 `SelfContained` 必须**无条件显式声明**。条件式声明（如 `<SelfContained Condition="'$(RuntimeIdentifier)' != ''">false</SelfContained>`）若写在 `<RuntimeIdentifier>` 赋值之前，求值阶段 RID 为空 → 条件不成立 → 属性根本没被设置。此时宿主 publish 传入的全局 `PublishSingleFile=true` 会传播到引用项目，SDK 11 据此推断 `PublishSelfContained=true`（`RuntimeIdentifierInference.targets:67-78`），再在 publish 阶段回写 `SelfContained=true`（同文件 85-89 行）→ 宿主非自包含引用自包含 exe → `NETSDK1151`。

**为什么会犯**：
- MSBuild 属性按书写顺序求值，条件里引用的属性若尚未赋值，条件静默为 false——不报错、不告警，最难察觉。
- 原配置注释明确写着"FDD、防止误打自包含"，设计意图正确，但实现（条件顺序）没做到——注释与行为分叉。
- 项目引用协商阶段（`_GetProjectReferenceTargetFrameworkProperties`）会忽略全局 `SelfContained=false`，所以宿主命令行传的 `--self-contained false` 救不了引用项目。
- `dotnet build` 不触发 publish 推断，开发期常规构建全绿，只有真正 `dotnet publish` 才暴露。

## 2 特征签名与扫描方法

- **S1**：grep `ForgeSelf.UpdateAgent/ForgeSelf.UpdateAgent.csproj` 中 `<SelfContained\s+Condition\s*=` 即红（应为 0 命中）。
- **S2**：G2 测试 `ProblemClassEnumerationTests.Pc049_UpdateAgentCsproj_MustDeclareFddSelfContainedUnconditionally`，断言无带 Condition 的 SelfContained 且存在无条件 `<SelfContained>false</SelfContained>`，带阳性对照（OutputType=Exe）。
- **强特征诊断**：同一 csproj `dotnet build` 成功但 `dotnet publish` 报 NETSDK1151 → 优先怀疑本类（推断只在 publish 生效）。
- **盲区**：只扫 UpdateAgent 一个文件；若新增其他被宿主引用的 exe 项目且复制了同样的条件式写法，S1/S2 扫不到，需人工复核（lens 第 1 条已提示）。

## 3 Canonical fix

把条件式声明改为无条件显式声明：

```xml
<!-- 改前（RID 求值时为空 → 不生效） -->
<SelfContained Condition="'$(RuntimeIdentifier)' != ''">false</SelfContained>
<RuntimeIdentifier Condition="'$(RuntimeIdentifier)' == ''">win-x64</RuntimeIdentifier>

<!-- 改后（无条件显式，_SelfContainedWasSpecified=true → SDK 不再推断） -->
<SelfContained>false</SelfContained>
<RuntimeIdentifier Condition="'$(RuntimeIdentifier)' == ''">win-x64</RuntimeIdentifier>
```

依据：UpdateAgent 的设计形态就是 FDD 单文件（csproj 注释 + `publish-updateagent.ps1` 体积断言双重声明），无条件 false 才真正表达该意图。

**被否方案**：
- 把 `<RuntimeIdentifier>` 行提到前面让条件成立 → 改 3 行顺序，语义等价但依赖书写顺序，后续调整仍易改坏；无条件声明更稳。
- 在 `ForgeSelf.Api.csproj` 的 ProjectReference 上加 `ValidateExecutableReferencesMatchSelfContained=false` → 关闭校验而非修复，掩盖真实形态不一致。
- 把 UpdateAgent 的 `ReferenceOutputAssembly` 改为其他值或移除引用 → 破坏"随宿主一起构建"的设计（2026-11-21 输入15），改动面远大于必要。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G2 | `ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs`（Pc049…） | `dotnet test` | 只扫 UpdateAgent，新增同类 exe 项目需人工复核 |
| G3 | `scripts/check-problem-classes.ps1` S1 签名 | pre-commit / 每次归档校验 | 同上 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| [2026-11-24-release-netsdk1151-selfcontained-inference](../cases/2026-11-24-release-netsdk1151-selfcontained-inference.md) | 2026-11-24 | 发布本地 zip 走到 host publish 报 NETSDK1151，UpdateAgent 被判为自包含 exe | 否（首次） |
