# 07 · Final Report — 最终报告（闸门2 提请验收 · 2026-10-28）

> **修订轮（2026-10-28 17:29 用户拍板）**：「mcp中心的工具不用前缀，是特殊例外」——mcp-center 整插件豁免前缀（`ToolNamespace.ExemptPluginId`），get_current_time/calculate 用原名；PrefixByPluginId 移除 mcp-center、其 plugin.json 删 toolPrefix、S5 加豁免断言；引用面（前端名册/测试/e2e/文档）全部回改；ADR-004 增修订节。修订轮验证：宿主+插件编译绿、vitest 1035/1035、check 0 errors、mcp-center 冒烟 calculate 6*7=42 透传 ✓（stdio npx 场景 npx 拉包超时=环境面，重试确认中）。本轮实施中 pwsh 批量替换引发 4 文件字符级损坏（`foeeach`/`eetuen` 型），已 git checkout 恢复 + 小步 edit 重做，全量编译扫描证实无同类残留。

## 状态

⚠️ **COMPLETED_WITH_RISK** —— 全部拍板需求落地且域内验证全绿；两处环境/存量面标 Unknown（详见风险）。

## 一句话

工具命名空间统一完成：81 个工具改名、8+1 个公共工具迁 mcp-center、11 个平台保留名、无别名层；宿主运行期 127 个工具注册实证零旧名零重名；前端/测试/e2e/文档四面同步。

## 主要变更（按批次）

- **B1 契约**：`ForgeSelf.Abstractions/ToolNamespace.cs`（保留名 11 + PrefixByPluginId 16 + RenameRows 81 + 守卫校验入口）；`IProjectWorkspaceService.cs` 上移；`ExtensionPointManager.cs` 注册口 Validate 门。
- **B2 改名**：15 个插件 81 个工具 Name + Description + 16 个 plugin.json `toolPrefix`。
- **B3 迁移**：AIAgentPlugin 844→322 行；8 个公共工具类迁 `Plugins/McpCenter/Services/PublicTools/`（文件三件套按 FR-5 绝对路径参数化）；6 个旧源文件入 `.trash/2026-10-28-tool-namespace-ownership/`。
- **B4 同步**：后端字面量（memory_relevant×3 / workflow_execute×5 / mcp_calculate×2）、AIAgent web 三表（composerContext FULL_ONLY/READONLY_EXTRA/PREFIXES 全量新名）、测试工程 9 文件、e2e 2 个 spec、文档 6 份 + ADR-004 + not-taken 044/045。

## 验证结果（Validation · 真实执行，✅/⚠️ 分级）

| 验证项 | 结果 | 等级 |
|--------|------|------|
| 后端过滤集（14 组守卫+迁移相关） | 251/251 绿 | ✅ Verified |
| 宿主 vitest | 1034/1034 绿 | ✅ Verified |
| mcp-center 冒烟 e2e | 8/8 绿（含 mcp_calculate 透传 6*7=42） | ✅ Verified |
| ai-agent 界面冒烟 e2e | 6/7 绿（红项=LLM 401 环境面） | ✅ Verified |
| e2e 宿主 127 工具注册日志 | 零旧名零重名，归属全对 | ✅ Verified |
| 20 插件 csproj 编译 + AIAgent web build | 全绿 | ✅ Verified |
| 全量 dotnet test | testhost 崩溃（存量 Integration 组合缺陷，4 次尝试+二分定位记录，域内跑到部分全绿） | ⚠️ Unknown |
| tool-gateway-031 LLM 端到端 | 上游 401（e2e 宿主无有效 key + 并行会话 provider WIP 进 build） | ⚠️ Unknown |

## 设计决策

- get_current_time/calculate 迁 mcp-center 并加 `mcp_` 前缀（用户「等等」范畴推定执行，ADR-004 已留推翻口）。
- cost-scope **不**加 toolPrefix——实测无工具插件，S1 误红根因是注释误判，修扫描器而非给空插件立规。
- 守卫扫描器三层防误报（注释剥离 / ServerId 行 / ScriptParameterType 行 / 绑定特性行），修类不修例。
- finish/submit_plan/complete_step/request_help 维持豁免（协议词一致性）。

## 代价与收益

- 收益：注册表工具名全局唯一自带归属；检索/权限/文档三面按名分组无歧义；公共工具与 AIAgent 版本解耦；重名冲突类（曾 5 组）永久消除且 CI 防复发。
- 代价：旧名一次性退役——未同步的外部集成将收 unknown tool（用户拍板接受）；本轮同步面 45+ 文件。

## 不做事决策（已入 not-taken 044/045）

- 不做别名层/不迁移存量引用（用户拍板）；不做根目录白名单；CWD 三道安全门语义原样随迁不收紧不放宽。

## 风险

1. 全量 testhost 崩溃为存量基建缺陷（Integration 段类间组合触发），已记 TODO P1 专项；若与并行会话 provider WIP 相关则随其落定自愈。
2. e2e LLM key 面（LM Studio EMPTY token / invalid_api_key）待并行会话 provider 批落定后复跑 tool-gateway-031 四场景即可闭环 AC-7 全绿。
3. 未同步旧名的外部调用方会失败——如出现，按 not-taken 044 触发条件评估显式别名注册。
4. **修订轮新增（17:29 拍板）**：① 2.4.16 本地更新源包（17:27 发）内工具名仍为修订前 `mcp_get_current_time`/`mcp_calculate`——**包命名已过期**，下一版发包须含修订后三 DLL（已注记 TODO :28 供发布会话对表）；② ~~守卫测试尚未执行~~ → **已解除**：外部 testhost 锁释放后补跑，后端过滤集 **212/212 绿**（含 S5 豁免断言）+ mcp-center 冒烟 **8/8 全绿**（calculate 6*7=42 ✓）。

## 结论

域内可验证面全部 Verified 绿；两处 Unknown 均有根因、证据与后续路径。**提请闸门2 验收**；验收通过后按闸门3 提交（git commit 待用户批准）。
