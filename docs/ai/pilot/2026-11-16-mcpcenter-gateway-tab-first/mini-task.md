# mini-task：McpCenter「网关配置」调为第一个页签并默认激活

- **日期**：2026-11-16 ｜ **执行**：lead ｜ **形态**：轻量（mini-task，≤3 文件档）
- **闸门1 依据**：用户 2026-11-16 输入1 直接指令「我不是说了吗，mcp插件，网关配置放第一个页签，避免每次都要切换」（原文见 `.forgeself/memory/2026-11-16.md` 输入1）；本会话审批策略已改「从不询问」＝自主执行，用户直接指令即闸门批准。
- **状态**：🔄 进行中

## ① 意图（为什么）

`/mcp-center` 打开后默认落在「工具管理」页签；用户常用的是「网关配置」（地址/令牌/跨域/会话模式/执行超时/写入 dsh 配置/外部 MCP 服务器管理），每次进页面都要先点一次页签切换。用户明确要求把「网关配置」放第一个页签，消除这次切换。

## ② 规格要求（做什么 / 到什么程度）

1. 页签顺序：网关配置在前、工具管理在后（页签头顺序 = DOM 顺序）。
2. 默认激活页签 = 网关配置（`activeTab` 初值 `'gateway'`）。
3. 「网关配置」页签内全部既有功能零变化（状态卡 / 修改表单 / 写入 dsh / 外部服务器管理）。
4. dsh 写入状态加载时机随默认页签调整：watch 加 `immediate`，挂载即拉（原「首次切到网关配置才拉」在网关配置成为默认页签后不会触发）。
5. e2e 同步：UI 渲染用例改为断言「第一个页签 = 网关配置 + 默认激活 + 挂载即见状态卡」，工具表格断言移到「切到工具管理」之后；先红后绿。
6. 插件版本 `2.9.0` → `2.9.1`（UI 行为变化，发布可辨识）。

## ③ 计划（涉及文件）

- `Plugins/McpCenter/web/src/McpCenterView.vue`：activeTab 初值 + 模板页签块换位 + watch immediate。
- `Plugins/McpCenter/plugin.json`：Version 2.9.1 + Description 追加 v2.9.1 一句。
- `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`：UI 渲染用例断言调整（其余用例点「网关配置」页签的动作保留——点已激活页签应幂等无害）。
- 工件：本 pilot 4 件（mini-task / 05 / 06 / 07）+ 日记 + TODO。
- **不改**：后端 C#（零触碰，dotnet 门禁不触发）、页签内任何功能、`mcp-center.live.spec.ts`（运行实例复验阶段再看）。

## Interaction Design（交互设计）

- **点什么出现什么**：打开 `/mcp-center` → 页签栏从左到右为「网关配置」「工具管理」，默认高亮「网关配置」，主区**无需点击**直接显示网关状态卡（MCP 服务地址/运行状态/访问令牌）+ 修改网关配置表单 + 写入 dsh 配置 + 外部 MCP 服务器区块；点「工具管理」→ 切到服务器侧栏联动 + 工具表格。
- **不变**：页签内所有控件行为、保存并热重启链路、dsh 写入 confirm 弹窗、外部服务器 CRUD、二次确认语义全部保持原交互；页签切换仍为 ElTabs 原生行为。
- **五态**：加载（状态卡 v-loading / 表格「加载中...」）、空态（「暂无工具数据」「尚未配置外部服务器」）、出错（表格错误条 + 重试 / ElMessage 错误提示）、正常态——全部沿用既有实现，本批只动页签次序与默认值，不触碰任何态的内部结构。
- **验收标准**：
  - AC1 页签头顺序 = 「网关配置」「工具管理」（e2e 断言 tab 列表首项文本）。
  - AC2 默认激活 = 网关配置：挂载后 `.el-tabs__item.is-active` 为「网关配置」且 `.gateway-status-cards` 可见（e2e）。
  - AC3 切「工具管理」后表头 + 工具行可见（e2e）。
  - AC4 既有 mcp-center e2e 全绿（默认页签变化不破坏外部服务器/测试台/UI 主链路用例）。
  - AC5 插件 web 门禁绿（check/test/build）+ 仓库根 `design:check` 无新增违例。

## ④ 任务拆分

- [x] 日记首行 + TODO 登记（来源:输入1）
- [x] 读 plugin-development 技能 + 定位页签实现与 e2e 断言面
- [ ] mini-task.md（本件）
- [ ] e2e 断言先改（顺序 + 默认激活 + 工具表格后置）→ 红跑一次（未改实现前应红）
- [ ] 实现：activeTab 初值 / 模板页签块换位 / watch immediate
- [ ] plugin.json → 2.9.1
- [ ] 门禁：插件 web check/test/build + design:check + mcp-center e2e（先 UI 渲染用例后全套冒烟）
- [ ] 走查截图读图（e2e 截图 + dev 走查）
- [ ] 发布（release-local -UpdateDir -Sign；运行实例复验留待用户更新后）
- [ ] 05-evidence / 06-review / 07-final-report + 034 文档 + 日记收尾 + TODO 移除

## ⑤ 验证（命令 + 判据）

- `cd Plugins/McpCenter/web && pnpm run check && pnpm run test && pnpm run build` 全绿（日志正文判定，不认 exit code）。
- 仓库根 `pnpm run design:check` 无新增违例。
- `ForgeSelf.Web` 内 `pnpm exec playwright test e2e/plugins/mcp-center --grep 插件自带界面`（红）→ 实现后同命令（绿）→ 全插件 spec 全绿。
- 后端零改动 ⇒ `dotnet build/test` 不触发；改动面以 `git status` 证明仅 web/plugin.json/e2e/工件。

## Failure Modes（预判失效模式与对策）

| # | 失效模式 | 对策 |
|---|----------|------|
| F1 | 220+ 行页签块模板移动出错（内容错位/丢块） | 移动后 e2e 逐一断言状态卡/表单/外部区块/工具表格齐全 + 截图读图 |
| F2 | dsh 写入状态因默认页签变化永不加载（原 watch 只在切换时触发） | watch 加 `immediate: true`，挂载即拉；走查核「写入 dsh 配置」区块状态文案 |
| F3 | 既有用例「点已激活页签」行为异常 | ElTabs 点当前页签为幂等操作；AC4 全套 spec 实跑验证 |
| F4 | 忘 bump 插件版本 → 用户无法辨识新包已生效 | plugin.json → 2.9.1；e2e 版本徽标断言读清单值自动跟随 |
