# 12-faq — 常见问题

> 状态：已补充（2026-08-20）
> 最后更新：2026-08-20

## 通用

**Q：后端默认端口是多少？前端呢？**
A：后端 `ForgeSetting.PortNumber = 7102`（运行期可改，重启生效）；前端 dev `7002`。AGENTS.md 旧注 7300/7380 已过时。

**Q：AI Provider 的 ApiKey 会明文返回吗？**
A：不会。列表/详情只返回掩码（`sk-****3456`），明文仅在解密后用于上游请求。

**Q：改了 VisionModel 等配置没生效？**
A：运行实例无权限停止时，需手动重启后端 exe 才生效。

## 聊天与会话

**Q：为什么聊天记录里 app 聊天和代理录制能合并到一个会话列表？**
A：`ChatSession` 是统一聚合根，app 聊天（`ChatController`）与代理录制（`UnifiedAI/*`）都 upsert 到它，靠 `SessionKey`+`Source` 区分。详见 `02-features/010-chat-session-aggregation.md`。

**Q：多模态请求为什么会省 token？**
A：`MultimodalProcessor` 对同一会话内相同图片（同视觉模型）缓存识别结果到本地文件，命中复用、失败不缓存。详见 `02-features/011-multimodal-image-cache.md`。

## Cordis 插件架构

**Q：插件架构整改后，还用 MS DI 吗？两者什么关系？**
A：**都用，各管各的**。MS DI 管"每个插件内部怎么组装自己的服务"（Scoped/Transient 生命周期），Cordis Context 管"插件之间怎么互通"（共享服务表）。两者通过 `SetHostProvider` 桥接——`ctx.Get<T>()` 在共享表找不到时，自动回落去 MS DI 容器找宿主平台服务（配置/日志等）。插件只管调 `ctx.Get<T>()`，不需要知道背后是共享表还是 MS DI。

**Q：插件间可以共享服务吗？兄弟插件可以互相引用吗？**
A：**可以共享服务，但不能直接引用对方的程序集或类**。提供方在 `Apply(IContext)` 阶段 `ctx.Register<T>(实例)` 写入 root 共享服务表；消费方运行时 `ctx.Get<T>()` 获取。双方只能通过 `Abstractions` 程序集中定义的接口契约交互，不能直接引用对方 DLL。

**Q：两个插件都提供相同的服务，谁生效？**
A：**后注册的覆盖先注册的**。Cordis 的 `provide()` 语义就是后声明覆盖先声明，这允许一个插件替换另一个插件的能力。但 Fiber 卸载时，`SharedServiceRemoval` 只摘除自己当初注册的实例，不会误删后来覆盖的同名服务。

**Q：一个服务在这个版本有，下个版本就没了，消费方怎么办？**
A：**消费方必须做 null 检查 + 降级处理**。这是 Cordis 的硬性约定——所有跨插件服务都是"软依赖"：
- 每次用每次 `ctx.Get<T>()`，不缓存实例到字段
- null 表示服务不存在，走默认逻辑（不是错误）
- 提供方不保证永久存在，消费方不假定提供方存在
- 这样提供方升级/卸载时，消费方自动降级，不需要改代码

**Q：能力接缝是什么意思？**
A：就是**接口（interface）**，也叫服务契约或 SPI（Service Provider Interface）。"接缝"（seam）是软件设计术语，指"不改动调用方代码就能换实现的地方"。把功能定义成接口，以后想换实现写一个新类就行，调用方不用动。例如 `ILlmRuntime` 定义 LLM 调用能力，宿主提供默认实现，插件也可以提供自己的实现替换它。

**Q：插件依赖另一个插件的能力，但宿主没有在 Abstractions 中声明接口怎么办？**
A：分两种情况：
- **推荐做法**：接口定义在 `Abstractions` 程序集中，双方编译期引用，有类型安全和 IDE 智能提示。本项目当前所有插件间共享接口都走这条路。
- **Cordis 原生做法**：通过 `plugin.json` 的 `provides`/`consumes` 声明依赖关系，PluginManager 按拓扑排序确保加载顺序。`PluginMetadata` 已预留 `Provides`/`Consumes` 字段，但自动重启机制尚未实现，等出现实际需求时落地。

**Q：`ctx.Get<T>()` 的解析顺序是什么？**
A：`Context.GetService` 内部按以下顺序查找，找到即返回：
1. **本地字典**（`RegisterLocal` 注册的值，仅当前上下文可见）
2. **root 共享服务表**（`Register` 注册的全局服务，兄弟插件可见）
3. **宿主 MS DI 容器**（`SetHostProvider` 桥接的平台服务）
找不到返回 null。

**Q：插件内部的服务注册用 MS DI 还是 Cordis Context？**
A：**插件内部自己的服务用 MS DI 子容器**（`services.AddScoped<T>()`），方便构造注入和生命周期管理。**要暴露给兄弟插件的服务用 `ctx.Register<T>(实例)`** 写入共享表。框架私有对象（`PluginMetadata`/`IServiceCollection`）用 `ctx.RegisterLocal<T>()` 注册为本地值，不进共享表。

**Q：插件热更新时，正在被其他插件消费的服务怎么办？**
A：提供方 Fiber 卸载时，`ctx.Effect` 逆序执行，自动从共享表摘除该插件注册的所有服务。消费方下次 `ctx.Get<T>()` 得到 null，走降级逻辑。消费方如果缓存了旧实例（禁止的做法），会拿到已 Dispose 的对象。所以**禁止缓存 `ctx.Get<T>()` 的结果到字段**。

**Q：`Register` 和 `RegisterLocal` 有什么区别？**
A：`Register<T>(实例)` = **全局服务**，写入 root 共享服务表，所有兄弟 Fiber 可见，对标 Cordis 的 `provide()`。`RegisterLocal<T>(实例)` = **本地值**，写入当前 Context 的本地字典，不进共享表，兄弟插件不可见，对标 Cordis 的直接赋值非声明属性。`PluginMetadata` 和 `IServiceCollection` 这类插件框架私有对象应该用 `RegisterLocal`。
