---
id: PC-028
title: 网关入口各自解析模型名，对外别名只在部分入口换成上游 id
tags: [gateway, public-alias, model-resolution, responses]
invariant: 凡把客户端 model 发给上游的入口，必须走 AIProviderRegistry.ResolveUpstreamModel；启用中的 PublicAlias 出站前换成 UpstreamModelId，禁用则一律不路由。禁止只解析出提供方、仍把别名原样当模型名。
status: guarded
first_seen: 2026-11-12
recurrences: []
cases: [2026-11-12-responses-alias-forwarded-404]
signature:
  - {id: S1, kind: grep, glob: "ForgeSelf.Api/Controllers/UnifiedAI/*.cs", regex: "ResolveProviderAndModel", expect: no-match}
  - {id: S2, kind: test, path: "ForgeSelf.Api.Tests/AIModelFeatureTests.cs"}
allowlist: []
guards:
  - {level: G1, path: "ForgeSelf.Api.Tests/AIModelFeatureTests.cs", status: active}
  - {level: G2, path: "ForgeSelf.Api.Tests/AIModelFeatureTests.cs", status: active}
lens:
  - 新的对话入口是调用 ResolveUpstreamModel，还是又抄了一份只返回提供方的解析？
  - 别名启用时，发给上游的 model 字符串是 UpstreamModelId 还是客户端传来的别名？
  - 别名禁用时，这个入口是拒绝，还是仍然落到默认提供方？
---
# PC-028 网关别名必须换成上游模型 id

## 1 不变量与诱因

`/v1/models` 把 PublicAlias 当作对外 id。客户端会原样拿这个 id 来调聊天接口。出站模型名必须是上游认识的 UpstreamModelId。

诱因：Chat 把别名反查写在控制器私有方法里。Responses / Agent 后加入口时抄了前半段（`provider:model` 拆分），没抄 `FindByPublicAlias`。`GetProviderByModel` 能靠别名找到提供方，但不返回上游 id，调用方就继续用原字符串。

## 2 特征签名与扫描方法

S1 禁止 UnifiedAI 控制器再出现私有 `ResolveProviderAndModel`。S2 锁住「别名 default → qwen3_8_vision」以及四个入口源码都调用 `ResolveUpstreamModel`。

盲区：不经过这四个控制器、直接 `GetProviderByModel` 再把原 model 字符串交给 SDK 的新代码，S1 扫不到。

## 3 Canonical fix

唯一入口：`AIProviderRegistry.ResolveUpstreamModel`。无冒号且命中启用别名 → `(提供方, UpstreamModelId)`；禁用 → `(null, 原名)`；`provider:model` → 拆开；其余回退 `GetProviderByModel` 并保留原名。控制器在调用上游前执行 `request.Model = upstreamModelId`。

被否：只改 Responses 私有方法。下次再抄一份还会丢别名分支。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `ResolveUpstreamModel_PublicAlias_SubstitutesUpstreamId` | `dotnet test` 过滤该用例 | 只覆盖别名/前缀/禁用，不打真实 GPUStack |
| G2 | `GatewayModelEntries_CallSharedResolveUpstreamModel` | 同上 | 只扫四个已知控制器文件 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-11-12-responses-alias-forwarded-404 | 2026-11-12 | 别名 default 在 Responses 原样发给 GPUStack，404；Chat 能换成 qwen3_8_vision | 否（首次） |
