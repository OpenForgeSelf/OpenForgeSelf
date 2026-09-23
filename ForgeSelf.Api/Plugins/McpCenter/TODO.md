# MCP 中心（mcp-center）插件待办

> 本文件记录 MCP 中心的**剩余问题与未尽事宜**（含设计未决项与核查发现）。
> 验收/评审「是否完备可提交」时对照 `.agents/skills/plugin-development/references/plugin-acceptance.md` 逐项勾选。
> 条目格式：`- [ ] <事项>（P<优先级>，来源）`；完成即移除，不留 ✅ 堆积（AGENTS §7.5）。

## 当前状态

- 版本：v2.1.0（MCP 中心：宿主工具网关 + 外部 MCP 客户端 + 服务器/工具管理 + 网关配置）
- 对外协议：MCP 2.0 全传输格式（stdio / Streamable HTTP / 旧版 HTTP+SSE）
- 对外工具：1 个万能工具 `universal_tool`（+ 枚举工具 `list_tools` 经其转发）

## ⬜ 待办

- [ ] **生产网关令牌为空**（`C:\Users\12504\.forgeself\Plugins\mcp-center\config.json` 的 `token` 为 `""`）：
      对外端口 18890 无令牌保护。**建议用户在 MCP 中心界面设置令牌**（P1，安全项）。
      考虑：首次启动若 token 为空，日志/界面醒目提示未设令牌。
- [ ] 外部工具不进宿主注册表：Agent 不能直接消费外部 MCP 工具（须经 `universal_tool` 转发）。
      可评估「按白名单把外部工具注册进宿主注册表」的取舍（名称冲突/安全面/快照时效）→ 设计确认后再做（P2，design §13）。
- [ ] 外部工具清单是**连接时快照**：服务器新增工具后需重连才可见。标准解法是订阅
      MCP 2.0 `notifications/tools/list_changed` 并触发重拉（P2，design §13）。
- [ ] WebSocket 传输未实现：MCP 2.0 标准传输的第三种（stdio / Streamable HTTP 已支持）。
      外部服务器用 ws:// 时当前报「传输类型不支持」（P3，design §13）。
- [ ] 多令牌 / IP 白名单 / 外部工具白名单未实现：网关令牌目前单值（P3，design §13）。
- [ ] 外部工具不经宿主 pre-execute 拒绝门：`universal_tool` 转发外部工具时跳过宿主事件链
      （`mcp.` 前缀直连外部会话）。如需宿主级安全拦截需在转发路径加门（P3，design §13）。
- [ ] `StreamableHttpMcpTransport._getSupported` 字段赋值未使用（CS0414 警告）→ 清理或使用（P3，代码卫生）。
- [ ] `specs/034-mcp-center/tasks.md` 状态头仍写「v2.0.0 整合进行中」、39 条任务全部未勾选：
      与实现进度不符（实际 v2.1.0 已发布）→ 按现状收敛勾选 + 更新状态头（P2，文档欠账）。
- [ ] McpServiceTests / McpCenterRuntimeTests 存在 xUnit1031 阻塞式 Task 警告（既有基线）
      → 顺手改 async 消除（P3，代码卫生，非阻塞）。

## ✅ 已闭环（近期完成，留痕即移）

- 管理面鉴权：`api/mcp` / `api/mcp-center/servers` / `api/mcp-center/config` 三个控制器
  类级 `[Authorize("ApiKeyPolicy")]` + `McpAdminAuthTests` 回归（v2.1.0）。
- 万能工具说明补全 + `list_tools` 枚举工具（v2.1.0）：传参示例 / 常规能力分类 / 发现工具入口 /
  外部命名空间写进 `UniversalToolForwarder.ToolDefinitionJson`；`ListToolsToolFunctionTests` 覆盖。
- 插件验收标准文档：`.agents/skills/plugin-development/references/plugin-acceptance.md`
  （管理面鉴权写入技能铁律 17、工具可发现性写入铁律 18）。
