# 03-plan：外部 MCP 三缺陷修复批（2026-10-17）

## 改动分组（canonical fix 优先）

### A. 后端·传输层（S1/S2/S3）
1. 新增 `Plugins/McpCenter/Services/McpClient/McpClientProtocol.cs`：clientInfo 常量 + `BuildInitializeParams`（一处定义）。
2. `StreamableHttpMcpTransport.cs`：
   - InitializeAsync 用 BuildInitializeParams；成功后 NotifyAsync("notifications/initialized")（try/catch Warn）；记录协商版本。
   - 新增 `_sessionId/_negotiatedVersion` 字段；RequestAsync/NotifyAsync/StartSseStream 统一 `ApplyProtocolHeaders`（Mcp-Session-Id + Mcp-Protocol-Version）；响应捕获会话头。
   - 新增 `NotifyAsync`（POST 无 id，只看 HTTP 状态）。
   - 类注释更新（会话复用已实现）。
3. `StdioMcpTransport.cs`：InitializeAsync 用 BuildInitializeParams + NotifyAsync（写无 id 帧，try/catch Warn）；新增 NotifyAsync。
4. `LegacySseMcpTransport.cs`：同上（NotifyAsync 走 endpoint POST）。
- 同类扫查命中项（3 处同根因）= 本组全覆盖。

### B. 后端·存储校验（S4 防护）
5. `ExternalServersStore.ValidateAndNormalize`：Headers/Env 值命中掩码形态（`****` 前缀且 ≤8 字符）→ 抛 McpClientException（400 文案）。

### C. 前端（S4）
6. `types/external.ts`：StateDto 补 `url?/command?/args?`。
7. `McpCenterView.vue`：
   - `openEditExternal` 回填 url/command/argsText 真值 + 掩码基线记录（新 ref）；`openCreateExternal` 重置基线。
   - `saveExternal` 编辑态：与基线逐字相同的 headersText/envText → 字段置 undefined。
   - 对话框模板：编辑态（externalEditingId 非空）请求头/环境变量下方加掩码语义说明行。

### D. 测试与守卫（G1 转绿 + G2/G3）
8. 改写 `McpExternalInitializeClientInfoTests.cs`：捕获全部请求体（List）；断言 initialize 带 clientInfo（转绿）+ 第二发是 notifications/initialized；注释从「复现红灯」改「类守卫 G1」。
9. 新增 `McpStreamableSessionHeaderTests.cs`：fake handler 返回 Mcp-Session-Id → 后续 POST 携带该头与 Mcp-Protocol-Version。
10. 新增 `McpClientProtocolGuardTests.cs`：
    - BuildInitializeParams 形状直测；
    - 源码扫描守卫（G3）：三传输源文件必须引用 BuildInitializeParams、必须含 "notifications/initialized"、禁止手写 `new { protocolVersion`；从 AppContext.BaseDirectory 向上定位仓库根。
11. `ExternalServersStore` 掩码拒绝测试（并入 10 或 McpExternalConfigTests 风格新增 fact）。

### E. 归档与文档
12. PC-008「MCP 客户端协议合规缺失」+ case 归档 `docs/11-troubleshooting/`，台账 README 三索引，`check-problem-classes.ps1` 绿。
13. `docs/02-features/034-mcp-center.md` 契约回写（initialize 载荷、通知、会话头、编辑回显语义、掩码拒绝）。
14. `plugin.json` 2.4.0 → 2.5.0（行为级新增）。

## 验证顺序（每步可立即判对错）
① `dotnet build ForgeSelf.slnx` → ② `dotnet test --filter McpCenter`（含红灯转绿/守卫/集成回归）→ ③ 插件前端 `pnpm run build` + `check` + `test` → ④ 冒烟 e2e（外部 MCP 主链路：新增 deepwiki→连接成功→编辑回显→删除清理）→ ⑤ `pwsh scripts/check-problem-classes.ps1` → ⑥ dev-stack 起源码前后端走查+截图 → ⑦ `release-local.ps1 -UpdateDir -Sign`。

## 风险与回滚
- 风险 1：真实服务器对 initialized 通知返回非 2xx → 已按尽力而为处理（Warn 不阻断）。
- 风险 2：掩码拒绝误伤真实以 `****` 开头的短密钥 → 形态判定收窄（前缀恰为 4 星且总长 ≤8），误伤概率极低；文案可自诊。
- 回滚点：改动全部在 git 工作区，单 commit 原子（插件本体+测试+文档），revert 即回。
