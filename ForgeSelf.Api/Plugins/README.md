# 插件体系（Plugins）

> 本文件是插件开发的权威规范。涵盖一个插件从「诞生 → 发布 → 发现 → 加载 → 应用 → 数据落盘 → 停用」的完整生命周期（前世今生）、命名规范（两层身份模型）、数据落盘约定，以及如何新建插件。
> 代码层（目录/程序集/EntryType）刻意使用 C# 原生 PascalCase；运行时层（Id/数据目录/路由）统一 kebab-case；**库文件名**则刻意取 XCode **连接名**（PascalCase，连接名即数据库名，与 XCode 模型一致）—— 这是**刻意设计而非不一致**，详见第四节。

---

## 一、设计用意（Why）

- **宿主只提供容器，不内置业务**：`ForgeSelf.Api` 仅提供运行时容器与扩展点契约（`IPlugin`/`IContext` 及各类 `I*Extension`），所有业务功能（记忆、抓包、定时、脚本、待办……）都以插件形式热插拔。
- **插件自包含**：每个插件 = 一个目录 + 一个程序集 `.dll` + 一份 `plugin.json`，可独立开发、发布、禁用。
- **数据隔离**：每个插件拥有独立数据目录与独立 SQLite 库文件，互不串库、互不污染宿主库。

---

## 二、一个插件的前世今生（生命周期）

1. **诞生（Scaffold）**：用 `PluginScaffolderService.Create(pluginId)` 生成骨架（目录 / `plugin.json` / `.csproj` / 示例 `IPlugin` 类），`pluginId` 自动按 kebab-case 产出。
2. **注册（Reference）**：在 `ForgeSelf.Api.csproj` 添加 `<ProjectReference>` 指向 `..\Plugins\{目录}\{目录}.csproj`；构建/发布时其产物被拷贝到 `publish/Plugins/{目录}/`。
3. **发现（Discover）**：宿主启动时 `PluginManager` 扫描 `publish/Plugins/*/plugin.json`，读取 `Id` / `EntryAssembly` / `EntryType`。
4. **加载（Load）**：经独立 `AssemblyLoadContext` 加载 `EntryAssembly`，再用 `Type.GetType(EntryType)` 反射出实现 `IPlugin` 的入口类。
5. **应用（Apply）**：宿主 `Build()` 之后调用 `plugin.Apply(ctx)`。插件在此注册服务、`IMenuExtension` 菜单、`IToolFunctionExtension` 工具函数、控制器路由等。
6. **运行（Run）**：控制器 / 工具函数经 DI 拿到插件服务实例；所有数据写入各自的数据目录（见第三节）。
7. **数据落盘（Persist）**：见第三节 —— 库文件统一命名 `{连接名}.db`（连接名即数据库名，与 XCode 一致），落在 `{数据根}/Plugins/{插件Id}/`。
8. **停用 / 移除（Unload）**：运行时已支持**单插件热更新/热插拔，不重启宿主**（详见第九节「运行时热更新」）。低阶能力：删除 `publish/Plugins/{目录}/` 仍可"硬卸载"该插件（其数据目录 `~/.forgeself/Plugins/{插件Id}/` 保留，可手动清理）。

---

## 三、数据落盘规范（Data Landing）

- **数据根（Data Root）**：由 `IDataLocationService` 解析。
  - 开发态（`BaseDirectory` 含 `Debug`/`Release`）：`{BaseDirectory}/Data/`
  - 发布 / 服务态：`%USERPROFILE%/.forgeself/`
- **插件库路径（统一）**：`{数据根}/Plugins/{插件Id}/{连接名}.db`
  - 例：`memory-system` → `~/.forgeself/Plugins/memory-system/MemorySystem.db`（连接名 `MemorySystem`）
  - 例：`mcp-center` → `~/.forgeself/Plugins/mcp-center/McpCenter.db`（连接名 `McpCenter`）
- **父目录自建**：SQLite 不会自动创建父目录，宿主在 `AddXCode` / `InitializeXCodeDatabase` 时先 `EnsureDirectory`，插件侧也可用 `ctx.EnsurePluginDataDirectory()` 取得已建好的目录。
- **库文件名铁律**：一律 `{连接名}.db`（连接名即数据库名，与 XCode 模型一致）。所有插件统一用 XCode 作为 ORM，连接名取自 `XCodeConfig.PluginDbs`。禁止以 `Id` 或任意写法命名（历史 `memory.db` / `capture.db` / `QuickLinks.db` 等混用写法已全部修正）。改名会生成第二份库，旧数据不可见。

---

## 四、命名规范（两层身份模型）

> **这是「刻意设计」而非「不一致」**。务必先理解两层，再写代码或建目录。

| 身份层 | 作用域 | 命名风格 | 能否改 | 原因 |
|---|---|---|---|---|
| **代码身份** | 目录名、程序集 `.dll`、 `plugin.json` 的 `EntryType`（=`Namespace.PluginClass`） | C# 原生 **PascalCase**（`AIAgent` / `ForgeSelf.Api.Plugins.MemorySystem.MemorySystemPlugin`） | 不可 | `EntryType` 须经反射 `Type.GetType("Namespace.Class")`，强制 PascalCase；且须符合 C# 命名约定 |
| **运行时身份** | `plugin.json` 的 `Id`、数据目录名、`~/.forgeself/Plugins/{id}`、前端路由 | **kebab-case**（全小写 + 短横线，`^[a-z0-9]+(-[a-z0-9]+)*$`） | 不可 | `Id` 会用作**目录名**（Linux 大小写敏感）与**前端路由**，kebab 最稳、最不易混淆 |
| **连接名身份** | 库文件名 `{连接名}.db`、`XCodeConfig.PluginDbs` 的 key、`DAL.Create/AddConnStr` 的连接名 | C# 原生 **PascalCase**（与代码身份一致：目录名、类名、连接名同风格） | 不可 | **连接名即数据库名**（XCode 模型）：库文件必须与连接名同名，XCode 实体 `ConnName` 据此命名，避免生成第二份库 |

**插件 Id 规范**：

| 正确 ✅ | 错误 ❌ | 说明 |
|---|---|---|
| `memory-system` | `memorysystem` | 多单词必须短横线分隔，全小写连排可读性差 |
| `quick-links` | `quicklinks.plugin` | 不加 `.plugin` 后缀（历史写法，已被移除） |
| `workflow-engine` | `workflow.engine.plugin` | 点号改用短横线 |
| `ai-agent` | `AIAgent` | 不混用大小写（Id 用作数据目录名，Linux 大小写敏感） |

**为什么不用 `.plugin` 后缀**：项目内没有命名空间/域隔离机制，`PluginManager` 仅做字符串相等比较，后缀不提供任何防冲突能力；而 `Id` 会用作数据目录名与前端路由名，点号在两者中都易混淆。新插件请用 `PluginScaffolderService` 生成，它已按本规范产出 Id。

**全量插件映射表（目录 / Id / 库文件 / 数据访问）**：

| 目录（PascalCase） | Id（kebab） | 库文件 | 数据访问方式 |
|---|---|---|---|
| `AIAgent` | `ai-agent` | `AIAgent.db` | XCode（`PluginDbs`，连接名 `AIAgent`） |
| `DevTools` | `dev-tools` | （无持久库） | — |
| `FileTools` | `file-tools` | （无持久库） | — |
| `MemorySystem` | `memory-system` | `MemorySystem.db` | XCode（`Memory`/`MemoryCategory` 实体，连接名 `MemorySystem`） |
| `McpCenter` | `mcp-center` | `McpCenter.db` | XCode（`ListenerConfig`/`CaptureSession` 实体，连接名 `McpCenter`） |
| `QuickLinks` | `quick-links` | `QuickLinks.db` | XCode（`PluginDbs`，连接名 `QuickLinks`） |
| `SamplePlugin` | `sample` | （无持久库） | — |
| `Scheduler` | `scheduler` | `Scheduler.db` | XCode（`PluginDbs`，连接名 `Scheduler`） |
| `ScriptRunner` | `script-runner` | `ScriptRunner.db` | XCode（`PluginDbs`，连接名 `ScriptRunner`） |
| `SystemMonitor` | `system-monitor` | （无持久库） | — |
| `TextTools` | `text-tools` | （无持久库） | — |
| `TodoTracker` | `todo-tracker` | `TodoTracker.db` | XCode（`PluginDbs`，连接名 `TodoTracker`） |
| `WorkflowEngine` | `workflow-engine` | `WorkflowEngine.db` | XCode（`PluginDbs`，连接名 `WorkflowEngine`） |

> 注：本项目统一以 **XCode 作为唯一 ORM**，各插件库文件均按「连接名即数据库名」规则命名为 `{连接名}.db`。`MemorySystem` 用 XCode 实体（`Memory`/`MemoryCategory` 表，连接名 `MemorySystem`）承载 CRUD（`MemoryServiceXCode` 及 AI 集成抽取）；`McpCenter` 用 XCode 实体（`ListenerConfig`/`CaptureSession` 表，连接名 `McpCenter`）。不存在 EF 上下文。

---

## 五、plugin.json 清单文件格式

```json
{
  "Id": "sample",                          // 插件唯一标识（必填，kebab-case，见第四节规范）
  "Name": "示例插件",                      // 插件名称（必填）
  "Version": "1.0.0",                      // 插件版本（必填）
  "Author": "OpenForgeSelf Team",          // 插件作者
  "Description": "插件描述",               // 插件描述
  "IconUrl": "",                           // 插件图标URL
  "EntryAssembly": "Plugin.dll",           // 入口程序集文件名（必填，= 程序集 .dll 名）
  "EntryType": "Namespace.PluginClass",    // 入口类全名（必填，需实现 IPlugin 接口，PascalCase）
  "Dependencies": [],                      // 依赖的其他插件 ID 列表
  "Permissions": []                        // 需要的权限列表
}
```

---

## 六、权限列表

- `FileSystem` - 访问文件系统
- `Network` - 访问网络
- `Database` - 访问数据库
- `Configuration` - 访问配置
- `ExtensionPoint` - 注册扩展点
- `AIService` - 访问 AI 服务
- `PluginManagement` - 管理其他插件

---

## 七、如何新建一个插件（Step by Step）

1. **生成骨架**：`PluginScaffolderService.Create("your-plugin-id")`（kebab），产出 `Plugins/YourPlugin/` 目录与示例 `IPlugin` 类。
2. **登记引用**：在 `ForgeSelf.Api.csproj` 添加 `<ProjectReference Include="..\Plugins\YourPlugin\YourPlugin.csproj" />`（目录名 PascalCase，与程序集一致）。
3. **实现 Apply**：在 `IPlugin.Apply(IContext ctx)` 中注册服务 / 菜单 / 工具函数 / 路由。
4. **数据读写**：用 `ctx.EnsurePluginDataDirectory()` 取得已建好的目录；库文件**必须**命名为 `{连接名}.db`（连接名即数据库名，与 XCode 一致）。XCode 插件：`connName` 必须在 `XCodeConfig.PluginDbs` 映射，实体 `ConnName` 与之同名，宿主自动 `DAL.Create`。
5. **构建发布**：`build.ps1` 会把 `Plugins/` 整体拷贝到 `publish/Plugins/`（保留），并排除宿主 `Data/Log`；`Plugins/` 目录本身不被 `git` 忽略，随仓库提交。
6. **验证**：启动后查 `~/.forgeself/Plugins/{id}/{连接名}.db` 是否生成、宿主日志是否无「插件目录不存在 / 加载失败 / 数据库初始化失败」。

---

## 八、常见坑（FAQ）

- **目录改名但 `Backend.csproj` 的 `ProjectReference` 路径没改** → 构建失败（找不到 `.csproj`）。改名须同步 13 处引用。
- **库文件名拼错**（如写成 `memory.db` 而非 `MemorySystem.db`）→ 生成第二份库，旧数据不可见；务必用 `{连接名}.db`。
- **`EntryType` 大小写 / 命名空间错** → `Type.GetType` 返回 `null` → 插件加载失败。
- **漏建数据父目录** → SQLite 抛「unable to open database file」；务必经 `EnsurePluginDataDirectory()` / 宿主 `InitializeXCodeDatabase`。
- **`Id` 含大写或点号** → 在 Linux 上数据目录名大小写敏感、前端路由解析异常；`Id` 只允许 kebab-case。
- **想"不重启宿主"就更新某个插件** → 用 `scripts/publish-plugin.ps1 -Plugin <目录名PascalCase>` 把新版本 staged 到 `_backups/{id}/{ver}/`，再 `POST /api/plugins/update/{id}` 触发宿主运行时版本比较 + 切 current + 卸载旧 ALC + 加载新 DLL + 刷新 MVC 端点；详细流程见第九节。

---

## 九、运行时热更新（不重启宿主）

> 本节描述**已实现**的"按插件单独发布、不重启宿主"能力。涵盖版本目录布局、三种触发方式、内部机制、失败回退与回滚。

### 9.1 版本目录布局（side-by-side）

每个插件在宿主运行根的 `Plugins/{id}/` 下，按如下结构存放：

```
Plugins/{id}/
  plugin.json           # 活动清单（PascalCase 字段）
  current               # 文本文件，存当前生效 semver，如 "1.2.0"（原子覆盖）
  versions/
    1.0.0/<entry.dll>   # 不可变版本快照
    1.1.0/<entry.dll>
    ...
  _backups/
    {id}/<ver>/...      # publish-plugin.ps1 staged 新版本的入口（详见 9.4）
```

宿主解析入口顺序：`versions/<current>/<entry>` → fallback 旧扁平布局 `<entry>`。**更新 = 把新版本 staged 到 `versions/<new>/`，把 `current` 指针改成新版本号（原子写）**。

### 9.2 三种触发方式

| 触发 | 入口 | 适用场景 |
|---|---|---|
| **HTTP API**（推荐，CI/手动） | `POST /api/plugins/update/{id}` | 自动化部署、精确控制触发时机 |
| **FileSystemWatcher**（自动） | 修改 `Plugins/{id}/versions/<new>/` 或 `current` 指针 | 开发期、保存即生效 |
| **`scripts/publish-plugin.ps1`** | `pwsh ./scripts/publish-plugin.ps1 -Plugin AIAgent` | CI 流水线、运维发布；脚本只负责"编译 + staged 复制"，运行时切换由宿主 API 触发 |

HTTP 端点清单（`Controllers/PluginController.cs`，路由前缀 `api/plugins`）：

| 方法 | 路径 | 作用 |
|---|---|---|
| `GET` | `/api/plugins` | 列出已加载插件（含 Version / 启用状态） |
| `GET` | `/api/plugins/updates` | 检查 `_backups/` 中可用更新 |
| `POST` | `/api/plugins/update/{id}` | 触发更新（版本比较 → 切 current → 卸载旧 ALC → 加载新 DLL → 刷新 MVC 端点） |
| `POST` | `/api/plugins/rollback/{id}` | 回滚到指定版本（body `{"version":"1.0.0"}`） |
| `POST` | `/api/plugins/enable/{id}` | 热启用已停用插件 |
| `POST` | `/api/plugins/disable/{id}` | 热停用插件（保留数据） |

### 9.3 内部机制（为什么"不重启"是可行的）

- **可收集 ALC 隔离**：每个插件用 `AssemblyLoadContext(name, isCollectible: true)` 加载；卸载时不污染宿主或其他插件。
- **端点动态刷新**：插件控制器以 `AssemblyPart` 注册进 `ApplicationPartManager`，结合 `IActionDescriptorChangeProvider.NotifyChange`，新端点**实时出现在路由表**，无需重启。
- **DI 子容器隔离**：插件服务挂可变子容器（`IPluginServiceRegistry`），卸载时仅摘除该插件索引。
- **DLL 文件锁处理**：`PluginAssemblyUnloader` 提供 `ForceCollect`（两轮 GC + 终结器）+ `TryOpenExclusive`（`FileShare.None` 探测句柄释放）+ `TryDeleteDirectory`（被占用就跳过、下轮重试），解决 Windows 下 DLL 句柄未释放导致覆盖失败。
- **原子指针切换 + 失败回退**：`WriteCurrentVersion` 用临时文件 + `Move` 覆盖（半截写入不可见）。新版加载失败时自动回退上一版本，旧版本目录保留供回滚。
- **API 监听（FileSystemWatcher）**：`PluginHotReloadWatcher` 监听整个 `Plugins/` 含子目录（忽略 `_backups`/`_trash`），300ms debounce 后调用 `ReloadPlugin`；保存文件即热重载。

### 9.4 单独发布脚本 `scripts/publish-plugin.ps1`

把"插件 csproj 编译产物"自动 staged 到 `Plugins/_backups/{id}/{ver}/` 的脚本。运行时切换由宿主 API 触发（脚本与运行时关注点分离）。

```bash
# 默认（dev 形态，PluginsRoot = 源 Plugins/_backups）
./scripts/publish-plugin.ps1 -Plugin AIAgent

# 指定发布形态的插件根
./scripts/publish-plugin.ps1 -Plugin AIAgent -PluginsRoot "D:/deploy/Plugins"

# 强制覆盖已存在的 staged
./scripts/publish-plugin.ps1 -Plugin AIAgent -Force

# 只打印不真改（CI 验证用）
./scripts/publish-plugin.ps1 -Plugin AIAgent -DryRun
```

**参数约定**：`Plugin` 是**目录名**（PascalCase，如 `AIAgent`），不是 kebab-case id。脚本从 `plugin.json` 读 `Id`（`ai-agent`）用于构建 staged 路径。`Configuration` 默认 `Release`。

**幂等行为**：若 `_backups/{id}/{ver}/` 已存在且 `-Force` 未传，脚本直接退出 0（不重跑 publish、不覆盖）—— 配合宿主运行时"版本未变不更新"语义（`PluginVersionService.UpdatePlugin` 内已实现 `VersionComparer.Compare(latestVersion, metadata.Version) <= 0` 跳过逻辑）。**真正版本增加**才会触发宿主运行时切 current 与热重载。

**典型流程**：
1. 改代码 + 改 `Plugins/AIAgent/plugin.json` 的 `Version`（如 `1.0.0` → `1.1.0`）；
2. 跑 `publish-plugin.ps1 -Plugin AIAgent`（编译 + staged）；
3. 宿主页运行中 → `curl.exe -X POST http://localhost:7102/api/plugins/update/ai-agent`（版本比较 → 切 current → 热重载）；
4. 或停止宿主 → 重启时自动加载 `versions/1.1.0/`。

### 9.5 失败回退与回滚

- **加载失败自动回退**：`ReloadPlugin` 捕获异常后回退 `current` 到上一可用版本并重载。
- **手动回滚**：`POST /api/plugins/rollback/{id}` + body `{"version":"1.0.0"}`（只要 `versions/1.0.0/` 目录仍在）。
- **硬卸载**（清数据）：删除 `publish/Plugins/{目录}/` 与 `~/.forgeself/Plugins/{id}/`。

---

## 十、插件自带界面（web/）

> 本节描述**已实现**的"插件前端"约定：插件目录下用 `web/` 存放自带界面（前端源码 + 构建产物），
> 由宿主在运行时远程加载并渲染，宿主**不重新构建**即可展示（一次发布前后端同更）。详规见
> `specs/010-plugin-frontend-runtime/`（fr-010 契约 + 目录设计）。试点实现：`Plugins/AIAgent/web/`。

### 10.1 目录布局（源码 / 产物分离）

```text
Plugins/{Plugin}/
├── plugin.json                 # 清单（固定位置，含 frontend 声明）
├── {Plugin}.csproj
├── …（后端源码）
└── web/                        # ★ 前端唯一归属
    ├── src/                    # 前端源码（.vue / .ts）—— 不对外提供
    ├── dist/                   # 构建产物（ESM，入口 index.js）—— 唯一对外
    ├── package.json
    └── vite.config.ts
```

- `web/src/` 是**源码**，绝不由宿主直接提供；`web/dist/` 是**构建产物**，由 `PluginFrontendFileMiddleware`
  以只读方式暴露到 `GET /plugins/{插件id}/web/dist/{资源路径}?v={版本}`。对外只达 `dist/`，后端 DLL、
  `plugin.json`、`web/src/` 一律不可达（含 `..`/反斜杠穿越防护 + 未归一化越界二次校验 + 未知扩展名拒绝）。

### 10.2 Entry 声明方式

在 `plugin.json` 的 `frontend`（camelCase）对象中声明 `"entry"`，指向**相对插件根目录**的界面入口
（与 AIAgent 实际清单一致，`plugin.json` 序列化采用 camelCase）：

```json
{
  "Id": "ai-agent",
  "Name": "AI代理插件",
  "Version": "1.2.4",
  "frontend": {
    "views": ["AiAgentView"],
    "menu": "AI Agent",
    "route": "/ai-agent",
    "icon": "fa-robot",
    "entry": "web/dist/index.js"
  }
}
```

- `entry` 是可选字段；缺省（如 MemorySystem / QuickLinks / TodoTracker）时宿主回退既有硬编码映射，零回归。
- 前端清单接口 `GET /api/plugin/frontend-manifest` 会把 `Entry` 一并下发，前端 `pluginViewLoader` 据其拼装
  资源 URL 动态 `import()`。

### 10.3 共享依赖 external 清单（杜绝 Vue 双实例）

前端构建配置（`web/vite.config.ts`）**必须**把以下共享依赖全部 `external`，保留**裸导入**，
由宿主页面 `/plugins/*` 资源外的 `importmap` 解析到宿主**同一份**实例：

- `vue` / `vue-router` / `pinia` / `element-plus`

产物校验：`web/dist/index.js` 应只保留 `from "vue"` 之类裸导入，**不**内联上述依赖的任何实现。
否则会出现 Vue 双实例导致响应式失效。试点产物已验证符合（仅 `from "vue"` 裸导入）。

### 10.4 发布只带产物（不含源码）

`scripts/publish-plugin.ps1` 把 `web/dist/`（**不含** `web/src/` 与 `node_modules/`）复制进 staged 目录，
保证分发单元精简且不泄露插件源码。宿主前端加载插件界面时走 `importmap` 解析共享依赖，
不重复打包、不引入宿主 `@/` 别名（插件 bundle 无法解析别名，数据一律走 HTTP/后端接口）。
