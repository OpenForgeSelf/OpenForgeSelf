# 设计插件 roadmap

> 本文件记录设计插件（design-system）接下来「做什么、为什么做、怎么做、做到什么程度算完成」。
> 按优先级 P1 > P2 > P3 排列；P1 是阻塞性/高价值缺口，P2 是体验增强，P3 是生态扩展。
> 每完成一项，请在对应 `- [ ]` 改为 `- [x]`，并在 `README.md §九` 历史里追加版本信息。

---

## P1 · 现在就应该做

### P1.1 后端持久化设计产物

**现状**：当前工作区与历史存在 `localStorage` 中。刷新页面可恢复，但换浏览器/清缓存即丢失；无法跨设备共享，也无法作为「设计资产」沉淀。

**目标**：让生成的 `DesignSystem` 可保存、可列表、可载入、可删除，并成为项目内的可追踪资产。

**实现方案**：
1. 后端新增实体 `DesignRecord`（XCode 实体，按现有 `ForgeSelf.Api` 分层）：
   ```csharp
   public class DesignRecord {
       public long Id { get; set; }
       public string PluginId { get; set; } = "design-system";
       public string Name { get; set; } = "";
       public string Brief { get; set; } = "";
       public string Industry { get; set; } = "";
       public string SeedJson { get; set; } = "";          // hue/accentHue/fontKey
       public string DesignSystemJson { get; set; } = ""; // 完整 DesignSystem 序列化
       public long UserId { get; set; }
       public DateTime CreatedAt { get; set; }
       public DateTime UpdatedAt { get; set; }
   }
   ```
2. 新增控制器 `DesignRecordsController`（路由 `api/design-records`）：
   - `GET /api/design-records` — 当前用户的列表（支持分页）
   - `GET /api/design-records/{id}` — 详情
   - `POST /api/design-records` — 保存（name/brief/industry/seed/designSystem）
   - `PUT /api/design-records/{id}` — 更新
   - `DELETE /api/design-records/{id}` — 删除
3. 前端 `DesignStudio.vue`：
   - 历史区从 `localStorage` 改从 API 拉取；保留 localStorage 作为离线降级。
   - 生成后自动 POST 保存（或提示保存）。
   - 新增「云端历史」vs「本地历史」切换/合并。

**验收标准**：
- `dotnet build` + `dotnet test`（新增 xUnit 覆盖 CRUD）通过。
- 前端 `pnpm run check` + `pnpm run test` 通过。
- e2e 验证：生成设计 → 刷新 → 从云端列表载入 → 展示页换肤一致。

**风险**：涉及数据库迁移、鉴权、用户隔离。按 AGENTS §6.4，**必须升级给人确认 schema 与数据归属策略后再写代码**。

**不做的替代方案**：继续增强 localStorage（导出/导入 JSON），直到有明确的团队协作需求。

---

### P1.2 修复宿主插件版本/更新/安装路径

**现状**：
- `PluginVersionService.Initialize(pluginsPath)` 从未被调用，导致 `/api/plugin/updates|update|versions|rollback` 死代码。
- `POST /api/plugin/install` 把插件包解压到 exe 所在目录，而不是 `publish/Plugins/<id>/`（活动代码目录）。
- 新插件热重载（FileSystemWatcher）只对已存在插件生效；全新插件必须 install 触发 `DiscoverPlugins()`。

**目标**：让插件版本管理、热更新、全新安装都走正确路径，发布插件不再依赖人肉目录搬运。

**实现方案**：
1. 在宿主启动流程中调用 `_pluginVersionService.Initialize(pluginsPath)`（pluginsPath = `AppContext.BaseDirectory/Plugins`）。
2. 修复 `PluginInstallerService.InstallFromPackage`：
   - 包体应解压到 `pluginsPath/<id>/` 而不是 `AppContext.BaseDirectory/<id>/`。
   - 解压后调用 `_pluginManager.DiscoverPlugins()` 并返回新插件信息。
3. 修复 `run-plugin-publish-verify.ps1` 中的版本化 API 调用：当前因 `Initialize` 未调用，脚本实际走的是热重载/安装兜底；若 P1.2 修复后，应优先使用版本化 API。

**验收标准**：
- 安装新插件后，文件正确出现在 `publish/Plugins/<id>/`。
- `/api/plugin/updates` 返回当前可用更新列表（不再 500/空）。
- `run-plugin-publish-verify.ps1` 在不重启宿主的情况下完成「首次安装 → 启用 → 版本切换」全流程。

**风险**：改动宿主核心插件管理，影响所有插件。需完整回归所有插件 e2e。

---

### P1.3 pluginViewLoader 缓存键加入 content-hash

**现状**：前端入口 URL 是 `/plugins/{id}/web/dist/index.js?v={version}`。版本号不变时，浏览器从磁盘缓存取旧 bundle；前端小改动（文案、icon）用户必须硬刷才看得到。

**目标**：同版本内的前端改动也能即时生效，同时保留版本号用于兼容性校验。

**实现方案**：
1. 插件构建脚本生成 `dist/index.js.sha256`（或把 hash 写入 plugin.json `frontend.hash`）。
2. 宿主 `plugin.json` 解析时读取 hash，清单 URL 输出 `?v=1.2.0&h=abc123`。
3. 前端 `pluginViewLoader` 以 `v + h` 作为缓存键；hash 变化即重新 fetch。

**验收标准**：
- 不升版本号，仅改前端文案并热发布后，新窗口访问立即看到新文案。
- 升版本号时 hash 同时变化，旧缓存失效。

**风险**：需改动宿主清单生成逻辑；要确保插件旧版本（无 hash）兼容。

---

## P2 · 体验增强

### P2.1 LLM 增强模式（可选开关）

**现状**：生成引擎是确定性模板。对「我需要一套面向印度二三线城市、低带宽、多语言、RTL 适配的 B2B 批发 App」这类复杂需求，只能给出通用电商骨架。

**目标**：在保持确定性引擎作为 fallback 与回归基线的前提下，给用户提供「智能生成」开关。

**实现方案**：
1. 前端新增「智能生成」toggle（默认关闭）。
2. 后端新增 `DesignSystemController` 端点 `POST /api/design-system/generate`：
   - 接收 brief + industry hint。
   - 调用 LLM（复用项目内现有 AI 网关 / OpenAI / Anthropic provider）。
   - LLM prompt 要求按 `DesignSystem` schema 输出 JSON。
   - 输出经校验后回退到确定性引擎（LLM 失败/超时/格式错误）。
3. 前端同时保留离线能力：LLM 不可用时自动切回本地 `generateDesignSystem`。

**验收标准**：
- 关闭智能生成：同需求两次生成完全一致（回归测试）。
- 开启智能生成：复杂需求输出比模板更贴合语义。
- LLM 失败时无白屏，自动降级。

**风险**：引入外部依赖与成本；输出不可完全回归，需要 separate e2e 路径。

---

### P2.2 自动对比度与截图回归校验

**现状**：设计自检第 2 条（对比度）靠人读；展示页换肤后无自动视觉回归保护。

**目标**：把 WCAG 对比度校验与视觉回归纳入 e2e，防止换肤后出现不可读文本或布局崩坏。

**实现方案**：
1. 在 e2e 中用 Playwright 生成常见 hue（紫/蓝/橙/绿）的设计系统，对每个生成结果：
   - 读取 `tokens.color.text.fg-1` vs `tokens.color.surface.surface-1`，用 `colorjs.io` 或内置公式计算对比度。
   - 断言正文对比度 ≥ 4.5:1，大文本/按钮 ≥ 3:1。
2. 建立截图基线（golden shots）：
   - 基线目录 `e2e/snapshots/design-system/`。
   - 每次 e2e 对「设计令牌 / 组件库 / 实时预览 / 控制台 UI Kit」截图并与基线做像素 diff（阈值可配置）。
3. 基线更新命令：`pnpm test:e2e:update-snapshot`。

**验收标准**：
- 新增对比度断言，失败时报告具体 hue 与颜色对。
- 基线 diff 失败时 e2e 不通过（除非显式更新）。

**风险**：不同机器/字体可能导致截图抖动；需要稳定的 headless 环境。

---

### P2.3 把真宿主发布 + 浏览器走查沉淀为可复用 skill

**现状**：每次真宿主验证都要读 `.agents/skills/plugin-publish-verify/SKILL.md` 并手动组合 PowerShell 命令 + Playwright MCP。流程已跑通但未封装。

**目标**：新增 `.agents/skills/forge-design-system-verify/SKILL.md` + 脚本，支持一键：
- 发布/热重载到指定宿主；
- 用 Playwright 打开插件；
- 生成设计系统；
- 切页截图；
- 下载产物并校验。

**验收标准**：新 skill 能在一次调用内完成上述全部步骤，输出通过/失败报告与截图路径。

---

## P3 · 生态

### P3.1 Figma / Tokens Studio 插件

**现状**：`tokens.json` 是自定义 schema，设计工具无法直接消费。

**目标**：让生成的 token 能导入 Figma / Tokens Studio。

**实现方案**：
1. 在 `exporters.ts` 新增 `toDesignTokensFormat(ds)`，输出 [Design Tokens Format](https://design-tokens.github.io/community-group/format/) 兼容 JSON。
2. 提供 `figma-tokens.json` 下载选项。
3. 文档说明导入步骤。

**验收标准**：导出的 `figma-tokens.json` 可被 Tokens Studio 插件成功导入，颜色、字号、间距分类正确。

---

### P3.2 社区预设市场

**现状**：预设硬编码在 `presets.ts` 中，新增预设需改代码并重新构建插件。

**目标**：支持从外部文件/目录加载预设，用户可分享自己的设计系统预设。

**实现方案**：
1. 在插件数据目录 `~/.forgeself/Plugins/design-system/presets/` 下读取 `*.preset.json`。
2. 新增 `PRESET_LOADER` 在启动时扫描并合并到 `PRESETS` 列表。
3. 工作台新增「加载预设」下拉，支持一键把预设设为 activeDs。
4. 支持导出当前生成结果为 `.preset.json`。

**验收标准**：
- 把一个符合 schema 的 `.preset.json` 放入 presets 目录，重启宿主后在工作台可见。
- 导出预设再导入，内容一致。

---

## 附录：roadmap 维护规则

- 新增条目使用 `- [ ] 编号 标题（P级别）` 格式，放在对应优先级分区末尾。
- 完成条目改为 `- [x]`，并在 `README.md §九` 历史里追加版本与日期。
- P1 条目若涉及后端 schema / 迁移 / 鉴权 / 宿主核心，必须先走 AGENTS §6.4 升级给人确认。
- 每轮任务结束时，检查本 roadmap 是否有过时条目（已完成的移至历史、已放弃标为不做的决策）。
