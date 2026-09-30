# Review（批次A 菜单/路由真源一致性 · 测试审查终审）

> 审查人=测试审查（seq105 派单）；Evidence 见同目录 05-evidence.md。闸门1 基准=seq95/seq99，交付回报=seq102，裁决口径=seq105。

## 审查八问

1. **实现是否满足 Intent？** 是——菜单/路由双轨漂移（D1 /quicklinks 悬空、D2 manifest 不补发、D3 纯后端插件声明界面菜单）全部消除，且有防漂移 e2e 门禁。
2. **是否符合 Spec？** 符合——T1 合并派生（Id/字段/防双发/禁用不发）、T2 一行改、T3 撤销留指引注释、T4 铁律19 四点逐字+§四门禁加条、T5 四组断言均落地；AC-1~8 独立复跑全 Pass。
3. **是否超出 Scope？** 未超——工作区跟踪改动=任务书 7 文件清单（AGENTS.md/docs 等为并行会话，seq105 已认定勿计入）；T5③ 断言面收窄已获 seq105 裁决①认可，子项排除注释在案。
4. **是否改了不该改的文件？** 否——生产代码仅 PluginController.cs（T1 指定文件）；无删除/跳过测试。
5. **测试覆盖 AC？** 覆盖——AC-8 单测 3 例真实调用 controller；AC-6 e2e 4/4 + 篡改必 Fail 实测（③悬空断言+①对账差异双 Fail 精确命中，②④不受扰动通过）。
6. **明显回归风险？** 低——全量 dotnet test 失败名单=批次E 9 项恰等（零新增）；前端 473 全绿；既有 4 红基线复证+引用面排查双重确认与本批零关联。
7. **架构一致性？** 一致——plugin.json frontend 单真源方向与铁律19 对齐；GetMenuItems 返回形状未变（契约不破）。
8. **Evidence 足以证明完成？** 足以——API 面 + DLL 探针 + 浏览器渲染反证（token 注入后 /quick-links 正常出「快捷链接」heading）三类独立证据。

## 检查块

- Requirement Check：**PASS**
- Scope Check：**PASS**
- Test Check：**PASS**（含 flake 零出现：上轮 AgentRegistry 负载波动本轮未复现）
- Architecture Check：**PASS**
- Risk：**L1**（改动集中 GetMenuItems 读路径 + 3 插件注册面，均有回归网）

## Findings（移交项管哥，均不属批次A 交付缺陷）

- **Critical**：无。
- **Important**
  1. 端口真源冲突：配置真源 `PortNumber=7102` vs 派单/AGENTS/live spec 惯例 `:51888`；旧常驻实例（D:\src\tools\ForgeSelf）已停、新批次A 宿主按真源跑 7102，**:51888 当前无监听**（用户入口待恢复，需裁决：改配置回 51888 / 或文档口径统一 7102——测审未擅改配置）。
  2. live 基建缺口：`playwright.live.config.ts` + quick-links live spec 无 token 注入通道，fresh context 必红（归因已反证锁死）→ 建议独立缺陷单。
  3. `migrate-plugin-versions.ps1` ConvertFrom-Json 报错（某 manifest 解析失败），发布以扁平回退布局正常工作 → 建议登记排查。

> **排查单（2026-09-29 审计补充）**：脚本 `scripts/migrate-plugin-versions.ps1` L42 `$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json` **确实无任何容错包裹**——`ConvertFrom-Json` 遇非法 JSON（注释、尾逗号、BOM 混合编码等）会直接抛 `PSObject` 解析异常，无 `try/catch` 兜底、无跳过单个插件继续的逻辑。若当前某插件 `plugin.json` 仍含此类瑕疵，该脚本会中断。**是否仍报错不可静态核实**：脚本本身不运行、不连仓库状态，纯静态读码只能确认"缺容错"这一事实；是否真有 manifest 触发需实跑或人工核对全部 `plugin.json` 才能定论。建议：① 给 L42 包 `try/catch`（解析失败 → `[skip] $name : manifest 解析失败` 并 `continue`）；② 登记为独立缺陷单，与批次A 解耦。
- **Minor**：批次E 观察名单建议保留 AgentRegistryServiceTests×10（上轮负载 flake，本轮未现）。

## Final Decision

**APPROVED**（AC-1~8 全 Pass + 门禁③发布有效实证；Findings 1-3 移交项管哥分流，不阻断闸门2；门禁④浏览器走查由体验走查岗接续）。
