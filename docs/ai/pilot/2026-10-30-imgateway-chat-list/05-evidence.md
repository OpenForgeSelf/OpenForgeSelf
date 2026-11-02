# Evidence（验证证据 · PILOT-078）

> 分级：**Verified**（亲自跑过拿真实输出）/ Inferred / Unknown。取证日志在 `.temp/tmp/`，截图在 `ForgeSelf.Web/screenshots/`。

## E1 实现（im-dev，门禁双绿 · Verified）

| 项 | 结果 | 证据 |
|---|---|---|
| 后端构建 | `dotnet build` 0 error exit 0 | `.temp/tmp/im-dev-build-backend-final.log` |
| 插件前端构建 | `pnpm run build` exit 0（12 modules，dist/index.js 24.14 kB） | `.temp/tmp/im-dev-build-web.log` |
| 产物自检 | vue 裸导入外部化 ✓；`@/`、element-plus、vue-router 0 命中 ✓；`<ElXxx>` 零实用 ✓；新文件裸色值 0 ✓；滚动子区块 flex-shrink:0 ✓ | dev 汇报 + 抽查 |
| 写范围纪律 | git status 全部落在 Allowed 清单；`Plugins/AIAgent/**`（404 批未提交）零触碰 | lead 抽查 git status |
| Model.xml→xcode | 生成幂等，git diff 仅新增预期（实体成对 + ImGateway.htm 附产物） | dev 汇报 + git status |
| 三落库点 diff 抽查 | Router ①映射后 in ②AI 失败 out/error(ErrorText 截 1000) ③回发成功 out/ok；构造注入 + DI 注册 | lead 亲读 `git diff ImGatewayRouter.cs` |
| 鉴权 diff 抽查 | 控制器类级 `[Authorize("ApiKeyPolicy")]` + conversations/messages 端点契约逐字段对 spec | lead 亲读 `git diff ImGatewayController.cs` |

## E2 单测（im-test，18/18 绿 · Verified）

- 门禁：`dotnet test --filter "FullyQualifiedName~ImGatewayTests"` → 失败 0 / 通过 18（trx `.temp/tmp/im-test-imgw-unitest.trx`，终局日志 `.temp/tmp/im-test-unitests-run4.log`）。
- `ImGatewayMessageStoreTests`（6）：FR-1 字段、ErrorText 截 1000、null 安全、吞异常双路径（空 ChannelKey / DROP 表 + 恢复阳性对照）。
- `ImGatewayRouterRecordTests`（4）：scripted IChatCompletion 成功链 in+out(ok)、失败链 out(error,摘要) 不回发不抛、落库失败不影响主链、事件/空内容不入表。
- `ImGatewayControllerQueryTests`（8）：conversations 排序/displayName/计数/preview 60/零消息 null/camelCase 硬断言；messages 升序/limit 钳 1..200（205 行实测）/beforeId 游标/越界/channelKey 缺失 400；FR-4 反射式类级鉴权（口径=lead 批准，活体归 e2e）。
- 过程 3 轮红→绿均为测试侧自身问题（JsonResult 分支 / StatusCodes using / FluentAssertions params 陷阱），非被测缺陷。
- PC-014 合规：XCodeTestFixture + `[Collection("XCode")]`，零 AddConnStr 手搓。

## E3 e2e 冒烟（im-test，2 passed · Verified）

- `pnpm run test:e2e -- e2e/plugins/im-gateway/im-gateway.spec.ts` → **2 passed (57.7s) EXIT=0**（`.temp/tmp/im-test-e2e-run2.log`）。
- 新增「会话视图主链路」：引导空态文案、徽标 v2.1.0、配置切换往返、带 token conversations 200 + camelCase、**无 token 401（AC-7 活体实测）**、控制台零致命报错、`/api/im-gateway/*` 零 5xx。API 探针 `{"withTokenStatus":200,"noTokenStatus":401,"isArray":true,"camelCase":true}`。
- 过程 2 处红→绿：①既有 spec 读 plugin.json 版本键用了小写 `version`（潜在笔误，兼容读取修正）②既有用例因默认视图变更为会话视图，按 AC-5 语义适配为「先切配置视图再断言表单」（语义保留非弱化）。
- 截图：`ForgeSelf.Web/screenshots/e2e/im-gateway/im-gateway-chat-empty.png`（双栏+空态+徽标+状态灯）、`im-gateway-chat-config.png`（配置视图完整）——lead 读图核验通过。

## E4 起源码走查（lead，dev-stack 重建实例 · Verified）

- 环境：`dev-stack.ps1` 重建（后端 :7301 PID 116768 / 前端 :7399 PID 80588，含本批代码）；dev `ImGateway.db` 种子 3 联系人（zhang3/li4/wang5）+ 7 条消息（in/out ok ×5、out error ×1、单 in ×1）后走查，**走查后已清种子数据**（两表归零复核）。
- **AC-1 ✓**：默认会话视图；列表 3 条目（头像字母/显示名/预览/相对时间）；顶部「未连接」状态灯；右栏未选中态「从左侧选择一个联系人查看消息」。
- **AC-2 ✓**：排序 = li4(-4min) → zhang3(-5min) → wang5(零消息排后)（`conversations` 实测 JSON 逐字段符合 FR-2）；点 zhang3 渲染 6 气泡升序。
- **AC-3 ✓**：out/error 气泡金色 meta「回复失败」+ 红色 ErrorText「AI 补全失败：Service request failed. Status: 404 (Not Found)」（截图 `walkthrough-thread-zhang3.png`）。
- **AC-4 △**：加载更多按钮逻辑由单测覆盖（beforeId 游标 + limit 钳制 + hasMore，205 行实测）；dev 种子仅 6 条 < limit 50 无法现场触发——单测为证，属覆盖充分（Unknown=真机超 50 条形态，留运行实例复验）。
- **AC-5 ✓**：配置视图往返；通道卡/扫码授权/绑定 Agent/ChatModel/状态灯/保存 footer 全在（截图 `walkthrough-config-view.png`）。
- **AC-6 ✓**：走查页面驻留 ~2 分钟跨多轮 12s 轮询，DOM ref（列表/条目）跨快照保持稳定 → 比对后赋值生效，无重建闪烁（Playwright ref 不变 = 节点未重建）。
- **AC-7 ✓**：e2e 活体 401/200（E3）。
- 走查种子踩坑（已入 README）：sqlite3 CLI 参数传中文按控制台码页入库成乱码 → 改 UTF-8 SQL 文件 `.read` 重种解决（首次种子作废重做，API 复验中文正常）。
- 控制台：全程 0 error（3 warning 为宿主既有资源提示）。

## E5 视觉观察（~~非阻塞~~ → **已被 E7 实测推翻并修复**）

- ~~暗色主题下对比度偏低 3 处（Warning 级）~~ **修订（2026-10-31 输入N+3）**：走查当时仅目测未按 D-1 实测，判级错误。用户以截图打回「太丑了，符合规范吗」后实测：标题 vs 页面背景 **1.02:1**、副标题 1.21:1、时间戳 ~1:1（要求 4.5/3:1）→ **P0 不可见**，不符合规范。根因：插件根视图无主题底（页头深字直接压深色背景图）+ 面板 40% 玻璃压深图；先例对照：宿主 /ai-agent 内容页同主题态用不透明主题底 rgb(242,243,245)（活体实测）。→ 修复见 E7。
- dev 实例灰膜感 = 宿主主题渐变叠加的成分之一，但**主因是插件自身无主题底**（E7 坐实），非纯宿主侧因素。
- 主题态事实（2026-10-31 实测）：dev 实例主题=「跟随系统」，当天系统=浅色 + 深色背景图（55% 遮罩）——浅/深两态都是用户环境的合法运行态，插件必须双态合格。

## E7 视觉打回修复轮（2026-10-31 输入N+3，im-dev 实现 ×2 轮 + lead 双主题复测 · Verified）

**轮1（P0 主题底）**：修复单 6 条——①`.ig-root` 加 `background: var(--el-bg-color)`（不透明主题底，宿主 /ai-agent 先例同源）②4 卡面板 40% 玻璃 → `var(--el-fill-color-light)` + `var(--el-border-color)`，backdrop-filter 移除 ③7 处辅助文字 `--fg-tertiary`→`--fg-secondary` ④气泡底复核（in=paper 白+边框；out=gold 16% color-mix；「回复失败」tag 语义色 gold→朱砂）⑤配置视图实底化 ⑥`pnpm run build` exit 0。**浅色复测 PASS**：标题 17.59 / 副标题 9.0 / 时间戳·预览·状态灯 9.0 / in 气泡 17.59 / out 气泡(合成底) 15.71 / error 红字 4.54 / 失败 tag 5.09 / eyebrow 4.5（全 ≥4.5 或 ≥3 次要线）；截图 `walkthrough-v2-light-thread.png`。

**轮2（轮1 引入的深色回归）**：复测抓到 in 气泡 `--paper-white`（**固定米白，仅浅色块定义**）× `--fg-primary`（深色翻浅字）= 深色下浅字压浅泡不可读。修复：in 气泡底 → `var(--el-bg-color)`（双主题自适应；浅色值不变）+ 全插件固定色 token 审计（`--paper-tint/-weak`/`--gold-tint` 实证为浅/深块成对重定义=自适应；`--paper-white` 残余仅 switch 圆点装饰块，豁免论证成立）；`pnpm run build` exit 0。**深色复测 PASS**：in 气泡 **16.53**（修复前不可读）、root/面板深底自适应 ✓、eyebrow 3.91 ✓；截图 `walkthrough-v2-dark-thread.png`。

**门禁**：`pnpm run design:check` 修复后重跑 **PASS EXIT=0**（style-tokens 1759 vs 基线 2501 零增量；compliance a/b/c/d 与基线持平）；插件冒烟 e2e 复跑见 E3 补记。

**边界记录**：走查/修复全程未触碰用户运行实例（:51888）与用户浏览器标签；dev-stack 曾两次进程消亡（vite file-watcher 被 repo 内文件写触发 reload 后进程退出，日志 `.temp/dev-stack-frontend.log` 09:34:45 "page reload e2e/plugins/ai-agent/ai-agent.spec.ts"），均重启恢复，与插件代码无关（环境行为，已记 TODO 沉淀项）；走查种子数据已清零复核；主题设置已恢复原状（跟随系统）。

## E6 机器校验

- 本批为功能新增（非缺陷批）→ 不触发 check-problem-classes 强制门禁；未新增问题类。
- **DESIGN.md 设计门禁①静态守卫 `pnpm run design:check`**（2026-10-31 输入N+2 补跑，脚本扫描范围含 `Plugins/*/web/src`，本批改动在管辖内）：**PASS EXIT=0** —— style-tokens 1759 处 vs 基线 2501（**无新增，反而 -742**）；design-compliance a(token族)=209 / b(胶囊)=0 / c(玻璃过头)=0 / d(蓝紫渐变)=0，与基线持平零增量。日志 `.temp/tmp/design-check-chatlist.log`。
  - ⚠️ **如实登记**：批内 im-dev 仅做了新文件裸色值手工自查（结论与棘轮一致），`design:check` 脚本本身未显式执行——**批内执行缺口，闸门三步①补跑后闭合**；pre-commit hook 在提交触及前端源码时仍会自动拦截（DESIGN.md §门禁化说明双保险）。

## Unknown 清单

| 项 | 状态 |
|---|---|
| 超 50 条消息联系人的「加载更多」真机形态 | Unknown（单测已覆盖游标逻辑；留运行实例复验） |
| 企微真实消息经新落库点的端到端（收→库→列表出现） | Unknown（dev 无真实企微长连接；单测已覆盖落库点；留 :51888 更新后只读复验） |

## AC 对账（机械校验补录 · 2026-11-02）

> pre-commit `-EnforceAcCoverage` 需 02 的 ACn 在 05 有同形编号引用；本节为编号对账表，证据均出自上文 E1-E7（来源等级沿用各节标注）。

| 02 编号 | 证据位置 | 等级 |
|---|---|---|
| AC1 | E4 §AC-1 ✓（走查：默认会话视图/列表/引导空态） | Verified |
| AC2 | E4 §AC-2 ✓（最近消息倒序 + 气泡升序渲染） | Verified |
| AC3 | E4 §AC-3 ✓（error 气泡金色 meta + ErrorText 摘要） | Verified |
| AC4 | E2（beforeId 游标 + limit 钳制 205 行实测）+ E4 §AC-4 △（>50 条真机形态 Unknown） | Verified（单测） |
| AC5 | E4 §AC-5 ✓（配置视图往返全功能在位） | Verified |
| AC6 | E4 §AC-6 ✓（12 轮询 DOM 稳定无重建闪烁） | Verified |
| AC7 | E3（e2e 无 token 401 / 带 token 200 活体实测） | Verified |
