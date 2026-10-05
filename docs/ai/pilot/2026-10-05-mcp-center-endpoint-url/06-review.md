# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。结论只报事实。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** 是。界面三处（chip / 「MCP 服务地址」卡 / 复制按钮）给出的都是 `listenUrl + /mcp`；真实绑定地址仍在运行状态卡（`监听 host:port`）可见；后端 `ListenUrl` 未被附加路径（`git diff` 证明 `Services/` 零改动）。用户 2026-09-27 的原诉求「增加地址，不写路径会让人误会」在事实层面被消除：e2e 实测根地址 404 / 端点 200。
2. **实现是否符合 Spec？** 符合 FR1–FR6 与 BR1–BR6（单一派生点、绑定串不动、与设计系统插件 `mcpEndpoint()` 同构、空值不编造、不涉令牌、版本同串）。边界条件覆盖：尾斜杠、空白、空串、`0.0.0.0`、超长省略号（沿用既有 CSS）。`Unknown U1`（剪贴板读回）按 Spec 约定降级并写入 Known Limitations；`U2`（沙箱内插件 web 构建）未命中，`pnpm run build` 直接成功。
3. **是否超出了 Scope？** 未超出。`git diff --stat` 实读 5 files changed / 74 insertions / 16 deletions，与 04-task Allowed 逐条对齐：视图、e2e、plugin.json、csproj、034 文档。
4. **是否修改了不应该修改的文件？** 没有。未触后端 `Services/Models/Controllers/McpCenterPlugin.cs`、未触宿主 `ForgeSelf.Api/**` 与 `ForgeSelf.Web/src/**`、未触共享 e2e 基建（`global-setup.ts`/`fixtures/**`/`playwright.*.config.ts`）、未触其它插件、未引入依赖、未动鉴权与数据。
5. **测试是否覆盖 Acceptance Criteria？** AC1–AC7 全部有实测输出（见 05-evidence 表格）；AC8（变更集合）由 `git diff --stat` + `git status` 覆盖。判据强度较改前**提升**：原断言是 chip `toContainText('127.0.0.1')`（裸地址也能过），新断言是「等于 `listenUrl + '/mcp'`」+ 反向腿 404，属加严而非放宽。
6. **是否存在明显回归风险？** 低。① 同 spec 内的 MCP 协议全链路与外部三传输（stdio / streamable-http / http-sse）在本次运行中同批通过，未被我改的展示逻辑影响；② 全仓检索确认无其它代码依赖「界面/文档展示裸 `listenUrl`」这一字符串（设计系统插件与 `sems` e2e 各自用 `listenUrl` 自行拼 `/mcp`，不受影响）；③ 后端契约与绑定行为未变。
7. **是否存在架构不一致？** 无，反而**消除**了一处不一致：同一事实（`listenUrl` → 端点）在仓库里原本有两套表现（设计系统插件已带 `/mcp`、MCP 中心不带），现同构为「前端派生、去尾斜杠 + `/mcp`」，并在 `034` 文档写死「后端 `ListenUrl` = 绑定串、禁止加路径」的口径，防止后来者误改后端。
8. **Evidence 是否足以证明任务完成？** 足以，且把**环境性失败与代码性结果严格分开**：两次 webServer 超时以 `DEBUG=pw:webserver` 的 502 证据链归因到 `HTTP_PROXY`/缺 `NO_PROXY`；后端 22 红以 Access denied 栈归因到 `%TEMP%` 写入受限，并在重定向 TEMP 后复跑得 96/96。两处均**未**用"无关"二字带过。

## Requirement Check

PASS

## Scope Check

PASS

## Test Check

PASS（含 1 项按 Spec 约定降级的断言，已登记 Known Limitations，不计为未完成）

## Architecture Check

PASS

## Risk

**L1**（影响面＝单个插件的展示层；无 DB / 无契约 / 无鉴权 / 无宿主；回滚＝`git checkout` 5 个文件 + 重建 dist）

## Findings

### Critical

无。

### Major

无。

### Minor

1. 卡片说明行「客户端须使用 /mcp 路径（根路径 404）」在 1280px 宽下折成两行（`…（根路径` / `404）`）；无截断无溢出，属排版细节。
2. 剪贴板内容未做自动化读回（`clipboard-read` 权限）；已有事实是复制函数与展示共用同一 computed。
3. 本机两条环境坑（`HTTP_PROXY` 缺 `NO_PROXY` 致 e2e webServer 恒 502；`%TEMP%` 拒建目录致测试整片假红）**未固化为仓库侧守卫**，本批按范围控制只做运行时绕过，已登记 TODO 交用户决定是否做成规范化入口。
4. `05-evidence.md` 中一处笔误「⇒ 503/502」，应为「⇒ 502」（本文档提交前已修正）。

## Final Decision

**APPROVED**

- 代码与判据满足 Intent/Spec/Task，Evidence 充分且来源等级分明。
- 用户动作进展（2026-10-05 更新，**不属本批代码缺口**）：① 闸门2 验收进行中（用户已下达「提交且仅提交本次修改」）；② 发布已执行 —— 用户指定「本地发布 + 完整打包到 `D:\src\my-proj\OpenForgeSelf\updates`」，本地离线整包 `2.7.3.2610051746` 已出包（签名 `Valid`、SHA256 MATCH、L1/L2/L3 布局不变量通过，见 05-evidence「发布证据」）；③ 用户已完成实例升级 ⇒「运行实例只读复验」已执行（token 200 / 插件 `2.2.1` / 18 插件 / 伺服前端产物与仓库产物**逐字节一致**，见 05-evidence 同名节）。仍未做：`git push` / 打 tag（等用户指示）。
- 与并行分支 `wt-mcp-playground`（MCP 中心 2.3.0）合并时**版本号取高者**并复跑插件门禁。
