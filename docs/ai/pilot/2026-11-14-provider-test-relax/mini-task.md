# mini-task：测试连接探针判据放宽（回复非空即成功，不校验 ok）

> 日期：2026-11-14 ｜ 任务 id：provider-test-relax ｜ 级别：轻量（≤3 业务文件）｜ 类型：缺陷（判据过严→假阴性）
> 适用问题类：**PC-034（新建，首见）**：探针成功判据绑定回复关键词 → 有效回复被误判为连接失败。
> 闸门1 说明：用户输入1 已直接指定方案与验收标准（「能正常返回结果即可，不严格校验返回内容」），本会话审批策略为不询问，按 §8.5 自主执行并全程留痕。

## 1 Intent（为什么 / 做什么 / 到什么程度）

- **现象**：设置页「测试连接（选定模型）」发送探针「请回复且仅回复ok」，模型正常出话但回复「我无法按要求只回复指定词语。」时被判定失败（`模型未回复 ok（实际回复：…）`）——连接与模型实际可用，属**假阴性**。
- **目标**：成功判据放宽为「上游调用成功 且 返回非空白回复」；回复内容不做任何关键词/语义校验。
- **边界（不变量，不许破）**：空回复仍判失败（PC-013 不变量「上游空响应当成功」不被本次放宽破坏）；探针 prompt、MaxTokens=128、URL 归一化、鉴权失败文案等一律不动。

## 2 Spec（要点 + 验收标准）

- `AIProviderService.TestConnectionAsync(id, modelId)`：`SaysOk(reply)` → `HasUsableReply(reply)`（= `!string.IsNullOrWhiteSpace`）。
- 成功消息：`连接成功 · 模型回复「{reply 截断至 100 字}」`；空回复仍为 `响应解析失败（未取到模型回复）`。
- 接口 doc 注释、守卫单测同步改语义；真源 `docs/02-features/001-ai-provider-config.md` §3 回写；e2e 仅改标题/注释中的过期表述（断言不动，`chatContent:'ok'` 在新判据下仍成功）。
- **验收**：① 红灯复现（旧判据下拒绝语被判 false）→ ② 绿灯（新判据矩阵全绿）→ ③ `dotnet build` + 全量 `dotnet test`（中档，改了宿主源码）新增红为 0 → ④ 真源/台账/索引回写齐。
- **Interaction Design**：本批无布局/动线/组件结构变化，仅「测试连接（选定模型）」结果反馈的判定与文案：点「测试连接 → 选定模型 → 确定」后——① 成功（上游调用成功且回复非空白）→ 卡片消息区显示成功态「连接成功 · 模型回复「{回复前 100 字，超出加 …}」」（判定面放宽，动线不变）；② 回复空白/取不到 → 失败态「响应解析失败（未取到模型回复）」；③ 鉴权失败 / 超时 / 不可达文案不变；④ 加载/空态/无权限态不受影响（后端判定变更，前端零改动）。验收标准：模型回复任意非空白文本（含拒绝「仅回复 ok」指令的说明）时，测试连接显示成功。

## 3 Plan（步骤）

1. 红灯：改 `AIProviderTestConnectionOkGuardTests` 矩阵为「拒绝语/无 ok 文本 ⇒ 应判可用」→ 跑 → 红（Verified）。
2. 绿灯：服务层换 `HasUsableReply` + 截断展示；测试文件改名 `AIProviderTestConnectionReplyGuardTests` 并按新语义钉死矩阵 → 跑 → 绿。
3. 同类扫查（P3）：按签名 `仅回复|SaysOk|含 ok|IndexOf("ok")` 扫全仓业务代码，命中表入 case。
4. 回写：真源 001 文档、PC-034 档案 + case + 三处索引、e2e 过期注释。
5. Verify：中档全量 dotnet test；汇总 05/06/07。

## 4 Task（Allowed / Forbidden）

- **Allowed**：`ForgeSelf.Api/Services/AIProviderService.cs`、`ForgeSelf.Api.Tests/Unit/AIProviderTestConnection*Tests.cs`、`ForgeSelf.Web/e2e/ai-providers.spec.ts`（仅注释/标题）、`docs/02-features/001-ai-provider-config.md`、`docs/11-troubleshooting/**`（PC-034/case/索引）、`docs/ai/pilot/2026-11-14-provider-test-relax/**`、TODO/日记。
- **Forbidden**：不动探针 prompt 与 MaxTokens；不动 PC-013 相关守卫与空响应失败分支；不提交 git（发布动作待用户同意）；不碰运行实例。

## 5 验证命令

- `dotnet build`（ForgeSelf.Api）；`dotnet test`（ForgeSelf.Api.Tests，全量=中档；环境前置 `$env:TEMP=$env:TMP=<repo>\.temp\tmp`，判据看日志正文不拿 exit code）。

## PC-034 记录（首见，非复发）

- 不变量：连通性探针（选定模型真实对话）成功判据只看「调用成功 + 非空白回复」，禁止对回复内容做关键词/语义校验；空回复仍判失败。
- 守卫：G1 = `ForgeSelf.Api.Tests/Unit/AIProviderTestConnectionReplyGuardTests.cs`（新矩阵钉死）。
- case：`docs/11-troubleshooting/cases/2026-11-14-provider-probe-ok-keyword-false-negative.md`。
