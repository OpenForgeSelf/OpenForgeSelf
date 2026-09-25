---
功能编号: 035
状态: 已实施（部分）：① 发布带版本号（版本化显式更新，2026-09-24 实测通过）；② 插件管理界面显示启用状态；③ web/dist 版本化读取缺陷已修复。剩余缺口（watcher 降级 / 扁平迁移 / frontend 版本操作 UI 等）仍登记待办
最后更新: 2026-09-24
关联: specs/034-mcp-center（来源），PluginVersionLayout / PluginVersionService / PluginHotReloadWatcher / PluginFrontendFileMiddleware / PluginManager / publish 脚本 / plugin-development·plugin-publish-verify 技能
---

# 035 插件版本化发布与显式升级机制

## 1. 需求来源与目标（用户原话整理）

> 发布插件 → 出现在插件目录的版本子目录，比如 `Plugins/mcp-center/1.0.0/插件完整内容`；
> `Plugins/mcp-center/` 下的某个文件记录当前生效版本；
> 插件以后可以**随便发布**（新版本落新目录，互不覆盖）；
> 宿主在**插件管理界面决定：启动、更新版本**——显式更新升级，而不是热更新（watcher）触发。

一句话：**把插件发布/升级从「文件落盘 + 自动热重载」改为「版本快照 + 显式切换」**，
发布动作本身永不触碰正在加载的文件，DLL 锁与热重载竞态从机制上消除。

## 2. 现状对照（2026-09-23 代码查证）

### 2.1 已实现（side-by-side 版本目录机制存在）

| 能力 | 位置 | 现状 |
|---|---|---|
| 版本目录布局 `Plugins/<id>/versions/<semver>/<完整内容>` | `PluginVersionLayout.cs`（`VersionDirectory`/`VersionsFolderName`） | ✅ 已实现；`StageVersion` 全量拷贝（含 web/dist、plugin.json） |
| `current` 指针文件 + 原子切换 | `PluginVersionLayout.cs:53-62`（temp + `File.Move` 原子覆盖） | ✅ 已实现 |
| 入口程序集解析优先走 `versions/<current>/` | `PluginManager.ResolvePluginInstance:383` + `ResolveEntryAssemblyPath:69-86` | ✅ 已实现（扁平布局为兜底） |
| 显式更新/回滚接口 | `PluginVersionService.UpdatePlugin/RollbackPlugin`（`POST /api/plugin/update/{id}`、`rollback/{id}`） | ⚠️ 已实现但**有缺陷**（见 2.2） |
| 失败回退上一版本 | `PluginManager.ReloadPlugin:947-975`（坏版本回退 previousVersion） | ✅ 已实现 |
| 版本保留策略 | `PruneVersions`（保留当前 + 上一版，更旧延迟删除、绝不阻塞） | ✅ 已实现 |
| 旧 DLL 句柄探测与延迟删除 | `PluginAssemblyUnloader`（`TryOpenExclusive`/`TryDeleteDirectory`/`ForceCollect`） | ✅ 已实现 |
| 活动清单同步 | `SyncActiveManifest`（versions/<ver>/plugin.json → 活动 plugin.json） | ✅ 已实现 |
| 前端「已安装/可更新」框架 | `PluginStore.vue`（版本徽标 / 启用禁用 / 可更新徽标） | ⚠️ 有框架，缺显式版本操作（见 2.2） |
| 前端按「id+版本」缓存插件界面 | `pluginViewLoader.ts`（版本变化自动重载组件） | ✅ 已实现，天然适配版本切换 |

### 2.2 缺口（本期需求的差异点）

1. **触发方式仍是「覆盖活动目录 + watcher 自动热重载」** ✅ **已改（2026-09-24）**
   `run-plugin-publish-verify.ps1` 主路径已改为「stage 到 `_backups/<id>/<version>/` → 显式 `POST /api/plugin/update/{id}` → 断言 `versions/<version>/` + current 指针」；不再覆盖活动目录、不依赖 watcher。实测：AIAgent 1.7.1→1.7.2、McpCenter 2.1.0→2.1.1（扁平存量首次版本化切换）均通过。
2. **版本化更新接口有缺陷**（troubleshooting 已知） ✅ **已修复（2026-09-24）**
   修复方向①落地：`PluginFrontendFileMiddleware.ResolveFrontendRoot` 改为「current 指针存在且 `versions/<current>/web` 存在 → 从版本快照读取；否则回退扁平 `{插件目录}/web`」；`PluginController.ComputeWebVersion` 指纹同样基于版本快照。实测：`/plugins/ai-agent/web/dist/index.js` 返回版本快照内容（探针标记命中），指纹随版本快照变化。
3. **前端缺「显式版本操作」**：已安装插件卡片只有启用/禁用；缺「更新到最新」「回滚到上一版本」「版本历史/已安装版本列表」操作入口（后端接口已具备）。 ⏳ 未做（登记待办）
4. **发布脚本未走 side-by-side**： ✅ **已改（2026-09-24，同缺口 1）**
5. **watcher 自动热重载** ✅ **已一刀切移除（2026-09-24，用户拍板）**：删除 `PluginHotReloadWatcher.cs`（类+注册+专属测试 5 例），`AppBuilder.cs` 注册移除；插件生效一律走版本化显式更新（`POST /api/plugin/update/{id}`）或冷启动。
6. **扁平布局存量迁移**：现有 `publish/Plugins/<Dir>/<Dir>.dll + web/dist`（无 current 指针）如何平滑迁移到版本目录——启动检测「无指针但扁平文件存在」→ 自动 stage 为 `versions/<清单版本>/` 并写指针（一次性迁移）。 ⏳ 未做（登记待办；实测验证了「手动发布扁平存量插件 → 首次版本化切换」可行，无需重启）

## 3. 目标设计（下期实现）

### 3.0 改动面全景（实现时全量清单，按模块勾选核对）

> 下期立项后按此清单逐项盘点，每项改动完成即勾选；最终交付前对照本表确认无遗漏。

**A. 后端宿主加载逻辑（`ForgeSelf.Api/Plugins/`）**

| 文件 | 现状 | 需要改 |
|---|---|---|
| `Services/PluginFrontendFileMiddleware.cs:160` | 前端静态资源读**插件根目录** `web/dist/` | ✅ **已改（2026-09-24）**：`ResolveFrontendRoot` 版本化优先（`versions/<current>/web` 存在则读取，否则回退扁平 `{插件目录}/web`）；配套测试 +3 |
| ~~`Services/PluginHotReloadWatcher.cs`~~ | ~~监听 `Plugins/` 下 versions/ + current 指针 + 扁平目录 DLL 落盘变更，debounce 300ms → 自动 `ReloadPlugin`~~ | ✅ **已删除（2026-09-24 一刀切）**：类文件 + `AppBuilder.cs` 注册 + 专属测试一并移除；`PluginManager.ReloadPlugin` 保留为显式重载入口（`PluginReloadTests` 3 例仍绿） |
| `PluginManager.cs` | `DiscoverPlugins():245` 扫描扁平目录；`ResolveEntryAssemblyPath` 已优先 versions/；`ReloadPlugin:937-975` 已有坏版本回退；`SyncPluginMetadataFromVersions:894-924` 已同步版本信息 | ⏳ 启动路径加**一次性迁移**：检测「无 current 指针但扁平文件存在」→ stage 为 `versions/<清单版本>/` + 写指针；确认扁平兜底在迁移后移除/保留 |
| `Services/PluginVersionService.cs` | `StageVersion:273` / `UpdatePlugin:121` / `RollbackPlugin:209` / `GetPluginVersions:75` / `CheckForUpdates:33` 已具备；`SyncActiveManifest:320` **只同步 plugin.json** | ⏳ ① 修 web/dist 同步缺陷（本期已走 Middleware 版本化读取，此路径可不再需要）；② `UpdatePlugin` 语义对齐「显式切换」（切 current 后等待 ALC 回收再加载，不依赖 watcher） |
| `PluginVersionLayout.cs` | versions/<semver>/ + current 指针 + 原子切换已具备 | ⏳ 补扁平迁移辅助（`MigrateFlatToVersioned`） |
| `Controllers/PluginController.cs` | `update:757` / `rollback:787` / `{pluginId}/versions:817` / `install:649`（.forgeself-plugin 包路径）已具备 | ✅ **已补类级 `[Authorize("ApiKeyPolicy")]`（2026-09-24，铁律 17）**：管理面全端点需宿主 API 令牌，无 token 401；新增 `PluginControllerAuthTests` 反射断言 |
| 启动装配（`Program.cs` / DI） | ~~watcher 作为 IHostedService 注册~~ | ✅ **已移除（2026-09-24）**：`AppBuilder.cs` 两行注册删除，留注释说明一刀切理由 |

**B. 打包发布脚本与构建（仓库根 `scripts/` + csproj + 发布技能脚本）**

| 文件 | 现状 | 需要改 |
|---|---|---|
| `.agents/skills/plugin-publish-verify/scripts/run-plugin-publish-verify.ps1` | **主路径 = 覆盖活动目录 + watcher 热重载**（步骤 5:173-245）；验证判据 = 版本==清单 + 资源 200 + DLL hash | ✅ **已改（2026-09-24）**：步骤 5 = `POST /api/plugin/update/{id}` 显式版本化切换；验证 = 版本化布局断言（`versions/<version>/` + current 指针）+ API 版本 + 前端清单 + 静态资源 + 版本快照入口 DLL hash |
| `scripts/publish-plugin.ps1` | stage 到 `publish/Plugins/_backups/<id>/<version>/`（供覆盖用） | 改产出「版本快照」：直接 stage 到版本目录源，或生成 `.forgeself-plugin` 包（走 `POST /api/plugin/install`） |
| `scripts/publish-plugin-full.ps1` | 全量发布封装 | 随主路径调整 |
| `build.ps1` | 全量构建 → publish 扁平 `Plugins/<Dir>/` | 视设计定：保持全量扁平 + 由显式机制 stage；或产版本目录 |
| `ForgeSelf.Api/ForgeSelf.Api.csproj` | 20+ 个 `Stage*Plugin` target（:213-426）+ `StagePluginsToPublish:440`（Publish 后整体复制扁平） | 拷贝目标/产物形态随发布路径调整（决定扁平是否仍为全量产物形态） |

**C. 技能文档（`.agents/skills/`）**

| 文件 | 需要改 |
|---|---|
| `plugin-development/SKILL.md` | 铁律 16（发布唯一入口描述）、§四 维护闭环步骤 3（发布动作）、§五 关键事实速查（热更新/覆盖路径、`POST /api/plugin/update` 现状描述）、新增插件默认按版本化发布 |
| `plugin-development/references/plugin-acceptance.md` | 验收标准「发布」大类：从「热重载成功」改为「版本快照 + 显式切换 + 零文件覆盖」 |
| `plugin-publish-verify/SKILL.md` + `references/{workflow,verification,troubleshooting}.md` | 更新路径主路径/兜底表**反转**（主路径 = 版本化显式；watcher 覆盖降为兜底/废弃）；流程骨架、验证判据、缺陷清单同步 |
| `plugin-frontend-scaffold/SKILL.md` | 前端产物 `web/dist` 随版本快照的说明 |
| `e2e-testing/SKILL.md` | e2e 发布链路（发布→更新→回滚→迁移）写法规范 |
| `AGENTS.md §2.4` | 技能清单表格中 `plugin-publish-verify` 职责描述同步 |

**D. 前端（`ForgeSelf.Web/`）**

| 文件 | 现状 | 需要改 |
|---|---|---|
| `src/views/PluginStore.vue` | 已安装卡片只有启用/禁用 + 「可更新」徽标（:334-339） | ✅ **已改（2026-09-24）**：卡片 footer-left 组显示 `v版本号` + 「已启用/已停用」标签（`.enabled-badge.on/.off`）+ 启用/禁用按钮（标签随 API `isEnabled` 实时切换，禁用/启用操作即时生效）；⏳ 补「更新到最新 / 回滚到上一版本 / 版本历史」显式操作（后端接口已具备） |
| `src/utils/pluginViewLoader.ts` | 已按 id+版本缓存（天然适配） | 确认 current 语义，无需大改 |
| `src/stores/pluginStore.ts` 等 service | — | 对接 update/rollback/versions 端点 + 版本历史 UI 数据 |

**E. 文档（`docs/`）**

| 文件 | 需要改 |
|---|---|
| `05-guides/plugin-hot-reload-limitations.md` | 重写/标注废弃（机制从「覆盖+自动热重载」变为「版本快照+显式切换」） |
| `05-guides/plugin-frontend-development.md` | 前端产物路径说明（web/dist 随版本） |
| `16-reference/api.md` | 插件端点契约变更/新增（update/rollback/versions/install） |
| `01-architecture/overview.md` + `host-capability-seams.md` | 插件加载/更新机制描述同步 |
| `07-decisions/not-taken-decisions.md` | 记录「watcher 自动热重载降级为仅通知」决策 |
| `15-roadmap/plugin-architecture.md` | 路线图同步 |
| `02-features/034-mcp-center.md` | 发布说明段若引用热重载路径则同步 |
| `specs/027-cordis-kernel/`（如含热更新文档） | 同步 |

**F. 测试**

| 范围 | 需要改 |
|---|---|
| `ForgeSelf.Api.Tests`（PluginVersionService / PluginManager 相关） | 现有用例适配新语义；新增：扁平→版本目录迁移、web/dist 随版本生效、坏版本回退、current 原子切换 |
| `ForgeSelf.Web/e2e` | 新增四条链路：发布→更新→回滚→迁移；现有依赖热重载路径的用例同步 |

**G. 存量数据与迁移**

| 项 | 说明 |
|---|---|
| `publish/Plugins/<Dir>/` 扁平布局（全部现有插件） | 启动一次性迁移到 `versions/<清单版本>/` + current 指针 |
| `publish/Plugins/_backups/` 暂存目录 | 废弃或改语义（不再作为覆盖来源） |
| 活动目录残留 `web/dist` / 旧 bundle | 迁移后清理策略 |

### 3.1 目录布局（用户描述，与现有机制对齐）

```
Plugins/<id>/
  current                     ← 文本文件，当前生效 semver（原子切换）
  plugin.json                 ← 活动清单（与 current 同步）
  versions/
    <semver>/                 ← 插件完整内容不可变快照
      plugin.json
      <Id>.dll + 私有依赖
      web/dist/**             ← 前端产物随版本
      （其余随发布的内容）
```

### 3.2 显式管理流（替代自动热重载）

- **发布**：新版本完整内容落 `versions/<new>/`（不碰任何在加载文件；可同时存在任意多版本）
- **更新**：管理界面/接口 → `StopPlugin`（Fiber 停止器 + ALC Unload + ForceCollect）→ 原子切 `current` → 加载新版；失败自动回退上一版本
- **启动/停用**：仅切换运行状态，不换文件
- **回滚**：切 `current` 到已保留版本（保留策略：当前 + 上一版，可配置）

### 3.3 验收标准草案（下期立项细化）

本期（2026-09-24）已实测验收：

- [x] **发布新版本落版本目录（发布带版本号）**：AIAgent 1.7.1→1.7.2、McpCenter 2.1.0→2.1.1（扁平存量首次版本化切换）均建立 `versions/<v>/` + current 指针，API 版本、版本快照 DLL hash、静态资源 200 全过（`run-plugin-publish-verify.ps1` 输出 `VERIFY PASSED`）
- [x] **管理界面显示启用状态**：插件管理页（`:51888/plugins`）每张卡片显示 `v版本号` + 「已启用/已停用」标签 + 启用/禁用按钮；禁用→「已停用」、启用→「已启用」实时切换（浏览器走查截图存档）
- [x] **更新后前端资源（web/dist）立即生效**（修复缺陷）：`/plugins/ai-agent/web/dist/index.js` 实测返回版本快照内容（探针标记命中）；`ComputeWebVersion` 指纹基于版本快照内容变化

仍待办（下期或认领）：

- [ ] 管理界面「更新到最新 / 回滚到上一版本 / 版本历史」显式操作 UI
- [x] **watcher 自动热重载一刀切移除**（2026-09-24 输入 8，用户拍板：不做降级，直接删类+注册+测试）
- [ ] 扁平布局存量插件启动时一次性迁移到版本目录
- [ ] 坏版本加载失败自动回退上一版本的 e2e 覆盖
- [x] **全程零「覆盖活动目录 + watcher 自动重载」路径确认**（2026-09-24 输入 8：watcher 已删，发布脚本主路径为版本化显式更新）
- [ ] e2e 覆盖：发布→更新→回滚→迁移四条链路
- [x] **管理面接口 `[Authorize("ApiKeyPolicy")]`**（2026-09-24 输入 8：PluginController 类级已补 + 反射断言测试；无 token 实测 401）
- [ ] §3.0 改动面全景 A-G 逐项勾选核对无遗漏（后端加载逻辑 / 发布脚本 / 技能文档 / 前端 / docs / 测试 / 存量迁移）
- [ ] 技能文档与发布脚本先行改造（先补脚本再发，AGENTS 铁律），技能验收标准同步

### 3.4 本期实施记录（2026-09-24，输入 6）

| 项 | 内容 |
|---|---|
| 后端 | `PluginFrontendFileMiddleware.ResolveFrontendRoot` 版本化优先；`PluginController.ComputeWebVersion` 版本化指纹基准 |
| 发布脚本 | `run-plugin-publish-verify.ps1` 主路径改「stage → POST /api/plugin/update/{id} → 版本化布局断言 + 版本快照 DLL hash」 |
| 前端 | `PluginStore.vue` 卡片「已启用/已停用」标签 + footer-left 布局 |
| 测试 | `PluginFrontendFileMiddlewareTests` +3、`PluginFrontendManifestTests` +1（版本化读取/指纹用例）；插件相关单测 33/33 绿 |
| 门禁 | `dotnet build` 0 errors；`pnpm run check` 0 errors / 85 warnings（既有）；`pnpm run test` 468/472（4 例既有 AIAgent http.test.ts 漂移失败，非本次引入，TODO 已登记） |
| 实测 | 51888 宿主：AIAgent 1.7.2（版本化升级）、McpCenter 2.1.1（扁平→版本化首次切换）；浏览器走查插件管理页标签渲染 ✅；输入 8 后：watcher 移除无残留（ForgeSelf.dll 字节探针 absent）、无 token 401、带 token 全端点 200、18 插件正常 |
| 遗留 | vitest 4 例 AIAgent 漂移（既有 P2/P3 TODO，输入 7 已修复 472/472）；发布实测导致仓库 plugin.json 版本提升（AIAgent 1.7.2 / McpCenter 2.1.1，属发布动作自然结果） |

## 4. 范围与排期

- **本期（2026-09-24，输入 6）已实施**：发布带版本号（发布脚本版本化主路径 + 实测）、插件管理界面显示启用状态、web/dist 版本化读取缺陷修复。见 §3.4。
- **输入 8（2026-09-24）已处理**：watcher 一刀切移除（非降级）+ PluginController 鉴权补齐（铁律 17）；vitest 漂移已修（输入 7）。**剩余**：扁平一次性迁移 / frontend 显式版本操作 UI / 坏版本回退 e2e / 发布→更新→回滚→迁移 e2e 四链路——登记 TODO 待认领（P2/P3）。
- 与本需求相关的既有文档：`docs/05-guides/plugin-hot-reload-limitations.md`、`docs/05-guides/plugin-frontend-development.md`、`docs/15-roadmap/plugin-architecture.md`（剩余缺口实现后同步更新）。
- 风险提示：watcher 移除与鉴权已按用户拍板落地并全量 Verify；剩余改动（扁平迁移 / 前端显式版本 UI）仍涉及插件加载核心，落地前按 AGENTS 风险分级先出架构设计再动手。

## 5. 待办

- TODO.md 已登记剩余缺口（P2/P3）：watcher 降级、扁平迁移、frontend 版本操作 UI、PluginController 鉴权、vitest AIAgent 4 例漂移（来源:输入6 实测遗留 + 既有）。


## 存量迁移（扁平 → 版本化）

一次性脚本 `scripts/migrate-plugin-versions.ps1`（幂等，可重复跑）：
- 对无 `current` / `versions/<ver>` 的插件目录，建 `versions/<当前版本>/` 快照（= 非宿主共享 DLL + `web/dist` + `plugin.json`），原子写 `current` 指针；
- 跳过 `_backups` / `_*` 与无 `plugin.json` 目录；宿主共享 DLL 白名单禁拷（对照 troubleshooting 坑 0）；
- 首次执行迁移 15 个扁平插件，已版本化/无清单的自动跳过。

后端 `GetPluginVersions` 合并三数据源（去重，按版本号降序）：
1. 已安装快照 `versions/<id>/`（side-by-side 布局）
2. 已暂存 `_backups/<id>/`（待更新版本）
3. 当前清单版本（带 release notes）

验证：`GET /api/plugin/{id}/versions` 对迁移插件返回快照版本（实测 todo-tracker → `["1.0.0"]`、ai-agent → 14 个版本）。

> 发布顺序铁律（2026-09-24 实测踩坑）：**宿主运行期间 build.ps1 会因 exe 被锁静默跳过更新**（exe 时间戳不动）。改宿主后端必须 **先停宿主 → build.ps1 → 起宿主**，再验证接口。