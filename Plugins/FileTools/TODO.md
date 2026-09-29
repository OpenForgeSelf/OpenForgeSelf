# FileTools 插件 TODO（插件级遗留）

> 只记**本插件**的未尽事宜与再犯就踩坑的约定；仓库级/跨插件待办在根目录 `TODO.md`，两边不重复正文。
> 当前版本：后端 `plugin.json` 1.1.2 / 前端 `web/package.json` 1.1.2（两处必须同时改，见下）。

## 发布与版本目录（先读这条再动手）

- 本插件**已版本化**：运行宿主目录形如 `Plugins/FileTools/{current, versions/<ver>/…}`。存在 `current` 时，宿主加载程序集（`PluginVersionLayout.ResolveEntryAssemblyPath`）与前端资源（`PluginFrontendFileMiddleware.ResolveFrontendRoot`）都**只读 `versions/<current>/`**，扁平根仅兜底。
- 因此「把新版铺到扁平 `Plugins/FileTools/`」＝没发布。生效三件套：① 全套 bits 进 `versions/<新ver>/`（**只放插件自己的** dll/deps/plugin.json/web/dist，混入宿主共享 DLL＝宿主启动即崩）② 原子写 `current` ③ 根 `plugin.json` 同步成快照清单。
- 版本号三处要一致：`plugin.json`、`web/package.json`、页面标题旁 `.ft-version` 徽标（徽标取自 `GET /api/plugin` 按 id 过滤，不是硬编码）。
- 跨插件的发布链缺陷（宿主包只发扁平、版本目录遮蔽新 bits）记在根 `TODO.md`，本文件不重复。

## 待办

- [ ] **前端 mock 债（P1，来源:输入1 立项实证 :349-376）**：`web/src/services/fileToolsApi.ts` 全文件无 HTTP 客户端，13 个真实端点（`api/filetools/*`）零消费者，重命名/清理/压缩/统计四个面板仍吃 mock。1.1.x 只让目录排行走了真接口（`fileToolsFoldersApi.ts`），**不顺手还这块债**；还债须逐面板替换并配 e2e，不允许整体删 mock。
- [ ] **批次D · 磁盘懒展开树 + 回收站删除（P1，来源:输入12，用户已拍「只做回收站删除」+「另起批次D」）**：采纳参考实现语义——`GET folders/drives`（固定磁盘 + 总容量）、`GET folders/children?dir=`（只列一层，目录先给 `totalBytes=-1` 不阻塞）、单目录异步统计 + **自实现 `ConcurrentDictionary` TTL 30s**（不引 `NewLife.Caching`，仓内 0 使用）、折叠丢该层再展开重算、按占用分档高亮（映射 `--el-color-*`，不照抄 WinForms 系统色）、面板底部操作日志区。**拒绝照抄两处**：`explorer /select`（浏览器不能起本地进程 → 改复制路径/树内定位）、`item.Delete()` 永久删除（违反 AGENTS §4.1 → 自写 `SHFileOperation` P/Invoke 送回收站，二次确认 + 逐条日志 + 单个失败不中断 + 类级鉴权）。走 §11 九阶段，产物 `docs/ai/pilot/batch-d-folder-tree-browsing/`。
- [ ] **AI 工具在聊天路径不可见（P2，需单独决策，来源:输入1，03-plan 偏差 D-8）**：本插件全部工具（含 `filetools.folder_stats`）不在白名单 `AIAgentService.ResolveOwnToolDefinitions():311-315`（只含 `ai-agent`+`memory-system`）；加 `"file-tools"` 会撑大小模型 prompt（历史教训：77 工具爆 prompt）。现仅经 `aiagent.universal_tool` 按名可达。
- [ ] **服务层假异步 + 一次物化（P2，来源:输入1，03-plan D-3）**：`Services/FileStatsService.cs` 等全线 `Task.FromResult` 包同步 + `GetFiles(AllDirectories)` 物化 → 改 `EnumerateFiles` 流式 + 取消。1.1.2 只改了目录排行一条路，其余四个服务未动。
- [ ] **目录大小求和重复实现收口（P2，来源:输入1）**：4 处各写一份（`FileStatsService.cs:26` / 宿主 `PluginVersionService.cs:471` / `ArchiveService.cs:43` / ScriptRunner 模板 `file-dir-size-ps`），第 5 处随批次C 出现 → 是否上移 `Abstractions` 需 `architecture-design` 出 ADR。
- [ ] **e2e 取消用例依赖「慢目录」（P2，来源:输入42 实测）**：1.1.2 并行引擎把 12k 夹具扫到 32ms，合成夹具撑不出 `Running` 窗口（UI 首帧轮询即得 Completed，取消键永远禁用）→ 用例改扫真实 `C:\Program Files`，`FT_E2E_SLOW_DIR` 可覆盖。换机器/无该目录的环境会红，属已知脆弱点，别当业务缺陷。
- [ ] **快照表留存策略未定（P3，来源:输入48 体检）**：`ScanFolderEntry` 随快照线性增长，`DELETE folders/snapshots/{id}` 只删当前快照行，没有过期清理或分页上限 → 要不要「快照保留 N 天 / 单快照条目上限」待定。

## 已完成（不重复展开）

- 目录大小排行 + 快照 + 对比（1.1.0/1.1.1，`docs/02-features/036-filetools-folder-ranking.md`）
- 扫描提速与并行中间态守恒（1.1.2，`docs/ai/pilot/folder-scan-perf-1.1.2/`）
- 控制器类级 `[Authorize("ApiKeyPolicy")]`（单控制器 `FileToolsController` 收口全部 21 个端点）
- 交付体检九节（`plugin-development/references/plugin-acceptance.md`，结论 ⚠️ COMPLETED_WITH_RISK，详表见 `.forgeself/memory/2026-09-29.md` 输入48）
