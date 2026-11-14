---
id: PC-034
title: 测试连接探针判据绑定回复关键词 → 有效回复误判失败（假阴性）
tags: [ai-provider, test-connection, probe, false-negative]
invariant: 连通性探针（选定模型真实对话）成功判据只看「上游调用成功 + 返回非空白回复」，禁止对回复内容做关键词/语义校验（模型可拒绝「仅回复 ok」类指令但仍完全可用）；空回复（含纯空白）仍必须判失败，不得当成功（PC-013 不变量不受本条放宽影响）。
status: guarded
first_seen: 2026-11-14
recurrences: []
cases: [2026-11-14-provider-probe-ok-keyword-false-negative]
signature:
  - {id: S1, kind: test, path: ForgeSelf.Api.Tests/Unit/AIProviderTestConnectionReplyGuardTests.cs}
allowlist: []
guards:
  - {level: G1, path: ForgeSelf.Api.Tests/Unit/AIProviderTestConnectionReplyGuardTests.cs, status: active}
lens:
  - 新增/修改任何「成功判据」时：是否依赖回复内容（关键词/子串/语义匹配）？只允许「调用成功 + 非空白」。
  - 放宽判据时是否连带破坏「空回复判失败」（PC-013）？两条不变量必须同时成立。
  - 成功消息是否对长回复截断展示（防止超长回复撑爆 UI 提示）？
---
# PC-034 测试连接探针判据绑定回复关键词（假阴性）

## 1 不变量与诱因

不变量（两条同时成立）：

1. **内容不校验**：探针成功判据 = HTTP 调用成功 + 回复非空白。模型收到「请回复且仅回复ok」后回复任何正常文本（含拒绝指令的说明）都应判连接成功。
2. **空响应仍判失败**：空字符串 / 纯空白 / 取不到 content ⇒ 失败「响应解析失败（未取到模型回复）」（PC-013 不变量，不随本条放宽）。

诱因：2026-10-18 设计「选定模型 · 真实对话」探针时把成功判据定为「响应文本含 ok（忽略大小写）」——把「模型是否可对话」与「模型是否服从指令格式」混为一个判据。2026-11-14 实测模型回复「我无法按要求只回复指定词语。」被判 `模型未回复 ok`，连接与模型实际可用，属假阴性。

## 2 特征签名与扫描方法

签名：探针/判定代码中出现「回复内容关键词匹配」（如 `SaysOk`、`IndexOf("ok")`、`Contains("ok")` 作为成功前提）。扫描：`grep -n "SaysOk\|仅回复|含 ok" ForgeSelf.Api Plugins/*/Services` —— 只允许探针 prompt 文本本身（发给上游的 content）出现「仅回复 ok」，判定逻辑不得出现。

## 3 Canonical fix

- `AIProviderService.HasUsableReply(content)` = `!string.IsNullOrWhiteSpace(content)`，替代 `SaysOk`；成功消息 `连接成功 · 模型回复「{前 100 字}」`。
- 空回复失败分支保留原样（PC-013）。
- 探针 prompt「请回复且仅回复ok」保留（内容不影响判定；e2e 断言其 wire 形态）。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `AIProviderTestConnectionReplyGuardTests.cs::HasUsableReply_AnyNonBlankTextIsUsable`（拒绝语/无 ok 文本 ⇒ true；空/空白/null ⇒ false） | dotnet test 全量 / CI | 只钉判据函数本身；端到端 mock 上游的 e2e 用例以「回复非空判成功」继续覆盖链路 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-11-14-provider-probe-ok-keyword-false-negative | 2026-11-14 | 测试连接（选定模型）对回复「我无法按要求只回复指定词语。」报失败，实际连接与模型可用 | 否（首见，用户直接上报） |
