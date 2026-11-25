# 05 Evidence — 全项目 token 违反审计（本批核心交付）

> 审计时间：2026-11-20 23:3x；工具：`scripts/check-theme-tokens.mjs`（自 NewLife.Cube.Vue 复制适配，审计期白名单为空）；
> 扫描范围：`ForgeSelf.Web/src` + `Plugins/*/web/src`（豁免：`src/styles/themes/*.css` 主题值所有者、`*.test.ts`/`*.spec.ts`/`*.d.ts`）。
> 全量原始清单：`.temp/theme-tokens-audit-full.log`（6007 行，会话内留存）；**可随时重现**：`node scripts/check-theme-tokens.mjs`。
> 证据等级：全部 **Verified**（脚本实跑 + 读码盘点）。

## §1 总量
**6007 处违反，涉及 173 个文件**。宿主 ForgeSelf.Web 2588 处 / 插件 3419 处。

## §2 按规则
| 规则 | 处数 | 说明 |
|------|------|------|
| 未授权的自定义 token（非 `--el-`） | **4393** | 去重 **338 个**不同 token；`--gold-400`(228)/`--fg-primary`(151)/`--fg-secondary`(143)/`--font-mono`(139)/`--ds-space-3`(138)/`--ds-space-2`(125)/`--ease-out`(121)/`--ds-border-1`(113)/`--dur-fast`(109)/`--ds-fs-small`(108)/`--radius-md`(106)/`--fg-tertiary`(102)/`--ds-space-4`(97)/`--cinnabar-400`(95)/`--gold-tint`(88)/`--radius-sm`(80)/`--paper-tint`(71)/`--aiw-text-soft`(61)/`--ds-radius-sm`(59)/`--aiw-primary`(56)/`--aiw-text`(56)/`--ds-fg-3`(56)/`--border-default`(54)/`--bronze-500`(54)/`--ds-surface-1`(52) 为 TOP25 |
| 硬编码颜色 | **1522** | hex 1336 / rgb(a) 132 / CSS 命名色 54（含 `var(--x, #fff)` 兜底） |
| 未知的 Element token（引用了不存在的 `--el-*`） | **92** | 见 §3——全部是真实错误（拼写/臆造档位），非白名单问题 |

## §3 未知的 Element token（15 种 · 92 处 · 建议最先修）
| token | 处数 | 定性 |
|--------|------|------|
| `--el-font-family-mono` | 33 | 不存在（EP 无等宽字体 token） |
| `--el-color-primary-light` | 17 | 不存在——EP 是带档位下标的 `--el-color-primary-light-3` 等，裸引用无效 |
| `--el-weight-medium` / `--el-weight-semibold` | 12 / 9 | 不存在（EP 无字重 token） |
| `--el-border-round` | 7 | 不存在（是 border-radius 取值 `round`？EP token 为 `--el-border-radius-round`） |
| `--el-color-success-light` / `--el-color-warning-light` / `--el-color-danger-light` / `--el-color-info-light` | 3/2/1/1 | 同 primary-light：缺档位下标 |
| `--el-color-accent` / `--el-color-accent-teal` | 2 / 1 | **不存在**——`ForgeSelf.Web/src/styles/tailwind.css:28-29` 桥接层在引用它们（`--color-accent`/`--color-teal` 工具类实为 undefined），宿主+插件共有 |
| `--el-font-family-serif` | 2 | 不存在 |
| `--el-border-radius-lg` | 1 | 不存在（EP 为 `-small/-medium/-large`，无 `lg`） |
| `--el-color-primary-dark` | 1 | 不存在（EP 为 `--el-color-primary-dark-2`） |

## §4 违反点全清单（173 文件 × 规则矩阵，格式：文件|自定义token|未知el|硬编码|小计）
```text
ForgeSelf.Web\src\App.vue|8|0|2|10
ForgeSelf.Web\src\components\AIAgentMessageStoryHost.vue|1|0|0|1
ForgeSelf.Web\src\components\BackgroundPanel.vue|7|0|0|7
ForgeSelf.Web\src\components\BackgroundSettings.vue|32|0|0|32
ForgeSelf.Web\src\components\DbAdminStoryHost.vue|0|0|1|1
ForgeSelf.Web\src\components\devtools\ColorTool.vue|0|0|88|88
ForgeSelf.Web\src\components\devtools\DataConverter.vue|1|0|0|1
ForgeSelf.Web\src\components\devtools\EncodingTool.vue|1|0|0|1
ForgeSelf.Web\src\components\devtools\EncryptTool.vue|2|0|0|2
ForgeSelf.Web\src\components\devtools\HashTool.vue|1|0|0|1
ForgeSelf.Web\src\components\devtools\JsonTool.vue|1|0|6|7
ForgeSelf.Web\src\components\devtools\JwtTool.vue|0|0|54|54
ForgeSelf.Web\src\components\devtools\QrCodeTool.vue|0|0|57|57
ForgeSelf.Web\src\components\devtools\RegexTester.vue|0|0|79|79
ForgeSelf.Web\src\components\devtools\TimestampTool.vue|0|0|62|62
ForgeSelf.Web\src\components\devtools\UuidTool.vue|0|0|39|39
ForgeSelf.Web\src\components\devtools\XmlTool.vue|1|0|6|7
ForgeSelf.Web\src\components\devtools\YamlTool.vue|1|0|6|7
ForgeSelf.Web\src\components\EmptyState.story.ts|3|0|0|3
ForgeSelf.Web\src\components\EmptyState.vue|6|0|0|6
ForgeSelf.Web\src\components\ForgeCard.story.ts|3|0|0|3
ForgeSelf.Web\src\components\ForgeCard.vue|11|0|0|11
ForgeSelf.Web\src\components\MessageInput.vue|1|0|0|1
ForgeSelf.Web\src\components\MessageItem.vue|1|0|2|3
ForgeSelf.Web\src\components\PageHead.story.ts|3|0|0|3
ForgeSelf.Web\src\components\PageHead.vue|9|0|0|9
ForgeSelf.Web\src\components\PluginCard.vue|0|0|1|1
ForgeSelf.Web\src\components\PluginDetailModal.vue|0|0|49|49
ForgeSelf.Web\src\components\scriptrunner\AIScriptDebugger.vue|0|0|3|3
ForgeSelf.Web\src\components\scriptrunner\AIScriptGenerator.vue|0|0|3|3
ForgeSelf.Web\src\components\scriptrunner\CodeSnippetCard.vue|1|0|21|22
ForgeSelf.Web\src\components\scriptrunner\CodeSnippetEditor.vue|0|0|28|28
ForgeSelf.Web\src\components\scriptrunner\CodeSnippetList.vue|0|0|34|34
ForgeSelf.Web\src\components\scriptrunner\CodeSnippetViewer.vue|1|0|44|45
ForgeSelf.Web\src\components\scriptrunner\ExecutionDetail.vue|0|0|41|41
ForgeSelf.Web\src\components\scriptrunner\ParameterEditor.vue|0|0|28|28
ForgeSelf.Web\src\components\scriptrunner\ParameterForm.vue|0|0|22|22
ForgeSelf.Web\src\components\scriptrunner\RuntimeBadge.vue|0|0|12|12
ForgeSelf.Web\src\components\scriptrunner\ScriptCard.vue|0|0|25|25
ForgeSelf.Web\src\components\scriptrunner\ScriptConsole.vue|0|0|33|33
ForgeSelf.Web\src\components\scriptrunner\ScriptEditor.vue|0|0|45|45
ForgeSelf.Web\src\components\scriptrunner\ScriptListPanel.vue|0|0|43|43
ForgeSelf.Web\src\components\scriptrunner\ScriptTemplateSelector.vue|0|0|3|3
ForgeSelf.Web\src\components\settings\AiProvidersPanel.vue|0|1|0|1
ForgeSelf.Web\src\components\settings\PermissionAuditView.vue|5|0|0|5
ForgeSelf.Web\src\components\settings\PermissionPolicyPanel.vue|31|0|0|31
ForgeSelf.Web\src\components\Sidebar.vue|5|0|3|8
ForgeSelf.Web\src\components\SmartTerminalStoryHost.vue|0|0|1|1
ForgeSelf.Web\src\components\StatusBar.vue|4|0|0|4
ForgeSelf.Web\src\components\systemmonitor\DiskDetail.vue|0|0|29|29
ForgeSelf.Web\src\components\systemmonitor\NetworkDetail.vue|0|0|25|25
ForgeSelf.Web\src\components\systemmonitor\OverviewCards.vue|2|0|7|9
ForgeSelf.Web\src\components\systemmonitor\ProcessList.vue|0|0|30|30
ForgeSelf.Web\src\components\systemmonitor\TrendChart.vue|0|0|3|3
ForgeSelf.Web\src\components\texttools\EncodingPanel.vue|2|0|0|2
ForgeSelf.Web\src\components\texttools\FormatterPanel.vue|2|0|0|2
ForgeSelf.Web\src\components\texttools\HashPanel.vue|2|0|0|2
ForgeSelf.Web\src\components\texttools\StatsPanel.vue|0|0|1|1
ForgeSelf.Web\src\components\texttools\TextInput.vue|1|0|0|1
ForgeSelf.Web\src\components\texttools\TextOutput.vue|1|0|0|1
ForgeSelf.Web\src\components\texttools\ToolTabs.vue|0|0|1|1
ForgeSelf.Web\src\components\TopNavbar.vue|17|0|0|17
ForgeSelf.Web\src\components\workflow\AIWorkflowGenerator.vue|0|0|54|54
ForgeSelf.Web\src\components\workflow\ExecutionLog.vue|0|0|25|25
ForgeSelf.Web\src\components\workflow\ScriptSelector.vue|0|0|34|34
ForgeSelf.Web\src\components\workflow\ScriptStepEditor.vue|0|0|33|33
ForgeSelf.Web\src\components\workflow\StepTimeline.vue|0|0|45|45
ForgeSelf.Web\src\components\workflow\WorkflowCard.vue|0|0|41|41
ForgeSelf.Web\src\components\workflow\WorkflowEditor.vue|0|0|62|62
ForgeSelf.Web\src\components\workflow\WorkflowExecution.vue|0|0|51|51
ForgeSelf.Web\src\data\features.ts|4|0|0|4
ForgeSelf.Web\src\styles\tailwind.css|24|2|0|26
ForgeSelf.Web\src\utils\canvas-theme.ts|1|0|0|1
ForgeSelf.Web\src\views\AgentsManageView.vue|54|0|0|54
ForgeSelf.Web\src\views\AllFeaturesView.vue|60|0|0|60
ForgeSelf.Web\src\views\ChatRecordsView.vue|60|0|0|60
ForgeSelf.Web\src\views\ChatView.vue|74|0|0|74
ForgeSelf.Web\src\views\CodeSnippetsView.vue|5|0|0|5
ForgeSelf.Web\src\views\DevToolsView.vue|13|1|0|14
ForgeSelf.Web\src\views\MemoryView.vue|96|0|0|96
ForgeSelf.Web\src\views\PersonalLibraryView.vue|57|0|0|57
ForgeSelf.Web\src\views\PluginDetail.vue|9|0|0|9
ForgeSelf.Web\src\views\PluginImportExport.vue|77|0|0|77
ForgeSelf.Web\src\views\PluginScaffolder.vue|46|0|0|46
ForgeSelf.Web\src\views\PluginStore.vue|14|0|0|14
ForgeSelf.Web\src\views\PluginUpdates.vue|37|0|0|37
ForgeSelf.Web\src\views\ProfileView.vue|107|0|0|107
ForgeSelf.Web\src\views\PromptsView.vue|52|0|0|52
ForgeSelf.Web\src\views\ScriptLibrary.vue|84|0|0|84
ForgeSelf.Web\src\views\SettingsView.vue|6|0|0|6
ForgeSelf.Web\src\views\SkillsView.vue|90|0|0|90
ForgeSelf.Web\src\views\SystemMonitorView.vue|17|0|11|28
ForgeSelf.Web\src\views\TaskCenterView.vue|40|0|0|40
ForgeSelf.Web\src\views\TextToolsView.vue|7|0|0|7
ForgeSelf.Web\src\views\WorkflowLibrary.vue|84|6|0|90
Plugins\AgentHub\web\src\AgentHubAgentCard.vue|0|1|0|1
Plugins\AgentHub\web\src\AgentHubEventStream.vue|0|2|0|2
Plugins\AgentHub\web\src\AgentHubView.vue|0|8|2|10
Plugins\AIAgent\web\src\agent-core\components\blocks\BlockRenderer.vue|0|0|1|1
Plugins\AIAgent\web\src\agent-core\styles\blocks.css|116|0|73|189
Plugins\AIAgent\web\src\agent-core\styles\widget.css|324|0|130|454
Plugins\AIAgent\web\src\AiAgentView.vue|0|3|0|3
Plugins\AIAgent\web\src\assistant\AgentThread.vue|2|0|0|2
Plugins\AIAgent\web\src\assistant\AgentWorkspace.vue|4|0|0|4
Plugins\AIAgent\web\src\assistant\assistant.css|105|0|4|109
Plugins\AIAgent\web\src\components\AgentEditDialog.vue|0|4|4|8
Plugins\AIAgent\web\src\components\ContextGroup.vue|0|3|0|3
Plugins\AIAgent\web\src\components\ContextPanel.vue|0|8|0|8
Plugins\AIAgent\web\src\components\DirectoryPickerDialog.vue|0|2|0|2
Plugins\AIAgent\web\src\components\FileEditor.vue|0|4|0|4
Plugins\AIAgent\web\src\components\RunRecordPanel.vue|0|8|0|8
Plugins\AIAgent\web\src\components\SessionPanel.vue|0|12|0|12
Plugins\AIAgent\web\src\components\StepProgressCard.vue|0|4|0|4
Plugins\AIAgent\web\src\components\TagInput.vue|0|0|6|6
Plugins\AIAgent\web\src\styles\tailwind.css|22|0|0|22
Plugins\DesignSystem\web\src\components\PanelState.vue|13|0|0|13
Plugins\DesignSystem\web\src\delivery\DeliveryMode.vue|45|0|0|45
Plugins\DesignSystem\web\src\design\skin.ts|1|0|0|1
Plugins\DesignSystem\web\src\DesignSystemView.vue|80|0|0|80
Plugins\DesignSystem\web\src\sections\AuditBoard.vue|87|0|0|87
Plugins\DesignSystem\web\src\sections\BrandAssets.vue|45|0|0|45
Plugins\DesignSystem\web\src\sections\ColorLab.vue|58|0|0|58
Plugins\DesignSystem\web\src\sections\ComponentGallery.vue|111|0|0|111
Plugins\DesignSystem\web\src\sections\DensityScales.vue|40|0|0|40
Plugins\DesignSystem\web\src\sections\ExportCenter.vue|64|0|0|64
Plugins\DesignSystem\web\src\sections\Guidelines.vue|75|0|0|75
Plugins\DesignSystem\web\src\sections\IconLibrary.vue|48|0|0|48
Plugins\DesignSystem\web\src\sections\Projects.vue|82|0|0|82
Plugins\DesignSystem\web\src\sections\ReleaseBoard.vue|68|0|0|68
Plugins\DesignSystem\web\src\sections\ShadowMotion.vue|46|0|0|46
Plugins\DesignSystem\web\src\sections\ThemeLab.vue|50|0|0|50
Plugins\DesignSystem\web\src\sections\TokenShowcase.vue|30|0|0|30
Plugins\DesignSystem\web\src\sections\TokenStudio.vue|40|0|0|40
Plugins\DesignSystem\web\src\sections\TypeScale.vue|55|0|0|55
Plugins\DesignSystem\web\src\shell\ModeBar.vue|16|0|0|16
Plugins\DesignSystem\web\src\showroom\CompareStrip.vue|34|0|0|34
Plugins\DesignSystem\web\src\showroom\DeviceFrame.vue|3|0|0|3
Plugins\DesignSystem\web\src\showroom\mannequins\mannequin.css|362|0|2|364
Plugins\DesignSystem\web\src\showroom\Showroom.vue|5|0|0|5
Plugins\DesignSystem\web\src\showroom\Stage.vue|16|0|1|17
Plugins\DesignSystem\web\src\showroom\TunePanel.vue|37|0|0|37
Plugins\DesignSystem\web\src\showroom\Wardrobe.vue|28|0|0|28
Plugins\DesignSystem\web\src\start\StartMode.vue|67|0|0|67
Plugins\DesignSystem\web\src\state.ts|1|0|0|1
Plugins\DesignSystem\web\src\styles\base.css|83|0|0|83
Plugins\DesignSystem\web\src\styles\tokens.css|119|0|1|120
Plugins\FileTools\web\src\components\ArchivePanel.vue|5|0|0|5
Plugins\FileTools\web\src\components\CleanupPanel.vue|4|0|0|4
Plugins\FileTools\web\src\components\FoldersPanel.vue|8|0|0|8
Plugins\FileTools\web\src\components\RenamePanel.vue|4|0|0|4
Plugins\FileTools\web\src\components\StatsPanel.vue|6|0|0|6
Plugins\FileTools\web\src\styles\tailwind.css|22|0|0|22
Plugins\FileTools\web\src\views\FileToolsView.vue|16|0|0|16
Plugins\Home\web\src\HomeView.vue|83|0|2|85
Plugins\Home\web\src\styles\tailwind.css|22|0|0|22
Plugins\ImGateway\web\src\components\ChatList.vue|11|0|0|11
Plugins\ImGateway\web\src\components\ChatThread.vue|24|0|0|24
Plugins\ImGateway\web\src\components\TagListInput.vue|13|0|0|13
Plugins\ImGateway\web\src\ImGatewayView.vue|118|0|0|118
Plugins\McpCenter\web\src\McpCenterView.vue|6|0|0|6
Plugins\McpCenter\web\src\styles\tailwind.css|22|0|0|22
Plugins\Sems\web\src\CommandList.vue|2|4|0|6
Plugins\Sems\web\src\DirectoryPickerDialog.vue|2|2|0|4
Plugins\Sems\web\src\LegacySemsView.vue|31|0|0|31
Plugins\Sems\web\src\ProjectCard.vue|0|3|0|3
Plugins\Sems\web\src\ProjectEditDialog.vue|2|1|0|3
Plugins\Sems\web\src\RunPanel.vue|0|10|0|10
Plugins\Sems\web\src\SpecPanel.vue|13|1|0|14
Plugins\Sems\web\src\workbench\workbench.css|122|0|0|122
Plugins\SmartTerminal\web\src\styles.css|137|0|1|138
Plugins\TodoTracker\web\src\components\TaskDrawer.vue|0|0|2|2
Plugins\TodoTracker\web\src\styles\tailwind.css|22|0|0|22
Plugins\ToolBridge\web\src\ToolBridgeView.vue|31|2|0|33
```

## §5 主题文件外的 token 定义点（11 文件——「谁在发明 token」）
| 文件 | 定义数 | 定性 |
|------|--------|------|
| `Plugins/DesignSystem/web/src/styles/tokens.css` | 75 | 插件自建 token 体系（**产品本体**，待拍板豁免或迁移） |
| `Plugins/AIAgent/web/src/agent-core/styles/widget.css` | 55 | 自建 `--aiw-*` 族（独立浮窗） |
| `ForgeSelf.Web/src/styles/tailwind.css` | 24 | Tailwind 桥接（值引用 el，但 2 处引用幻影 token 必修） |
| `Plugins/{AIAgent,FileTools,Home,TodoTracker,McpCenter}/web/src/styles/tailwind.css` | 22×5 | 各插件 Tailwind 桥接 |
| `Plugins/AIAgent/web/src/agent-core/styles/blocks.css` | 11 | 自建 token |
| `ForgeSelf.Web/src/App.vue` / `Plugins/AIAgent/.../ContextPanel.vue` | 2 / 1 | 零散定义 |

## §6 自造弹窗盘点（R5 · 输入11「避免造轮子」）
- 自造 modal 实现 **9 文件**：`ForgeSelf.Web/src/components/PluginDetailModal.vue`、`views/MemoryView.vue`、`views/ScriptLibrary.vue`、`views/SkillsView.vue`、`views/WorkflowLibrary.vue`、`Plugins/FileTools/.../CleanupPanel.vue`、`.../RenamePanel.vue`、`Plugins/QuickLinks/.../LinkFormModal.vue`、`Plugins/QuickLinks/QuickLinksView.vue`。
- 正确使用 `<el-dialog>` 的文件 9 个（两阵营并存）。SkillsView 弹窗已实证浅色主题文字 1.06:1（输入10），是自造弹窗主题跟随缺失的直接恶果。

## §7 门禁套件灵敏度验证（复制后回归）
- `node scripts/check-theme-tokens.mjs scripts/fixtures/theme-tokens-anti-patterns.vue` → **32 处全捕获**（4 未知 el + 16 未授权自定义 + 12 硬编码；审计期白名单为空故较上游 21 处多 11 处白名单豁免项），控制组（真实 el token / transparent / currentColor）零误报。
- `node scripts/gen-element-tokens.mjs` → element-plus 2.14.3，**568** 个合法 `--el-*` token。

## §8 风险与局限
- 审计不含 `.temp/`、`ct/` 画廊、`forgeself-design/` 历史原型、`dist/` 构建物（均非页面代码；ct 画廊仍受既有 style-tokens 棘轮约束）。
- 命名色检查只在 `<style>`/css/scss 上下文生效（.ts 内字符串不查——上游设计，避免误伤类型联合）。
- 6007 处为「静态违反点」数，修复批按 03-plan 分域收敛时行级数量会随合并/删重复略有浮动，以收敛后门禁 0 处为准。
- **灾后复验（2026-11-23，输入12 回迁批）**：本文件与门禁套件自灾前备份逐字节回迁（SHA256 逐文件一致）。11-23 复跑基线为组织仓 main @ 9ad7c48，得 **6006 处/172 文件**（custom 4392 / hard 1522 / unknownEl 92）——与 11-20 审计的差值恰为 `ForgeSelf.Web/src/utils/canvas-theme.ts` ×1（该文件属 theme-canvas-unify 在途批次、未入组织仓，仅存于备份，回迁走备份盘点菜单）；其余 172 文件计数与 §4 矩阵逐行一致（Compare-Object 差异=1）。`.temp/theme-tokens-audit-full.log` 与 `theme-tokens-audit-matrix.txt` 现为 11-23 复验版；`gen-element-tokens.mjs` 重生成待新基线 `pnpm install` 后补跑（`element-tokens.json` 568 token 已随套件回迁、当前有效）。

## §9 收口补充（2026-11-24 整合批回填，来源:输入27）
- 本批（审计批）验证结论分级：基线矩阵/门禁套件/弹窗盘点/灵敏度验证 = **[Verified]**（2026-11-20~11-23 实跑留档于本文件 §1-§8；灾后回迁经 SHA256 逐文件一致复核）。
- 审计产出物去向：门禁套件五件（check-theme-tokens.mjs / gen-element-tokens.mjs / element-tokens.json / theme-check.mjs / fixtures 旧 demo）与根 package.json 三别名由**执行链 2026-11-23-theme-token-unify 收编入库并演进**（checker 增 fileExemptions 与结构化册；demo 入口改 theme-tokens-antipattern-demo + verify-theme-demo.mjs，本批旧 fixture 保留为上游史料）；R1-R6 违规全量修复亦由该链 W1-W7 完成（AC1 全仓 0）。
- 结论：本批为「审计+门禁套件」批，执行收口在下游链完成，本目录工件作为上游依据归档。
