# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实；CHANGES_REQUIRED 时须指明回退到哪个阶段。
> Reviewer：本会话自审（同 Agent 双视角复查），对照 00~05 全链工件 + git diff 逐站点核对。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** 满足。Intent 的核心是「中文命令输出不再乱码，且不把 UTF-8 协议流掰坏」——AIAgent/ImGateway 等 8 站点 GBK/UTF-8 双相实测完好（05 现场验收节），UTF-8 阳性对照恒绿；FR-3 留痕日志非静默。
2. **实现是否符合 Spec？** 符合。FR-1 四级裁决（BOM→严格 UTF-8→ANSI+往返校验→宽容兜底）与标签集齐；FR-2 八站点全部读 BaseStream 字节；FR-3 两流式站点 + 工具站点均记 EncodingUsed；FR-4 G2 守卫建成且 MUTATION 实证可红。Spec Unknown 表三项全部现场消化（守卫放行 -EncodedCommand、三处读取形态逐文件核对、偏差已回写 03-plan）。
3. **是否超出了 Scope？** 未超。改动严格限于 03-plan 站点清单 + 归档/文档链；MUTATION 探针注入已彻底撤销（还原绿日志 + git diff 净变更为零）；一次性探针脚本用完即删。
4. **是否修改了不应该修改的文件？** 未发现。5 个 plugin.json bump 属 Spec Compatibility 明示范围；`docs/02-features/021-ai-agent.md` 与三处索引属问题类归档义务链；无无关文件被顺手动。
5. **测试是否覆盖 Acceptance Criteria？** AC1-AC6、AC8 均有 Verified 实据（05 AC 对照表）；AC7 如实标 Unknown（待拍板），未冒充通过。缺口守卫（G2 阳性对照 allowlistedHits>0）防扫描失效假绿。
6. **是否存在明显回归风险？** 低。工具 JSON 契约/截断/超时/守卫语义零改动（355 项插件过滤集 + 57 项编码/守卫专项全绿背书）；流式站点（CliTransport/WeComScanAuthService）改行缓冲后跨读边界行拼接由 ProcessOutputLineBuffer 保证且有单测。残余风险见 Risk 节。
7. **是否存在架构不一致？** 一致。解码裁决器落位 ForgeSelf.Core（与 EventBus 等同层先例），插件经 ForgeSelf.Abstractions/Core 引用，未新建跨插件私有接缝；守卫落位 ForgeSelf.Api.Tests/Architecture，与既有同类守卫 PluginToolConstructorDiGuardTests 同范式同目录。
8. **Evidence 是否足以证明任务完成？** 足以证明「本批（AC1-AC6/AC8）完成」；AC7 发布链未闭环，故整体任务状态 = **代码与归档完成、发布待用户拍板**，05 已如实分栏。

## Requirement Check

PASS（AC7 属闸门等待非需求失败，见 Findings Minor-3）

## Scope Check

PASS

## Test Check

PASS

## Architecture Check

PASS

## Risk

L1（公共解码链路改动影响 8 站点，但契约不变 + 双层守卫 + 全量绿测收敛；发布后若个别子进程码页极端混合，最坏损失为替换符且有 EncodingUsed 日志可查，不劣于修复前）

## Findings

### Critical

无。

### Major

无。

### Minor

1. **RunnerService 曾入 allowlist 后又撤销**（实施偏差，已回写 03-plan「实施偏差」节）：初判其"不消费文本"，核码后确认消费并迁移；教训：allowlist 准入必须先读码定性，不能凭站点清单脑补——PC-011 正文裁决表已留该记录。
2. **MUTATION 探针落盘纪律**：注入/还原各占一次编辑，靠"还原绿日志 + git diff 净零"双证据闭环；若只凭记忆撤销会有残留风险。建议后续 MUTATION 固化为守卫测试内置 self-test（ScriptExecutor 迁移批一并做，已含在 TODO 挂账精神内）。
3. **AC7 未闭环**：发布与 :18890 运行实例只读复验待用户拍板后按 plugin-development §四 六步执行（含插件冒烟 e2e 与本地目录更新源发布）；在此之前插件版本 bump 只是待发布声明，不构成已交付。
4. 前序会话工具坑（Grep 在本 workspace 不可靠、`Stream.ReadAllBytesAsync` 不存在、内联 pwsh 吞 `$`）已沉淀为会话经验并影响实现写法（助手内置 Capture/CaptureAsync），无需额外动作。

## Final Decision

APPROVED（范围 = AC1-AC6 + AC8；AC7 发布链独立待用户拍板，不阻塞本批合入建议）
