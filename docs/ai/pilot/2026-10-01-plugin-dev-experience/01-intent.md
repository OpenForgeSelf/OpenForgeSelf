# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做到什么程度」，不提前决定具体代码实现。
> Task ID：PILOT-plugin-dev-experience ｜ 日期：2026-10-01

## Problem

插件开发/调试回路存在三处结构性摩擦：

1. **R1 更新链路与版本号强耦合**：改一行 C# 后，让运行中的宿主看到新代码需要「手动 bump plugin.json 版本 → publish-plugin.ps1 → POST /api/plugin/update/{id}」三步，且更新 API 要求 stage 版本**严格大于**当前版本——高频调试会刷出大量垃圾版本号，忘 bump 则一切静默无效。
2. **R2 无本地热载能力**：ALC 直接加载 `versions/<current>/` 的 DLL（被进程独占锁），叠加「禁止 agent 停/启/杀宿主」铁律，本地开发连"改完重启看效果"都不可行。
3. **R3 前端产物断链**：插件 `web/dist`（gitignore）与宿主运行目录无自动同步，实测运行实例的前端产物落后源码 3 天；插件加载失败时宿主只保留 `ex.Message`，前端只显示一个 Error 徽标无原因；日志无 PluginId 维度、级别硬编码 Info（Debug 全丢），定位一次插件问题要在混合大日志里翻找。

## Why

- 本项目是 AI-agent-first 自迭代工具系统，**插件是主要交付物**（已有 18 个），开发/调试插件的效率直接决定迭代速度。
- 三摩擦的共同根因是"运行时正确性机制（版本化/锁/缓存）没有为开发态留出口"，属于结构性缺口而非操作技巧问题，值得一次性架构级补齐。

## Expected Outcome

- 开发宿主（dev 开关开启的**新起实例**）上：改插件 C# 后**一条命令、同版本号**即可重载生效；改插件 UI 后保存 → 秒级刷新可见（watch 模式），并可选 opt-in 真 HMR。
- 任何宿主上：插件加载/运行失败能查到完整原因（消息+类型+堆栈+时间）；dev 实例上插件日志带 `[plugin:<id>]` 维度并可按插件分文件查看；`/api/dev/diagnostics` 一眼看全 18 个插件状态。
- 用户手上的 :51888 Production 实例行为**零变化**（dev 能力默认全关）。

## Constraints

规范 §1 硬性约束逐条对照：

1. 不修改生产环境 → 只新增 dev-gated 能力，不改 Production 行为；
2. 不修改数据库结构 → 无 DB 变更（PluginErrorStore 为进程内内存存储）；
3. 不修改鉴权/权限/支付/安全核心逻辑 → dev 端点复用既有 `[Authorize("ApiKeyPolicy")]`，不改鉴权体系；
4. 不新增大规模依赖 → 零新 NuGet/npm 依赖（HMR 用各插件已有 vite）；
5. 不进行无关重构 → 177 处 `XTrace.Log` 静态调用**不机械替换**（选型已否决）；
6. 不修改与本任务无关的文件 → 文档漂移修正仅限 6 处已确认文件；
7. 不为展示能力扩大范围 → 2B HMR 为 opt-in，不默认启用；
8. 必须可运行实际验证 → dotnet build/test + pnpm check/test + dev 宿主实跑；
9. 结论基于真实仓库 → 方案报告中全部关键断言带文件行号；
10. 验证失败不伪造 → 既有失败（基线 118）与本次引入严格分离。

项目铁律附加约束：禁停/启/杀用户运行中宿主进程（AGENTS.md:30/95/107）；活动插件目录只放插件自身 DLL、禁宿主共享 DLL（R8②）；端口禁硬编码 7102/7002。

## Success Criteria

以下均可被实际命令/运行判定：

1. `dotnet build` / `dotnet test`（verbose）结果**不劣于**基线（build 0 错误；test 失败数 ≤ 118 且无新增失败名）；
2. dev 宿主上，不修改 plugin.json 版本号，连续 `POST /api/dev/plugin/<id>/reload` ≥ 3 次全部成功，且每次 reload 后插件行为反映最新编译产物（以一条探针日志/端点输出佐证）；
3. shadow 目录无泄漏：reload 多次后 `forge-dev-shadow` 下每个插件仅保留当前 hash 目录；
4. 制造一个必然加载失败的插件（如 EntryType 拼错），`GET /api/plugin` 返回的该插件条目含完整错误原因（消息+类型+堆栈），`/api/dev/diagnostics` 同样可见；
5. dev 宿主日志中插件生命周期日志带 `[plugin:<id>]` 前缀；`log/plugins/<id>/` 按插件分文件可查；
6. `pnpm run check` / `pnpm run test` 不劣于基线（check 既有红条目不新增）；
7. `FORGESELF_DEV_MODE` 未设置时，宿主启动与 API 行为与改动前一致（diagnostics/reload 端点 404）；
8. 6 处文档漂移修正后，`docs/` 与技能中不再有"watcher 自动重载"等与现实矛盾的陈述。
