# 后端服务参考

> 后端宿主 Services 层职责、生命周期、依赖关系速查。
> 插件内部 Services 见各插件代码（`Plugins/*/Services/`）。
> 最后更新：2026-09-29（dsh 对齐 B1–B9 收官后同步：会话事件溯源/Turn-Step 运行时/工具管线六闸门/持久化双实现/数据根重载）

## 服务分类

### AI 相关

| 服务类 | 接口 | 职责 | 依赖 |
|--------|------|------|------|
| `AIService` | `IAIService` | LLM 聊天请求（同步/流式），统一调用上游 API | `IConfigurationService`, `ILogService`, `HttpClient` |
| `AIServiceLlmRuntime` | `ILlmRuntime` | 适配器：将 `IAIService` 包装为 Cordis 能力接缝 `ILlmRuntime`（tool 角色消息带 `CallId`、`Usage`/`FinishReason` 透传） | `IAIService` |
| `AIProviderService` | `IAIProviderService` | AI 提供方 CRUD、默认提供方管理、测试连接 | `IAIProviderRepository`, `ISecretEncryptionService` |
| `AIModelService` | `IAIModelService` | 模型列表管理、按提供方同步、启用/禁用 | `IAIProviderRepository`, `ISecretEncryptionService`, `IHttpClientFactory`, `ILogService` |
| `AIProviderRepository` | `IAIProviderRepository` | AI 提供方数据访问（含 ApiKey 加解密） | `ISecretEncryptionService` |
| `ApiKeyService` | — | API 子密钥派生/轮换/解析 | `ISecretEncryptionService` |

### 聊天与会话

| 服务类 | 接口 | 职责 | 依赖 |
|--------|------|------|------|
| `ChatSessionService` | `IChatSessionService` | 会话聚合根 CRUD、轮次统计更新（会话列表过滤 `plan:%` 规划会话） | `ILogService` |
| `ChatTurnService` | `IChatTurnService` | 聊天轮次保存/查询 | `ILogService` |
| `ChatTurnStreamRecorder` | `IChatTurnStreamRecorder` | 流式响应录制（逐块写入 ChatTurn） | `IChatTurnService`, `IWebSocketBroadcaster` |
| `MessageService` | `IMessageService` | 旧版聊天消息读写（`ChatMessage` 表，已降为投影——唯一 prompt 来源见 `SessionProjectionService`） | `ILogService` |
| `ChatSessionResolver` | —（静态类） | 会话键解析工具（`ResolveConversationKey`） | 无 |

### 会话事件溯源（dsh 040，B1–B4）

| 服务类 | 接口 | 职责 | 依赖 |
|--------|------|------|------|
| `PersistentSessionStore` | `ISessionStore` | 会话事件持久化（Sqlite/XCode，`Append`/`Replay`）；默认注册（内存版 `InMemorySessionStore` 保留为测试/回退实现） | XCode |
| `PersistentInbox` | `IInbox` | 收件箱持久化（followup/steer/inject 三通道，跨重启）；默认注册（内存版 `InMemoryInbox` 保留） | XCode |
| `SessionProjectionService` | —（Scoped） | 从 `Replay` 幂等全量重投影：事件日志 → `ChatMessage` 表（`SyncAsync`，写路径唯一 prompt 来源闭环） | `ISessionStore`, `IMessageService` |
| `SessionEventProjection` | —（静态类） | 事件序列 → 模型可见消息投影（内存/持久化两实现共用契约）；大工具结果 spill（`SpillThresholdBytes` 默认 32KiB，配置中心可调） | 无 |
| `SessionEventSubject` | — | 会话事件进程内发布（live 事件不落盘） | 无 |
| `SessionEventJsonConverter` | —（类型转换器） | `SessionEvent` 多态序列化（13 子类判别） | 无 |

### Turn/Step 运行时与工具管线（dsh 041/042，B5–B8）

| 服务类/类型 | 接口 | 职责 | 依赖 |
|--------|------|------|------|
| `ToolRegistry` | `IToolRegistry` | 工具注册/查询 + **六闸门执行面**（`ExecuteAsync`/`ExecuteBatchAsync`：pre-execute 三态 → 单调守卫 → execute waterfall → post-execute waterfall → finalize 恰好一次 → result 冻结快照） | `IServiceProvider?`, `IEventBus?`, `IToolGuardRegistry`, `IApprovalService` |
| `ToolGuardRegistry` | `IToolGuardRegistry` | 工具守卫注册（落 `ForgeSelf.Abstractions`；只减不增，AddGuard 返回 IDisposable） | 无 |
| `NoopApprovalService` | `IApprovalService` | 审批接缝默认实现：`AskAsync` 恒 false（fail-closed，Ask 决策无审批即 Denied） | 无 |
| `ToolPipeline`（契约，Abstractions） | — | `PreToolDecision`（Allow/Deny/Ask 三态）/`PostToolDecision`/`ToolExecution` 等管线契约类型 | 无 |
| `ReactLoopAgent` | `IAgent`（AIAgent 插件） | ReAct 状态机：turn/step 帧、`ExecuteBatchAsync` 调度（model-ordered commit、取消合成 Skipped）、出口工具分区、看门狗、收件箱三语义（插件侧，详见 dsh 三部曲） | `ILlmRuntime`, `IToolRegistry`, `IInbox` |
| `AgentRuntimeRegistry` | `IAgentRegistry`（AIAgent 插件） | Agent 实例注册表（按会话解析 loop 运行时） | 无 |

### 配置

| 服务类 | 接口 | 职责 | 依赖 |
|--------|------|------|------|
| `ConfigurationService` | `IConfigurationService` | 读取 `appsettings.json` 各节配置 | `IConfiguration` |
| `LogService` | `ILogService` | 日志记录（包装 XTrace） | 无 |
| `PortConfigurationService` | `IPortConfigurationService` | 端口配置读写 | `IPortAvailabilityService` |
| `PortAvailabilityService` | `IPortAvailabilityService` | 端口可用性检测 | 无 |
| `ApiServerKeyService` | — | API 服务器密钥管理 | `ISecretEncryptionService` |
| `DataLocationService` | `IDataLocationService` | 数据根解析：`FORGESELF_DATA_ROOT` 环境变量**最前置**重载（静态版/实例版双解析语义一致）→ Development 程序目录 `data/` → 用户目录 `~/.forgeself` | `IWebHostEnvironment` |
| `ConfigUnifier` | —（静态类） | 启动期把框架/项目 `Config<T>` 配置文件统一收敛到数据根 `config/`（三层降级：目录不可建/逐类重定向/Save 失败均告警不抛，不炸启动） | 无 |

### 更新与维护

| 服务类 | 接口 | 职责 | 依赖 |
|--------|------|------|------|
| `StagedUpdateService` | — | 唯一更新执行链路（stage → 应用，008 `UpdateService` 已冻结仅保留查询口径） | `UpdateChecker`, `UpdateConfig` |
| `UpdateService` | — | 自动更新检查（历史链路，已冻结——执行走 `StagedUpdateService`） | `UpdateChecker`, `IServiceManager`, `UpdateConfig`, `ServiceConfig`, `HttpClient` |
| `UpdateChecker` | — | GitHub Release/Gitee/本地目录 检查、更新包下载 | `UpdateConfig`, `HttpClient` |
| `UpdateSettingsService` | `IUpdateSettingsService` | 更新源运行时可变配置 | `IConfigurationService` |
| `SecretMigrationService` | — | 密钥明文迁移/加密改造 | `ISecretEncryptionService` |
| `ApplicationRestartService` | `IApplicationRestartService` | 应用重启（通过命名管道通知托盘） | `ILogger<ApplicationRestartService>`, `IHostApplicationLifetime` |
| `ServiceManager` | `IServiceManager` | Windows 服务安装/卸载/启停 | `ServiceConfig` |
| `ServiceControllerWrapper` | `IServiceController` | Windows 服务控制器封装 | `serviceName`（构造参数） |
| `TrayIconManager` | — | 托盘图标管理（显示/隐藏/气泡通知） | `IServiceManager`, `port`, `appName`, `appVersion`, `copyright` |
| `TrayProcessStarter` | —（静态类） | 启动托盘进程 | 无 |
| `ServicePipeServer` | — | 命名管道服务器（接收托盘进程的关闭命令） | `pipeName`（构造参数） |

### 插件系统

| 服务类 | 接口 | 职责 | 依赖 |
|--------|------|------|------|
| `PluginServiceRegistry` | `IPluginServiceRegistry` | 可变 MS DI：每插件子容器挂载/卸载/转发 | 无 |
| `PluginUpdateSettingsService` | — | 插件更新源（本地包目录 LocalDir）配置 | `IConfigurationService` |
| `PluginVersionService` | —（Plugins/Services） | 插件版本化侧载（stage → versions/<ver>/ + current 指针）、本地包扫描 | `PluginUpdateSettingsService` |
| `PluginInstallerService` | —（Plugins/Services） | 插件安装/更新（版本化重写，无 `_backups`） | `PluginVersionService` |
| `MvcActionDescriptorChangeProvider` | `IActionDescriptorChangeProvider` | 动态端点移除的 ActionDescriptor 刷新通知 | 无 |

### 运行时

| 服务类 | 接口 | 职责 | 依赖 |
|--------|------|------|------|
| `RuntimeDetector` | `IRuntimeDetector` | 运行时环境检测（OS/架构/进程信息） | 无 |
| `CronParser` | `ICronParser` | Cron 表达式解析（有效性/下次执行时间） | 无 |
| `HostProjectRegistry` | `IProjectRegistry` | 宿主工程注册表 | 无 |
| `WebSocketBroadcaster` | `IWebSocketBroadcaster` | WebSocket 连接管理与广播 | 无 |

### Cordis 能力接缝（持久化默认 + 内存实现并存）

| 服务类 | 接口 | 职责 | 依赖 |
|--------|------|------|------|
| `PersistentInbox` | `IInbox` | **默认注册**：收件箱持久化（XCode）；`InMemoryInbox` 保留为测试/回退实现 | XCode |
| `PersistentSessionStore` | `ISessionStore` | **默认注册**：会话事件持久化（XCode）；`InMemorySessionStore` 保留为测试/回退实现 | XCode |

> 注：`IAgentLoop`（旧代理循环接口）与 `ToolCallContext`（旧工具调用上下文）已在 dsh B5/B9 退役删除——执行面由 `IAgent`/`IAgentRegistry`（ReactLoopAgent）与 `ToolExecution` 六闸门取代，见 `02-features/027-cordis-kernel.md` 与 dsh 三部曲。

## 服务注册位置

| 注册方式 | 位置 | 适用服务 |
|----------|------|----------|
| 宿主 DI（`AddSingleton`/`AddScoped`） | `AppBuilder.cs` | AI 相关、聊天、配置、更新、运行时服务 |
| 宿主 DI（`AddSingleton`） | `AppBuilder.cs`（L144-168） | 能力接缝默认实现：`IToolGuardRegistry`/`IApprovalService`/`IToolRegistry`/`ISessionStore`(Persistent)/`IInbox`(Persistent) |
| 宿主 DI（`AddScoped`） | `AppBuilder.cs`（L161-162） | `SessionProjectionService`、`ILlmRuntime`(AIServiceLlmRuntime) |
| 插件子容器 | 各插件 `Apply(IContext)` | 插件内部 Services（AIAgent 的 ReactLoopAgent/AgentRuntimeRegistry 等） |
| Cordis 共享表（`ctx.Register<T>`） | `PluginManager.ProvideHostServices` | 宿主 seed 契约（IConfigurationService/IToolRegistry 等） |

## 生命周期说明

- **Singleton**：AIProviderService、ConfigurationService、LogService、PluginServiceRegistry、ToolRegistry、ToolGuardRegistry、IApprovalService(Noop)、PersistentSessionStore、PersistentInbox、WebSocketBroadcaster 等
- **Scoped**：ChatSessionService、ChatTurnService、MessageService、SessionProjectionService、ILlmRuntime（每请求新建）
- **Cordis eager 单例**：通过 `ctx.Register<T>(instance)` 注册的服务（如 IWorkflowAIAdvisor），在插件 Apply 阶段构造，随 Fiber 卸载销毁
