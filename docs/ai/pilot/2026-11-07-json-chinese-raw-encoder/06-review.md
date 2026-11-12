# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** 满足。用户诉求「中文不转义成 `\uXXXX`，直接保持原文」：G1 行为锁证明 `ForgeJson.Default`/`Indented` 输出含「目录上溯」原文且不含 `\u`、命名策略保持 null（零漂移）；G2 源码扫描使全仓写出点 488 处违例 → 0。落盘 / HTTP 响应 / DB JSON 列共用同一 Encoder，各面一致。
2. **实现是否符合 Spec？** 符合。02-spec AC1–AC6 全部 ✅ Verified（05 对照表）；缩进仅按 Spec 声明的两处落盘面切 `ForgeJson.Indented`（UpdateSettingsService / PluginUpdateSettingsService），其余写出面零缩进/命名变更。
3. **是否超出了 Scope？** 两处越界，均为 §0 例外条款（同根因同类扫查）且已记录：① `PluginScaffolderService` 模板内生成代码同步改 ForgeJson（守卫盲区同类，防未来插件复现）；② `mock-mcp-server.js` 夹具修复（本批行为变更「中文原文输出」暴露的伴生缺陷，中文载荷 LSP 帧解析，详见 05 实验 4）。未动并行会话文件（DbAdmin 仅 Encoder 一行，属本批扫点职责内）；未动 csproj 版本行；未动 TODO.md 并行会话条目。
4. **是否修改了不应该修改的文件？** 无。
5. **测试是否覆盖 Acceptance Criteria？** 覆盖。AC1/AC2 = G1×4 + G2×1 守卫常驻（RED 488 → GREEN 0，阳性对照 `serializeCount > 50` / `files.Count > 150` 防假绿）；AC3 build 0 错误；AC4 全量对表 29 红九组归因闭环（HEAD 对照实验 22 红逐个同红实证存量 + 空闲复跑 + 干净重建 + 夹具修复复跑 2/2 绿），**修复后本批净新增红 = 0**；AC6 台账机器校验 27 PC 全过。
6. **是否存在明显回归风险？** L1（见 Risk 节）。Encoder 换型只影响写出面转义行为，反序列化不受影响（Encoder 被忽略）；字节级帧解析、HTML 敏感字符面已在 case §6 约束（消费方均为 API/文件/DB）。
7. **是否存在架构不一致？** 无。`ForgeJson` 落 `ForgeSelf.Abstractions` 共享层（宿主与插件均引用，与既有分层一致）；统一不变量「所有生产 `JsonSerializerOptions`/`JsonWriterOptions` 初始化器一律声明 Encoder（含只读初始化器）」以守卫固化，零豁免。
8. **Evidence 是否足以证明任务完成？** 足够。05 全节 Verified、日志逐条摘录可对账（`.temp/*.log`）；29 红归因有四重实验背书（HEAD worktree 对照 / 空闲复跑 / 干净重建 / 夹具修复验证），无 Unknown 级结论充当完成依据。

## Requirement Check

PASS（用户原始诉求逐字满足：中文原文、不转义；范围=本项目全部 JSON 序列化点，G2 扫描机器背书）

## Scope Check

PASS（两处越界均属 §0 同根因例外且留痕；并行会话文件零侵占）

## Test Check

PASS（守卫 RED→GREEN + 全量对表归因闭环 + 夹具修复复跑绿；净新增红 = 0）

## Architecture Check

PASS（ForgeJson 共享层 + 统一不变量守卫常驻，无新依赖、无分层破坏）

## Risk

L1（低）：机械跨切面写出面配置变更，机器裁判双绿（G2 守卫 + 全量对表）。残余风险三条，均已记 TODO 观察项：① G2 已知盲区（原始字符串模板内生成代码 / 插值孔 / 冷门重载）出现真实逃逸再升级实例级扫描；② 发布后 :51888 运行实例中文显示抽查未做（属升级实例动作）；③ 并行会话活跃期跑全量测试，编译快照可能取到瞬时中间态（本批 Maf 假红实证）。

## Findings

### Critical

无。

### Major

无。

### Minor

1. 05 归因表初稿组计数（ProjectWorkspace 17 / WorkflowPlanning 7）与 `full-test1.log` 逐行统计（15 / 6）不符，回填时以日志为准纠正——过程发现，终稿已一致。
2. 归因复跑期间误杀 testhost 92832（当时正在跑的 Mcp/CancelAsync 复跑 job 的宿主）致该实验作废重跑；教训已沉淀：空闲期残留 testhost 可能属并行会话，杀前必须先查 job 归属。
3. 「复跑绿即 flake」的判定在本批 Mcp×2 上先错后纠：第一轮空闲复跑被误杀中止、定性被挂起；第二轮空闲复跑仍红（失败点精确落在中文载荷 `tools/call`，同会话 ASCII 请求全过）才暴露夹具真缺陷。机制修正：flake 判定必须附「失败点一致性」复核，不能只看红/绿翻转。
4. 编译快照瞬时中间态（Maf 假红）：全量测试与并行会话编辑并发时，构建可能取到半写状态；机制修正：定性类复跑前先干净重建（`--no-build` 复跑只复现旧快照，不构成新证据）。

## Final Decision

APPROVED
