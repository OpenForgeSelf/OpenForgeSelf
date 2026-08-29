# 插件体系（Plugins）

> 本文件是插件开发的权威规范。涵盖一个插件从「诞生 → 发布 → 发现 → 加载 → 应用 → 数据落盘 → 停用」的完整生命周期（前世今生）、命名规范（两层身份模型）、数据落盘约定，以及如何新建插件。
> 代码层（目录/程序集/EntryType）刻意使用 C# 原生 PascalCase，运行时层（Id/数据目录/路由/库文件）统一 kebab-case —— 这是**刻意设计而非不一致**，详见第四节。

---

## 一、设计用意（Why）

- **宿主只提供容器，不内置业务**：`OpenForgeSelf.Backend` 仅提供运行时容器与扩展点契约（`IPlugin`/`IContext` 及各类 `I*Extension`），所有业务功能（记忆、抓包、定时、脚本、待办……）都以插件形式热插拔。
- **插件自包含**：每个插件 = 一个目录 + 一个程序集 `.dll` + 一份 `plugin.json`，可独立开发、发布、禁用。
- **数据隔离**：每个插件拥有独立数据目录与独立 SQLite 库文件，互不串库、互不污染宿主库。

---

## 二、一个插件的前世今生（生命周期）

1. **诞生（Scaffold）**：用 `PluginScaffolderService.Create(pluginId)` 生成骨架（目录 / `plugin.json` / `.csproj` / 示例 `IPlugin` 类），`pluginId` 自动按 kebab-case 产出。
2. **注册（Reference）**：在 `OpenForgeSelf.Backend.csproj` 添加 `<ProjectReference>` 指向 `Plugins/{目录}/{目录}.csproj`；构建/发布时其产物被拷贝到 `publish/Plugins/{目录}/`。
3. **发现（Discover）**：宿主启动时 `PluginManager` 扫描 `publish/Plugins/*/plugin.json`，读取 `Id` / `EntryAssembly` / `EntryType`。
4. **加载（Load）**：经独立 `AssemblyLoadContext` 加载 `EntryAssembly`，再用 `Type.GetType(EntryType)` 反射出实现 `IPlugin` 的入口类。
5. **应用（Apply）**：宿主 `Build()` 之后调用 `plugin.Apply(ctx)`。插件在此注册服务、`IMenuExtension` 菜单、`IToolFunctionExtension` 工具函数、控制器路由等。
6. **运行（Run）**：控制器 / 工具函数经 DI 拿到插件服务实例；所有数据写入各自的数据目录（见第三节）。
7. **数据落盘（Persist）**：见第三节 —— 库文件统一命名 `{插件Id}.db`，落在 `{数据根}/Plugins/{插件Id}/`。
8. **停用 / 移除（Unload）**：当前为发布期**静态加载**（非运行时热拔）；删除 `publish/Plugins/{目录}/` 即卸载该插件（其数据目录 `~/.forgeself/Plugins/{插件Id}/` 保留，可手动清理）。

---

## 三、数据落盘规范（Data Landing）

- **数据根（Data Root）**：由 `IDataLocationService` 解析。
  - 开发态（`BaseDirectory` 含 `Debug`/`Release`）：`{BaseDirectory}/Data/`
  - 发布 / 服务态：`%USERPROFILE%/.forgeself/`
- **插件库路径（统一）**：`{数据根}/Plugins/{插件Id}/{插件Id}.db`
  - 例：`memory-system` → `~/.forgeself/Plugins/memory-system/memory-system.db`
- **父目录自建**：SQLite 不会自动创建父目录，宿主在 `AddXCode` / `InitializeXCodeDatabase` 时先 `EnsureDirectory`，插件侧也可用 `ctx.EnsurePluginDataDirectory()` 取得已建好的目录。
- **库文件名铁律**：一律 `{插件Id}.db`（kebab，与 Id 同名），禁止任意命名（历史 `memory.db` / `capture.db` / `QuickLinks.db` 等混用写法已全部修正）。改名会生成第二份库，旧数据不可见。

---

## 四、命名规范（两层身份模型）

> **这是「刻意设计」而非「不一致」**。务必先理解两层，再写代码或建目录。

| 身份层 | 作用域 | 命名风格 | 能否改 | 原因 |
|---|---|---|---|---|
| **代码身份** | 目录名、程序集 `.dll`、 `plugin.json` 的 `EntryType`（=`Namespace.PluginClass`） | C# 原生 **PascalCase**（`AIAgent` / `OpenForgeSelf.Backend.Plugins.MemorySystem.MemorySystemPlugin`） | 不可 | `EntryType` 须经反射 `Type.GetType("Namespace.Class")`，强制 PascalCase；且须符合 C# 命名约定 |
| **运行时身份** | `plugin.json` 的 `Id`、数据目录名、`~/.forgeself/Plugins/{id}`、前端路由、库文件名 `{id}.db` | **kebab-case**（全小写 + 短横线，`^[a-z0-9]+(-[a-z0-9]+)*$`） | 不可 | `Id` 会用作**目录名**（Linux 大小写敏感）与**前端路由**，kebab 最稳、最不易混淆 |

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
| `AIAgent` | `ai-agent` | `ai-agent.db` | XCode（`PluginDbs`） |
| `DevTools` | `dev-tools` | （无持久库） | — |
| `FileTools` | `file-tools` | （无持久库） | — |
| `MemorySystem` | `memory-system` | `memory-system.db` | XCode（`Memory`/`MemoryCategory` 实体）+ EF（`Memories`/`MemoryCategories`，同文件异表） |
| `ProxyCapture` | `proxy-capture` | `proxy-capture.db` | EF（`ProxyCaptureDbContext`） |
| `QuickLinks` | `quick-links` | `quick-links.db` | XCode（`PluginDbs`） |
| `SamplePlugin` | `sample` | （无持久库） | — |
| `Scheduler` | `scheduler` | `scheduler.db` | XCode（`PluginDbs`） |
| `ScriptRunner` | `script-runner` | `script-runner.db` | XCode（`PluginDbs`） |
| `SystemMonitor` | `system-monitor` | （无持久库） | — |
| `TextTools` | `text-tools` | （无持久库） | — |
| `TodoTracker` | `todo-tracker` | `todo-tracker.db` | XCode（`PluginDbs`） |
| `WorkflowEngine` | `workflow-engine` | `workflow-engine.db` | XCode（`PluginDbs`） |

> 注：`MemorySystem` 同时有 XCode 实体（`Memory`/`MemoryCategory` 表）与 EF 上下文（`Memories`/`MemoryCategories` 表），二者**同落 `memory-system.db` 但表名不同、互不冲突**。XCode 管 CRUD（`MemoryServiceXCode`），EF 管 AI 集成抽取（`MemoryIntegrationService` / 工具函数）。

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
2. **登记引用**：在 `OpenForgeSelf.Backend.csproj` 添加 `<ProjectReference Include="Plugins/YourPlugin/YourPlugin.csproj" />`（目录名 PascalCase，与程序集一致）。
3. **实现 Apply**：在 `IPlugin.Apply(IContext ctx)` 中注册服务 / 菜单 / 工具函数 / 路由。
4. **数据读写**：用 `ctx.EnsurePluginDataDirectory()` 取得已建好的目录；库文件**必须**命名为 `{id}.db`（XCode 用 `DAL.Create("YourConnName")` 且 connName 在 `XCodeConfig.PluginDbs` 映射；EF 用 `UseSqlite("Data Source={dir}/{id}.db")`）。
5. **构建发布**：`build.ps1` 会把 `Plugins/` 整体拷贝到 `publish/Plugins/`（保留），并排除宿主 `Data/Log`；`Plugins/` 目录本身不被 `git` 忽略，随仓库提交。
6. **验证**：启动后查 `~/.forgeself/Plugins/{id}/{id}.db` 是否生成、宿主日志是否无「插件目录不存在 / 加载失败 / 数据库初始化失败」。

---

## 八、常见坑（FAQ）

- **目录改名但 `Backend.csproj` 的 `ProjectReference` 路径没改** → 构建失败（找不到 `.csproj`）。改名须同步 13 处引用。
- **库文件名拼错**（如写成 `memory.db` 而非 `memory-system.db`）→ 生成第二份库，旧数据不可见；务必用 `{id}.db`。
- **`EntryType` 大小写 / 命名空间错** → `Type.GetType` 返回 `null` → 插件加载失败。
- **漏建数据父目录** → SQLite 抛「unable to open database file」；务必经 `EnsurePluginDataDirectory()` / 宿主 `InitializeXCodeDatabase`。
- **`Id` 含大写或点号** → 在 Linux 上数据目录名大小写敏感、前端路由解析异常；`Id` 只允许 kebab-case。
