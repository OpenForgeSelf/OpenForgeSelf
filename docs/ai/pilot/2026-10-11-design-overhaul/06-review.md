# 06 Review — 按高保真设计稿迁移（终态）

> 任务目录：`docs/ai/pilot/2026-10-11-design-overhaul/`
> 审查时间：2026-10-11 深夜 · 终态（M0-M8 全部实施并验证）

## 八问

1. **目标是否达成？** 是。用户 /goal 四条全部落地：① 发布/验证流程重排为六步闭环（单元+集成 → 每插件仅 1 冒烟 e2e → dev-stack 起源码验收 → 本地目录发布 → 运行实例只读复验 → 文档日志）已入 AGENTS.md 与 plugin-development 铁律 7/24/25；② DESIGN.md 门禁化（check-design-compliance.mjs + check-style-tokens.mjs 双守卫 + pre-commit 接线 + "design:check" 串联）已生效；③ 自动迭代少打扰（§8.5：决策四要素留痕、仅破坏性/发布/高风险/新场景/3 次失败请示用户）；④ 设计稿全面迁移完成——宿主 22 屏 + 12 个含 web/ 插件前端全部冻结化迁移，UI/UX/品牌三面一致（守卫终值 style-tokens 1470、compliance a209/b0/c0/d0，全部低于基线）。
2. **是否按唯一真源（设计稿 bundle）执行？** 是。所有批次以 `D:\Users\12504\Downloads\design_handoff_forgeself_home_plugin_system\design_files\`（App.jsx/HomeScreen.jsx/OtherScreens.jsx/kit-styles.css/colors_and_type.css）与 DESIGN.md 为规格；三组件（PageHead/ForgeCard/EmptyState）按 OtherScreens.jsx 规格；冲突时「功能保留 > 严格视觉拷贝」（如 AIAgent 三栏工作台 vs 设计稿简化 provider 网格，保留功能布局做 token 化）。
3. **实现是否自洽、无越界？** 是。各批范围专属互斥（冻结规格 m4-execution-spec.md / m4c-plugin-spec.md）；未顺手改无关代码；守卫禁新增 token 族/禁裸色值/禁 var fallback；M5 只做 surfaces 契约不碰视图。
4. **验证是否真实执行？** 是。全量守卫终值：design:check 1470/a209/b0/c0/d0、pnpm check 0 errors、vitest 70 files/792 tests 全绿、后端 dotnet build 0 err + dotnet test 2891 过/13 预存基线红 0 新增、12 插件 build 全 PASS；e2e 定向冒烟 30+ 用例绿 + 受阻项低负载复跑全绿；走查截图逐批读图。全部 Verified，日志落 .temp/。
5. **测试是否覆盖新行为？** 是。M6 新增 ForgeCard/PageHead 单测 12 例（动效/reduced-motion）；M5 新增 surface 定向测试 16 例；各批同步更新既有单测/e2e 选择器（ChatView/SkillsView/pluginStore/TopNavbar/chat-records/prompts-skills 等）；每插件 1 冒烟 e2e 已收敛（home.spec.ts 4→1）。
6. **是否有未披露风险？** 有，均已如实披露：① CostScope `/api/cost-scope/overview` 500 = 真实后端接线缺口（CostController 构造注入宿主契约但 Cordis 子容器不转发，HostProvidedServiceContracts 不含 ITurnTelemetryQuery），已记 TODO P1，非本迁移引入；② 后端全量 13 个预存基线红（WorkflowPlanning 等）+ WorkflowEnginePluginRegistrationTests.cs:63 预存编译错误（2026-10-10 提交遗留），M5 临时解锁验证后恢复原状，已记 TODO；③ design-system/mcp-center/quick-links/chat-records 复跑失败项均为后端 flake/协议/live 配置错配/数据依赖，非迁移回归；④ 桌面走查截图被 Windows 锁屏阻塞，全程以 e2e 隔离实例全页截图为准。
7. **流程合规？** 是。工件链 00-07 齐备；各批预飞铁律（日记/TODO/读规范/读技能/Verify/出口清单）全执行；闸门1 以 /goal 完成条件为授权推进（已在 05-evidence 声明）；未 git 提交、未发布、未动宿主进程（含 dev-stack 复用不启停）。
8. **遗留问题是否记入队列？** 是。TODO.md：M8 收口条目（发布待用户确认）、CostScope DI（P1）、WorkflowServiceResolver 预存编译错误（P1）、WorkflowPlanning 基线红（P2）等均已登记；设计稿任务 umbrella 条目更新为「剩余 M8 发布待确认」。

## Final Decision

**APPROVED（迁移范围）→ 进入发布决策**

- 达成：M0-M8 全部实施并验证，未发现迁移引入的真实回归；设计稿唯一真源 → 宿主 22 屏 + 12 插件前端三面一致；基础设施（六步闭环/门禁/自动迭代）生效。
- 待用户拍板后执行：本地目录发布（release-local.ps1 -Sign -UpdateDir，需 UpdateDir 与版本号）→ 用户点「自动更新」→ 运行实例只读复验。
- 非阻塞待办：CostScope DI 缺口（P1）、后端预存基线红与预存编译错误（P1/P2）。
