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
| `design_context` | read | 设计说明书（唯一真源）：项目身份/使用规则/颜色/排版/尺度/组件/品牌/**UX 规范（v3.1.0）**/交付清单 | `project, format(md\|json), sections[], budget` |
| `design_lookup` | read | 查令牌/组件/导出/图标/**规范（v3.1.0 `kind=guideline`）**；`nearest` 按值反查 | `project, kind, path, prefix, tier, type, page, pageSize, q, code, theme` |
| `design_review` | read | 审查代码与设计系统一致性：硬编码/未知令牌引用；`checklist` 交付清单（**含规范派生条目**） | `project, code\|files, language, mode(code\|checklist), strict` |
| `design_audit` | read | 读可达性审计结论（`run=true` 才重跑并落库 = 写动作） | `project, run, kind, passed, page` |
| `design_presets` | read | 风格预设目录：`list` 预设全字段（**v3.1.0 起 13 个**）/ `recommend` 按 brief/industry 打分 | `action(list\|recommend), brief, industry, kind, tone, density` |
| `design_create` | **write** | 快速创建设计系统项目：从预设+显式参数生成令牌/组件/审计/**规范** | `name, code, kind, description, presetId, overrides, apply` |
| `design_edit` | **write** | 改项目：`set_token` / `regenerate` / `publish`（critical 未清拒）/ **`guideline` 增改一条规范（v3.1.0）** | `project, action(set_token\|regenerate\|publish\|guideline), path, value, theme, code, title, summary, body, rules[], tokens[], category, status` |

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
| `GET projects/{id}/guidelines[?status=&category=&theme=]` | `design_lookup kind=guideline` | v3.1.0；**默认不含 archived**（`status=all\|archived` 才看得到），每条带 `tokenRefs/tokenValues/brokenRefs/valueTheme/categoryLabel/rules[]` |
| `GET projects/{id}/guidelines/{code}` / `PUT …/{code}` | `design_edit action=guideline` | v3.1.0；PUT = upsert（同 code 第二次是更新不是新增），写入即 `source=manual`；带 `expectUpdatedAt` 不一致回 **409** 且不写 |
| `POST projects/{id}/guidelines/generate?overwrite=` / `POST …/{code}/archive` | （工具侧只读，生成挂在 `design_create/regenerate`） | v3.1.0；generate 回 `created/skipped/skippedProtected/overwritten/total`；archive = **软删**，恢复 = PUT 带 `status=adopted`。**没有 DELETE 端点** |

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
8. **规范出参的数值是"现查"的，不是库存的**（v3.1.0）：`body/rules[].text` 里只有反引号令牌路径；
   同一份规范在括注后的 `body`（展示）与 `bodyRaw`（原文）里长得不一样 —— **写回时必须用 `*Raw`**，
   把括注文本 PUT 回去就等于把 `24px` 存进库里（令牌改值后就成了旧数字）。
   `tokenValues[path]` 与 `brokenRefs` 的取值口径是 `valueTheme` 那一套主题（**参考主题 = 项目默认色彩主题**），
   与 `?theme=` 请求的导出主题不是一回事；别拿它当"当前主题的数"用。
9. **`design_edit action=guideline` 里空数组 = "这次不改"，不是"清空"**（v3.1.0）：`tokens:[]` / `rules:[]` 一律忽略，
   避免"只想改标题却把引用令牌清单清空"。要真的清空必须显式给非空表达（当前版本不提供清空语义）。
   写入即把 `source` 置 `manual`：下次 `generate`（`overwrite=false`）会保护它并在 `skippedProtected` 里点名。
10. **规范没有删除**（v3.1.0，全插件铁律）：只有 `archive`（软删，默认清单读不到、`status=all` 读得到）与
    PUT `status=adopted` 恢复。集成方要"移除一条规范"就归档它，别去找 DELETE —— 控制器里一个都没有（有反射守卫）。

11. **写成功后"立刻读"可能读到旧视图，集成方必须轮询而不是单次判定**（v3.1.0 实测，M3 批 C 复现；根因在读侧，修法待拍板）：
    `design_create` / `quick-create` 已返回 200 并给出项目 id，紧接着 `design_lookup kind=export` 可能拿到**少掉整个 `--ds-shadow-elevation-*` 族**的 CSS，
    `GET projects` 也可能查不到刚建的那条；而同一瞬间读 `tokens/effective` 是齐的 —— 也就是**库里不缺，是读路径先读到旧视图**。
    对自动化脚本的三条硬要求：① 写后读一律**带退避地轮询**到"结构完整"（例：断言五档 `elevation-1..5` 全在）再用，不要单次判定就落盘；
    ② 需要"产物是否完整"的判据时，走**不吃写读链**的 `generate/preview-css`（纯内存投影，与导出同源）当权威源；
    ③ 别把"读到的第一份"当事实写进下游仓库/PR —— 这类缺陷最难查，因为写侧的 200 是真的。
    跟踪状态见 `Plugins/DesignSystem/README.md` 已知缺口 **G15**；插件侧的常驻判据写法见 `design-system-verify` 自查表 #52。

## 六、插件内「交付与接入页」（v3.0.0 · 消费侧的可视入口）

插件界面的四模式外壳里，`交付与接入` 模式（`DeliveryMode.vue`）就是**把上面这套消费方式做成页面**，
给不想读文档的人一个"照抄就能接上"的入口。它展示的每一块都必须是**同源真值**，不许前端另算一套：

| 区块 | 内容 | 真源（页面必须与它逐字/逐条一致） |
|---|---|---|
| Agent 网关 | 网关地址 + 调用片段 | 页面**不含真实令牌**：片段只写 `Bearer <你的令牌>` 占位符 |
| 工具清单 | 8 个 `design_*` 工具名与说明 | `GET agent/tools` == `meta.agentTools` == `Agent/DesignToolIndex.cs`（三处必然一致） |
| 写开关 | 当前 `allowWrite` + 切换 | `GET\|PUT api/design-system/agent-access`（PUT 往返，见 §三） |
| 说明书 / 规则 | `brief` 与 `agent-rules` 原文 | REST 导出 `GET projects/{id}/brief` + `agent-rules` 格式（同源，可对照复制） |
| 试审查 | 粘贴代码 → 审查结论 | 与 `POST projects/{id}/review` 同源；**>200KB 客户端直接拦截、不发请求** |

- 自查：**里外一致**——页面上列的工具数/规则原文，与用 `design_context` / `list_tools` 拿到的必须一致；
  页面不得出现真实令牌（连掩码都不渲染），否则截图/复制即泄露。
- 相关 e2e：`e2e/plugins/design-system/design-system-showroom.spec.ts` C 片（AC18–AC23）。

## 七、判据（集成代码写完后自查）

- 工具返回与 REST 关键字段**逐字段一致**（同源：说明书里的颜色/变量名 == `tokens/effective`）。
- 写工具三条路径都验：开关开→写成功；开关关→被拒且文案指开关；文件损坏→fail-closed 只读。
- 干跑（`apply=false` / `DryRun=true`）前后项目数与 `DesignToken` 行数不变（不只信返回值）。
- 工具数量：`list_tools` 枚举（按 `pluginId` 归因）== `meta.agentTools`；**这条等式已由 e2e 常驻用例 G7 每次真跑网关自动比对**（`design-system-guidelines.spec.ts`，含"未知工具必 `isError`""不许绕过 `universal_tool`"两条反向腿），所以本技能**不抄件数**——抄了就会漂。读/写分档看 `GET agent/tools` 的 `readOnly` 字段，别照记忆数。
