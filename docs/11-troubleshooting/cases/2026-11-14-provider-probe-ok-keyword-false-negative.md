---
id: 2026-11-14-provider-probe-ok-keyword-false-negative
title: 测试连接（选定模型）把正常回复误判为失败——探针判据绑定关键词 ok（PC-034 首见）
pc: PC-034
severity: P2
evidence: Verified
---
# Case：2026-11-14 测试连接探针「含 ok 判据」假阴性

## 1 现象

用户在设置页对某提供方做「测试连接（选定模型）」，上游调用成功、模型正常出话，但回复为「我无法按要求只回复指定词语。」，页面提示失败：

```
模型未回复 ok（实际回复：我无法按要求只回复指定词语。）
```

连接与模型实际可用（能正常出话），属判定假阴性。

## 2 复现红灯

- **可重复红灯（Verified）**：`ForgeSelf.Api.Tests/Unit/AIProviderTestConnectionOkGuardTests.cs`（修复前载体）矩阵改为新语义后 3 例失败——`SaysOk("我无法按要求只回复指定词语。")=false`、`SaysOk("no")=false`、`SaysOk("失败")=false`（期望均应为 true）。红灯输出：`.temp/test-red-provider-relax.log`（失败 3 / 通过 5）。
- 判据本体即缺陷面（`AIProviderService.SaysOk`），单测层足以复现；上游链路无需真实模型。

## 3 根因（5 Whys + 第 6 问）

1. 为什么报失败？→ 成功判据 `SaysOk(reply)` 要求回复含「ok」（忽略大小写），该回复不含。
2. 为什么判据含 ok？→ 2026-10-18 设计「选定模型真实对话」探针时把「连接可用」与「服从指令格式」合并成一个判据。
3. 为什么会误判？→ 模型完全可以拒绝「仅回复 ok」的指令格式但仍是对话可用的模型；关键词匹配把这类正常回复归为失败。
4. 为什么当时没暴露？→ mock 上游/实测样例都回复「ok」，无「拒绝指令但正常回复」样本。
- **第 6 问：为什么流程没让我一开始就写对？**→ 当时没有「探针成功判据不得依赖回复内容」的不变量与守卫（本批补 PC-034）。

## 4 同类扫查命中表（2026-11-14）

| 位置（文件:行） | 同根因？ | 处置 |
|---|---|---|
| `ForgeSelf.Api/Services/AIProviderService.cs:221,295`（SaysOk 判据本体） | **是（本例）** | 换 `HasUsableReply`（非空白即成功）+ 成功消息截断 100 字 |
| `ForgeSelf.Api/Controllers/AIProviderController.cs:190,316`（判据描述注释 ×2） | 是（同判据的文档面） | 注释同步改为「回复非空白即成功，PC-034」 |
| `ForgeSelf.Api.Tests/Unit/AIProviderTestConnectionOkGuardTests.cs`（旧判据守卫） | 是 | 删除，改立 `AIProviderTestConnectionReplyGuardTests.cs` 钉新判据 |
| `Plugins/AIAgent/**`、其余插件 Services | 否 | 扫描签名 `仅回复|SaysOk|含 ok|IndexOf("ok")` 无判定逻辑命中（仅探针 prompt 文本与 IM 模板回复测试名，异根因） |

## 5 修复与被否方案

- `AIProviderService`：`SaysOk` → `HasUsableReply`（`!string.IsNullOrWhiteSpace`）；成功消息展示回复前 100 字（防长回复撑爆提示）；空回复失败分支不动（PC-013）。
- 接口/控制器 doc 注释、真源 `docs/02-features/001-ai-provider-config.md` §3 同步改语义。
- 被否：把探针 prompt 改成无指令文本（改动 e2e wire 断言且无必要——判据已不依赖内容）；对回复做「含中文/长度>阈值」等新启发式校验（又一次内容判据，同类隐患）。

## 6 重构提案与结论

R0：无需重构。判据函数为单点纯函数，G1 守卫已把不变量钉死；无跨模块契约面。

## 7 守卫落地

- **G1（新，本批）**：`AIProviderTestConnectionReplyGuardTests.cs::HasUsableReply_AnyNonBlankTextIsUsable`（9 例矩阵：任意非空白文本 ⇒ true，含本例拒绝语；空/纯空白/null ⇒ false）。
- e2e `ai-providers.spec.ts`（既有，断言兼容新判据，标题/注释同步）：mock 上游全链路仍绿语义。

## 8 逃逸分析

N/A 首见（用户直接上报，非守卫后复发）。lens 已入 PC-034：改判据先问「是否依赖回复内容」。

## 9 基建改进

- `docs/ai/pilot/2026-11-14-provider-test-relax/`（mini-task + 05/06/07）。
- 真源 001 §3 同节勘误：PILOT-074 后已不存在的 `ExtractContent` 描述改为 SDK 适配器现状。

## 10 复用赋能

任何「探活/测试连接/连通性判定」类功能：成功判据只允许「调用成功 + 非空产出」；要验证模型可用性，看「能否出话」而不是「是否听话」。

## 11 证据

| 结论 | 等级 | 出处 |
|---|---|---|
| 红灯 3 失败 / 5 通过（旧判据下拒绝语误判） | Verified | `.temp/test-red-provider-relax.log` |
| 绿灯 9/9（新判据矩阵）+ 构建 0 error | Verified | `.temp/test-green-provider-relax.log`、`.temp/build-provider-relax.log` |
| 判据本体与引用面（全仓仅服务+旧守卫+控制器注释） | Verified | grep `SaysOk|响应解析失败` 全仓 9 命中，逐条归入 §4 |
| 空回复失败分支保留（PC-013） | Verified | `AIProviderService.cs` 修改后代码 |
