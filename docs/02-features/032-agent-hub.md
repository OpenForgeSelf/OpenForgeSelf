# 032 · Agent 中枢（AgentHub）—— 外部 Agent 探测与委派总线

> 版本：2026-09-24（v1.0.9）· 状态：**已落地**
> 来源：输入4 调研（CLI-agent 探测/委派生态）→ 输入5（探测支持附加扫描目录）

## 1. 结论先行

| 能力 | 实现 | 状态 |
|---|---|---|
| 外部 agent 登记注册表（vendor 唯一，按 profile 预填交互口） | AgentRegistry + `api/agent-hub/agents` | ✅ |
| 本机探测（不自动登记） | AgentProbeService + `GET api/agent-hub/agents/discover` | ✅ |
| **附加扫描目录（探测时除 PATH 外额外查找）** | `AgentHubSettingsStore` + `GET\|PUT api/agent-hub/settings`（config.json 持久化） | ✅ **v1.0.9 新增** |
| 委派执行（CLI one-shot + 事件流 + 审批台） | DelegationRuntime / CliTransport / `api/agent-hub/tasks` | ✅ |
| 管理面鉴权 | 控制器类级 `[Authorize("ApiKeyPolicy")]`（铁律 17） | ✅ settings 端点已带；agents/tasks 端点沿用宿主侧策略（待核对清单见 §5） |

**一句话**：探测不再是「只沿 PATH」——用户可配置附加扫描目录（每行一个，落盘 `config.json`），装在不进 PATH 的 CLI（pnpm/bun/scoop/自定义安装）也能被发现。

## 2. 探测机制（代码事实，AgentProbeService）

`ResolveExecutable(name)` 的查找顺序：

1. **绝对路径**：`Path.IsPathRooted` → `File.Exists` 直接校验；
2. **PATH**：按 `PATH` 环境变量逐目录 × 候选名（原名 + Windows 扩展名 `.exe/.cmd/.bat/.ps1` 补全）；非法 PATH 项 try/catch 跳过；
3. **附加扫描目录**（v1.0.9）：`AgentHubSettingsStore.Current.SearchDirectories` 逐目录查找，与 PATH 已覆盖目录去重；同样做扩展名补全。

`DiscoverAsync()` 扫描面 = 内置 profile（`Data/Profiles/{claude,codex,opencode,qodercli}.json` 的 Executable）逐个探测 + `KnownLocations` 硬编码兜底（cursor/cursor-agent、aider、gemini）。探测结果区分：未安装（Missing）/ 降级（Degraded，profile 断言不通过不静默）/ 可用（Ok，`--version` 探针）。

## 3. 设置接口契约（v1.0.9 新增）

路由前缀 `api/agent-hub/settings`，**类级 `[Authorize("ApiKeyPolicy")]`**（铁律 17：无 token 直接 401）。

| 方法 | 路径 | 入参 | 返回 |
|---|---|---|---|
| GET | `api/agent-hub/settings` | — | `{ searchDirectories: string[] }` |
| PUT | `api/agent-hub/settings` | `{ searchDirectories: string[] }` | 规范化后结果（去空白/去重/去空项），点即保存落盘 |

持久化位置：**插件数据目录** `{数据根}/Plugins/agent-hub/config.json`（生产即 `~/.forgeself/Plugins/agent-hub/config.json`），随数据走、发布覆盖不影响；文件损坏时退回默认并告警，不阻断插件。

## 4. 前端（AgentHubView.vue 扫描区）

Agent 目录 tab 工具栏下方新增「附加扫描目录」配置块：textarea（每行一个目录）+ 保存按钮；保存后回填后端规范化结果并提示「重新扫描即生效」；读取失败红色提示（操作成败可见，§3.4 交互要求）。

## 5. 验证与已知事项

- 单测 `AgentHubProbeSettingsTests`（8 个）：附加目录命中 / Windows 扩展名补全 / 绝对路径回归 / 空目录不抛 / 设置存储默认值·落盘重载·规范化·损坏文件容错。
- e2e `ForgeSelf.Web/e2e/plugins/agent-hub/agent-hub.spec.ts`：新增「设置写往返（GET→PUT→GET 持久化）」+「扫描区配置输入 UI 交互」两用例；并修复既有标题断言（版本徽标 → 前缀匹配）。
- **发布坑（v1.0.9 实证）**：版本化更新（`POST /api/plugin/update`）只把新前端产物放进 `versions/<v>/web/dist`，但前端中间件解析**插件根目录 `web/dist`** → 页面仍跑旧 bundle。发布后必须补拷：`versions/<v>/web/dist/*` → 根 `web/dist/`，并 grep 新增关键词核验（见 `plugin-publish-verify` troubleshooting）。
- **待核对**：`AgentHubAgentsController` / `AgentHubTasksController` 是否带类级 `[Authorize("ApiKeyPolicy")]`（铁律 17 遗留项，v1.0.9 未动，单独记 TODO）。

## 6. 来源

- 输入4 调研报告：`docs/06-research/002-cli-agent-detection-delegation.md`（MCO 探测三件套 / Forkn 路径可配 / 五元组适配器契约等落地建议）
- 技能：`plugin-development` §四（发布闭环）· §3.2（出树构建）
