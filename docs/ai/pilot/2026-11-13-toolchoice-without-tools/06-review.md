# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实；CHANGES_REQUIRED 时须指明回退到哪个阶段。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** 是。缺陷 = 出站 `tool_choice` 在无 `tools` 时未丢弃 → 上游 400；修复在全部写点统一落地守卫语义（bridge 两向 + ChatClient + FromUnified + pipeline 咽喉点共 5 处，OpenAISdkAdapter 既有守卫不重复动），用户 live 400 的实际路径（同协议直通）由咽喉点守卫覆盖——T1/T2 复现该路径即证。
2. **实现是否符合 Spec？** 是。守卫语义「源 `tools` 缺失或 count==0 ⇒ 丢弃 `tool_choice`；非空 ⇒ auto/required/function 三形态原样保留」逐条落地；两方向对称（Chat→Responses / Responses→Chat）；不新增 `ChatToResponsesDropped` 条目；Unknown 项（上游对 `tools:[]` 无 tool_choice 的接受度）按 Spec 不动 `MapToolsTo*` presence-only 语义。
3. **是否超出了 Scope？** 否。改动全部在 mini-task Allowed 清单内：4 源文件 + 2 测试文件 + 本目录工件 + `docs/11-troubleshooting/` 归档 + 日记/TODO。
4. **是否修改了不应该修改的文件？** 否。未动 `OpenAISdkAdapter`（已有守卫）、`ChatToResponsesDropped`、csproj 版本行；偏差文件 `LlmProxyPipeline.cs` 属同根因同类修复，已按规范「偏差先记录再修正」在 mini-task Plan.4 登记后才落码。
5. **测试是否覆盖 Acceptance Criteria？** 是。TDD 红→绿：红 run2 恰 6 红（pipeline 三路径 T1–T4 + OpenAIChatJson 2 例）→ 快档绿 18/18；阳性 T4/T6/`WithTools_IsKept` 守 G2 不误伤；中档全量 3775 例对表 0 新增红（05 §4.2 逐族）。
6. **是否存在明显回归风险？** 低。守卫仅在「`tool_choice` 非空且 `tools` 缺失/空」时删从属字段，有 tools 路径与直通/桥接两路阳性全绿。残余风险 = Unknown（`tools:[]` 接受度），已登记 PC-033 盲区清单。
7. **是否存在架构不一致？** 否。守卫落点与既有分层一致：协议映射层（bridge，直接调用旁路）+ 出站汇合点（pipeline `LlmJson.Write` 前，直通/桥接必经）双保险；形态与既有 `OpenAISdkAdapter` 出站守卫同族（纵深防御，不冲突）。
8. **Evidence 是否足以证明任务完成？** 是。05 §2 红灯 6/6 逐条断言消息 + §4.1 快档绿 18/18（99ms）+ §4.2 全量 3775 例基线对表 0 新增红，全部 Verified（日志路径 + 后台 job id 在案）。

## Requirement Check

PASS — 上游 400 根因消除：守卫语义全写点落地，live 直通路径有专项复现测试（T1）。

## Scope Check

PASS — 改动面与 Allowed 清单一致，Forbidden 项未触碰。

## Test Check

PASS — 快档红→绿（6 红 → 18/18）+ 中档全量 0 新增红（25 红全部逐族命中存量基线登记）。

## Architecture Check

PASS — 写点/咽喉点双保险与既有 `OpenAISdkAdapter` 守卫形态一致，无新抽象层引入。

## Risk

L1（低）：守卫为删字段式最小干预；阳性路径全绿；Unknown 项已入 PC-033 盲区清单（不阻塞本批）。

## Findings

### Critical

（无）

### Major

（无）

### Minor

1. 首轮同类扫查只列「协议转换写点」，漏「body 原样转发」的同协议直通路径（首轮绿跑 T1/T2 暴露后补第 5 处守卫）——已按 problem-class-closure 做逃逸分析并归档 PC-033（扫查清单条款：必须含数据流汇合点）。
2. mini-task 验证节最初引用的基线红清单（2 条）与实际全量基线（分散于 TODO 多行）不同步——05 §4.2 已按 TODO L548 等登记逐族对表修正（记录性，不再动作）。

## Final Decision

APPROVED

（依据：四 Check 全 PASS + 中档全量 0 新增红 + 缺陷类闭环三节齐备（07 §10–12）。闸门3 提交经用户「补全提交」指令授权；仅本地提交、不推送。）
