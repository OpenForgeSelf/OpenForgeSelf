---
name: design-system-consume
description: 设计系统插件（design-system）的**消费侧**指南：外部客户端 / 内置 AIAgent / MCP 网关如何发现并调用 8 个 design_* 工具，如何解封套、过写开关、走 REST 对等端点。写「用 design_* 工具做设计系统自动化 / 集成」类任务时用；维护 design_* 工具本身走 design-system-verify。
---

# 设计系统插件消费指南（design-system-consume）

> 适用：从 **外部** 使用 design-system 的 Agent 工具（MCP 网关 `universal_tool` 转发 / 内置 AIAgent `ToolScopePluginIds` 白名单），
> 或写基于这些工具的集成代码 / 测试。
> 维护侧（改工具本身）走 `design-system-verify`；本技能只讲**怎么用**。

## 一、工具清单（8 个，唯一真源 = `Agent/DesignToolIndex.cs`）

| 工具 | Kind | 作用 | 关键参数 |
|---|---|---|---|
| `design_guide` | read | 使用指南：版本/工具清单/三条工作流/可选项目与预设/写开关/发现提示 | `-` |
| `design_context` | read | 设计说明书（唯一真源）：项目身份/使用规则/颜色/排版/尺度/组件/品牌/交付清单 | `project, format(md\|json), sections[], budget` |
| `design_lookup` | read | 查令牌/组件/导出/图标；`nearest` 按值反查 | `project, kind, path, prefix, tier, type, page, pageSize` |
| `design_review` | read | 审查代码与设计系统一致性：硬编码/未知令牌引用；`checklist` 交付清单 | `project, code\|files, language, mode(code\|checklist), strict` |
| `design_audit` | read | 读可达性审计结论（`run=true` 才重跑并落库 = 写动作） | `project, run, kind, passed, page` |
| `design_presets` | read | 风格预设目录：`list` 8 预设全字段 / `recommend` 按 brief/industry 打分 | `action(list\|recommend), brief, industry, kind, tone, density` |
| `design_create` | **write** | 快速创建设计系统项目：从预设+显式参数生成令牌/组件/审计 | `name, code, kind, description, presetId, overrides, apply` |
| `design_edit` | **write** | 改项目：`set_token` 写单令牌 / `regenerate` / `publish`（critical 未清拒） | `project, action(set_token\|regenerate\|publish), path, value, theme` |

- 读写归类：**read 6 / write 2**（`design_audit` 归类 read，`run=true` 时执行写动作）。
- 出参键全小写 **camelCase**（`DesignToolBase.ExecuteAsync` 统一 `JsonNamingPolicy.CamelCase`）。

## 二、发现与调用（外部客户端）

### 发现：`list_tools`（无封套）

```text
tools/call { tool: "list_tools", parameters: { keyword: "design" } }
→ 响应直接是数据：{ total: 8, keyword: "design", tools: [{ name, description, pluginId }, ...] }
```

⚠ `list_tools` 是**枚举助手**，响应**没有** `{success, data}` 封套 —— 直接解 `result.content[0].text`。

### 调用：`universal_tool` 转发 + `{success, data}` 封套

```text
tools/call { tool: "universal_tool", parameters: { tool: "design_context", parameters: { project: "<code>", format: "json" } } }
→ result.content[0].text = { success: true, data: { ... } }   ← data 才是载荷
```

- 工具自身失败：`{ success: false, error: "<消息>" }`（HTTP 仍 200，由调用方判 `success`）。
- `data` 内的对象键均为 camelCase（如 `colorHex`、`tokenCoverage`、`allowWrite`）。

## 三、写开关（Agent 写能力门禁，fail-closed）

- 位置：插件数据目录 `{数据根}/plugins/design-system/agent-access.json`（只写不删）。
- **默认 fail-open**（文件不存在 → `allowWrite=true`）；文件损坏 → **fail-closed 只读**（`allowWrite=false`）。
- 关写后：`design_create` / `design_edit`（含 `design_audit run=true`）被拒，错误文案指向
  `PUT api/design-system/agent-access`。
- REST 对等：`GET|PUT api/design-system/agent-access`（PUT body `{ enabled: true|false }`）；
  响应 `{ success, data: { allowWrite, source(default|file), corrupt, updatedAt } }`。
- 每次 `Get()` 现读文件不缓存：PUT 立即生效、其他进程改后重启保持。

## 四、REST 对等端点（`api/design-system`，均 `[Authorize("ApiKeyPolicy")]`）

| 端点 | 对应工具 | 备注 |
|---|---|---|
| `GET meta` | `design_guide` 同源 | `capabilities` 24 项 + `agentTools` = 8 工具名数组 |
| `POST projects/quick-create` | `design_create` | `QuickCreateRequest{Name,Code,Kind,Description,Preset,Request,DryRun}`；**REST `DryRun=false` 落库**（与工具 `apply=false` 干跑语义相反） |
| `GET projects/{id}/brief` | `design_context` | md/json 说明书 |
| `POST projects/{id}/review` | `design_review` | 审查 |
| `GET presets` / `POST presets/recommend` | `design_presets` | 预设与推荐 |
| `GET\|PUT agent-access` | 写开关 | 见 §三 |
| `GET agent/tools` | `list_tools` | 同源枚举 |
| `GET projects/{id}/tokens/effective?theme=` | `design_lookup` | `{theme, themeId, count, items[], diagnostics}` |

## 五、消费侧常见坑（逐条实测）

1. **封套分两层**：`universal_tool` 转发本身有 `{isError, content}`；design_* 工具结果再套 `{success, data}`；
   `list_tools` 例外（无第二层封套）。解包顺序：`result.content[0].text` → 判 `success` → 取 `data`。
2. **`EffectiveToken.colorHex` 不是 `hex`**：`DesignMapper.cs` 序列化后字段名为 `colorHex`（另有
   `value/sourcePath/aliasPath/resolved/error/contrastRatio/wcagLevel`）。
3. **`design_review` 的硬编码默认是 warning 级**：`summary.hardcoded` 数出硬编码条数；`strict:true` 才把
   warning 升为 error（`summary.errors`）。断言口径：`hardcoded ≥ 1` 恒真；`errors ≥ 1` 需 `strict:true`。
4. **`design_review` 缺省主题会走 shared 快照 → 语义层为空 → 硬编码色判不出**：
   必须先 `ResolveTheme` 落项目默认主题（light）。调用时显式传 `theme` 最稳。
5. **`design_audit` 归类 read 但 `run=true` 是写动作**：关写开关后 `run=true` 会被拒；只读查询不受影响。
6. **REST `DryRun` 与工具 `apply` 语义相反**：REST `DryRun=false` = 落库；工具 `apply=false` = 干跑不落库。
7. **端口**：MCP 网关端口由 `FORGESELF_MCP_GATEWAY_PORT` 覆盖（默认被用户实例占用时）；宿主端口 7102 被占
   = 测试打在旧产物，先验版本自洽。

## 六、判据（集成代码写完后自查）

- 工具返回与 REST 关键字段**逐字段一致**（同源：说明书里的颜色/变量名 == `tokens/effective`）。
- 写工具三条路径都验：开关开→写成功；开关关→被拒且文案指开关；文件损坏→fail-closed 只读。
- 干跑（`apply=false` / `DryRun=true`）前后项目数与 `DesignToken` 行数不变（不只信返回值）。
- 工具数量：`list_tools` 枚举 == `meta.agentTools` == 8，读 6 写 2。
