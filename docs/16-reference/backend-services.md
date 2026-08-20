# 后端服务参考

> 后端宿主 Services 层职责、生命周期、依赖关系速查。
> 插件内部 Services 见各插件代码（`Plugins/*/Services/`）。
> 最后更新：2026-08-20

## 服务分类

### AI 相关

| 服务类 | 接口 | 职责 | 依赖 |
|--------|------|------|------|
| `AIService` | `IAIService` | LLM 聊天请求（同步/流式），统一调用上游 API | `IConfigurationService`, `ILogService`, `HttpClient` |
| `AIServiceLlmRuntime` | `ILlmRuntime` | 适配器：将 `IAIService` 包装为 Cordis 能力接缝 `ILlmRuntime` | `IAIService` |
| `AIProviderService` | `IAIProviderService` | AI 提供方 CRUD、默认提供方管理、测试连接 | `IAIProviderRepository`, `ISecretEncryptionService` |
| `AIModelService` | `IAIModelService` | 模型列表管理、按提供方同步、启用/禁用 | `IAIProviderRepository`, `ISecretEncryptionService`, `IHttpClientFactory`, `ILogService` |
| `AIProviderRepository` | `IAIProviderRepository` | AI 提供方数据访问（含 ApiKey 加解密） | `ISecretEncryptionService` |

### 聊天与会话

| 服务类 | 接口 | 职责 | 依赖 |
|--------|------|------|------|
| `ChatSessionService` | `IChatSessionService` | 会话聚合根 CRUD、轮次统计更新 | `ILogService` |
| `ChatTurnService` | `IChatTurnService` | 聊天轮次保存/查询 | `ILogService` |
| `ChatTurnStreamRecorder` | `IChatTurnStreamRecorder` | 流式响应录制（逐块写入 ChatTurn） | `IChatTurnService`, `IWebSocketBroadcaster` |
| `MessageService` | `IMessageService` | 旧版聊天消息 CRUD（`ChatMessage` 表） | `ILogService` |
| `ChatSessionResolver` | —（静态类） | 会话键解析工具（`ResolveConversationKey`） | 无 |

### 配置

| 服务类 | 接口 | 职责 | 依赖 |
|--------|------|------|------|
| `ConfigurationService` | `IConfigurationService` | 读取 `appsettings.json` 各节配置 | `IConfiguration` |
| `LogService` | `ILogService` | 日志记录（包装 XTrace） | 无 |
| `PortConfigurationService` | `IPortConfigurationService` | 端口配置读写 | `IPortAvailabilityService` |
| `PortAvailabilityService` | `IPortAvailabilityService` | 端口可用性检测 | 无 |
| `ApiServerKeyService` | — | API 服务器密钥管理 | `ISecretEncryptionService` |

### 更新与维护

| 服务类 | 接口 | 职责 | 依赖 |
|--------|------|------|------|
| `UpdateService` | — | 自动更新检查与执行、回滚 | `UpdateChecker`, `IServiceManager`, `UpdateConfig`, `ServiceConfig`, `HttpClient` |
| `UpdateChecker` | — | GitHub Release 检查、更新包下载 | `UpdateConfig`, `HttpClient` |
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
| `ToolRegistry` | `IToolRegistry` | 工具函数注册/查找/执行（含事件总线钩子） | `IServiceProvider?`, `IEventBus?` |
| `MvcActionDescriptorChangeProvider` | `IActionDescriptorChangeProvider` | 动态端点移除的 ActionDescriptor 刷新通知 | 无 |

### 运行时

| 服务类 | 接口 | 职责 | 依赖 |
|--------|------|------|------|
| `RuntimeDetector` | `IRuntimeDetector` | 运行时环境检测（OS/架构/进程信息） | 无 |
| `CronParser` | `ICronParser` | Cron 表达式解析（有效性/下次执行时间） | 无 |
| `WebSocketBroadcaster` | `IWebSocketBroadcaster` | WebSocket 连接管理与广播 | 无 |
| `ToolCallContext` | —（数据类） | 工具调用上下文（ToolName/Parameters/Result 等） | 无 |

### Cordis 能力接缝（InMemory 实现）

| 服务类 | 接口 | 职责 | 依赖 |
|--------|------|------|------|
| `InMemoryInbox` | `IInbox` | 内存收件箱（GetMessages/Clear/Followup/Steer/Inject） | 无 |
| `InMemoryAgentLoop` | `IAgentLoop` | 内存代理循环（RunAsync） | 无 |
| `InMemorySessionStore` | `ISessionStore` | 内存会话存储（Append/Replay/DeriveMessages） | 无 |

## 服务注册位置

| 注册方式 | 位置 | 适用服务 |
|----------|------|----------|
| 宿主 DI（`AddSingleton`/`AddScoped`） | `AppBuilder.cs` | AI 相关、聊天、配置、更新、运行时服务 |
| 宿主 DI（`AddSingleton`） | `AppBuilder.cs` | Cordis 能力接缝（InMemory*） |
| 插件子容器 | 各插件 `Apply(IContext)` | 插件内部 Services |
| Cordis 共享表（`ctx.Register<T>`） | `PluginManager.ProvideHostServices` | 宿主 seed 契约（IConfigurationService/IToolRegistry 等） |

## 生命周期说明

- **Singleton**：AIProviderService、ConfigurationService、LogService、PluginServiceRegistry、ToolRegistry、WebSocketBroadcaster 等
- **Scoped**：ChatSessionService、ChatTurnService、MessageService（每请求新建）
- **Transient**：ToolCallContext（数据类）
- **Cordis eager 单例**：通过 `ctx.Register<T>(instance)` 注册的服务（如 IWorkflowAIAdvisor），在插件 Apply 阶段构造，随 Fiber 卸载销毁
