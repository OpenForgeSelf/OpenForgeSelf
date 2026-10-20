# 06-review：MCP 中心·外部 MCP 三缺陷复现批（2026-10-17）

> Reviewer 九问（规范 Stage 8；本批为轻量复现批，第 9 问按 problem-class-closure 核）。

## 逐问
1. **实现是否满足 Intent？** PASS——Intent=只复现不修复；三现象全部拿到证据：①②=运行实例 400 原文 + A/B 直连对照 + xUnit 红灯；③=API 响应体（url 在发）+ 前端行号（硬编码空值）。
2. **是否符合 Spec？** PASS——AC1 编译通过（Verified）；AC2 测试 FAIL 且消息含 clientInfo（Verified）；AC3 :51888 /test 400 原文逐字入档（Verified）。
3. **是否超出 Scope？** PASS——业务代码 0 改动；仅新增 1 测试文件 + 3 工件；运行实例只调了 GET 与无副作用的 /test（端点文档明示「不建会话、不保存」）；探索期 curl 探针用毕即删、未进版本控制（AGENTS 红线遵守）。
4. **是否改了不该改的文件？** PASS——无。
5. **测试是否覆盖 Acceptance Criteria？** PASS——V4 红灯即 AC2；AC1/AC3 由命令输出覆盖。
6. **明显回归风险？** 无——新增测试当前为**预期红灯**，会被全量 `dotnet test` 计入失败；这是复现批的交付形态（同 2026-10-17-gateway-list-todos-scope-repro 先例），修复批转绿前该 Filter 命中 1 红是**有意的锚点**，不是基线污染。若 CI 全量跑测因此红，处理方式=修复批立即跟进（本批不 skip、不加 trait，保持可见）。
7. **架构不一致？** 无——测试放 ForgeSelf.Api.Tests/Plugins/McpCenterTests（既有归口），CapturingHandler 模式与仓内无先例但零依赖新增，成本最低层（规范 P1「最低成本层红灯」）。
8. **Evidence 是否足以证明任务完成？** PASS——Verified/Inferred 分级标注，无「应该可以」。
9. **同类扫查与守卫？** 部分——扫查命中表已出（05 §P3：三传输同缺 clientInfo + initialized/Session-Id 次级缺口 + 掩码往返衍生缺陷）；守卫/case+PC 归档**不在复现批交付面**（候选新 PC 已登记 mini-task「适用问题类」节，随修复批按出口清单落地）。

## Findings
- **Critical**：无（本批不改业务代码）。
- **Major（供修复批，均已被证据钉死）**：
  - M1 三传输 initialize 同缺 `clientInfo`（同根因面 3 处）——canonical fix 应一处定义、三处引用。
  - M2 掩码视图当编辑真值往返：编辑保存会把 `****尾4` 写回 Headers/Env 覆盖真实密钥（**数据破坏型**，修复批优先级高于表面回显）。
  - M3 对有状态外部服务器缺 `initialized` 通知 + `Mcp-Session-Id` 回传（deepwiki 不阻，换一个标准 server 必阻——「测试也失败」类问题的另一半版图）。
- **Minor**：①②在 UI 上的呈现只有 toast 文案、列表 `lastError` 对 /connect 手点路径不落（`_errors` 只由启动期后台建连写入），排障可见性差——修复批顺带裁决。

## Risk
**L1**（复现批本身零业务改动；风险全部在修复批：动插件传输层三处 + 前端表单，须走 plugin-development 六步闭环）。

## Final Decision
**APPROVED**（复现批目标达成；修复批方案六段已呈用户，待闸门1）。
