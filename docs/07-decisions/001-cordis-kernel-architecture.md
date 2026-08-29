# ADR 001 · Cordis 内核与插件化重构的关键决策

> 状态：已决策并实施（2026-08-16 复核：D1/D3/D4/D5 已落地；D2 部分落地——`ForgeSelf.Abstractions` 已建，独立程序集仅 MemorySystem 已拆，其余 11 插件仍内嵌主程序集）
> 依据：`00-vision/`（P4 插件化扩展、D2 主程序只留宿主能力、愿景「可自我演进」）
> 前置：`../06-research/001-deepseek-harness-plugin-architecture.md`、`../15-roadmap/plugin-architecture.md`

本项目**尚未上线**，决策不考虑向后兼容，以最佳架构为唯一标准。

## 决策表

| # | 决策 | 背景 | 理由 | 准则（可复用） |
|---|------|------|------|----------------|
| D1 | 插件契约统一为 `IPlugin.Apply(IContext)`，生命周期由 Fiber 统一管理，**不留** `IReversiblePlugin` / 旧 `Initialize(IServiceProvider)` / `Start`/`Stop`/`Destroy` 兼容层 | 现有 `IPlugin` 有多生命周期方法，且拿裸 `IServiceProvider`；`IReversiblePlugin` 双轨增加复杂度 | Cordis 插件本质是「贡献函数」：一切注册皆 effect、卸载即逆序回滚，多生命周期方法破坏可逆性 | 插件只贡献，不管理自己的生命周期 |
| D2 | 12 个插件**一步到位**拆为独立程序集，新增 `ForgeSelf.Abstractions` 契约程序集（业务接缝 + `IPlugin` + 共享 DTO），宿主与插件都引用它 | 现状插件编入主 exe，`EntryAssembly` 全指向 `OpenForgeSelf.dll` | P4/D2 要求「主程序只留宿主能力」；独立程序集是热更新与隔离的前提 | 能力代码不进宿主，共享契约独立成集 |
| D3 | 版本目录 side-by-side：`Plugins/<id>/versions/<semver>/` + `current` 指针，保留 **N=2**（当前 + 上一版本） | 现状原地覆盖 DLL 触发文件锁 | 永不原地覆盖；保留上一版本即可回滚，N=2 平衡磁盘与回滚能力 | 升级 = 新版本目录 + 指针切换，旧版延迟回收 |
| D4 | 前端用轻量动态 `import()` + `contributes` 协议 + tabs 挂载，**不上 module-federation** | 前端是单 Vue SPA，无多应用微前端诉求 | module-federation 复杂度与「本地优先、普通用户可及」不匹配；动态 import 已满足按插件装载 | 能用动态装载解决，不引入微前端框架 |
| D5 | 按 P0→P5 逐阶段出 `specs/NNN-*/` 的 spec→plan→tasks 分批实现 | 原路线图 A-E 已重组为 P0-P5 | 每阶段独立可验证；符合 Loop Engineering 与 speckit SDD 纪律 | 大重构分阶段，每阶段可独立验证与回滚 |

## 落点

- 契约与阶段见 [`../15-roadmap/plugin-architecture.md`](../15-roadmap/plugin-architecture.md)
- 内核设计见 [`../01-architecture/cordis-kernel.md`](../01-architecture/cordis-kernel.md)
- 功能档案见 [`../02-features/027-cordis-kernel.md`](../02-features/027-cordis-kernel.md)

## 实施状态（2026-08-16 按代码复核）

| # | 决策 | 实施状态 |
|---|------|----------|
| D1 | 契约统一为 `IPlugin.Apply(IContext)`，生命周期由 Fiber 统一 | ✅ 已实施：12 个插件全部迁移到 `Apply(IContext)` 自注册（`ctx.Get<IServiceCollection>()` + `ctx.Effect`），`Fiber.Mount/Dispose` 统一生命周期 |
| D2 | 12 插件一步到位拆独立程序集 + 新增 `ForgeSelf.Abstractions` | 🟡 部分：`ForgeSelf.Abstractions` 已建立（契约 + DTO）；独立程序集仅 `MemorySystem` 已拆（其余 11 个仍内嵌主程序集） |
| D3 | 版本目录 side-by-side + `current` 指针，N=2 | ✅ 已实施：`PluginVersionLayout`（原子切换指针）+ `PluginAssemblyUnloader`（破锁/延迟删除）+ `PluginHotReloadWatcher`（自动 reload） |
| D4 | 前端轻量动态 `import()` + `contributes` 协议 + tabs 挂载 | ✅ 已实施：`pluginManifest` store + `mergeFeatureList` + `router/dynamicPlugins.ts`（`/plugin-view` 动态 import；试点 MemoryView/QuickLinksView/TodoView） |
| D5 | 按 P0→P5 逐阶段出 spec→plan→tasks 分批实现 | ✅ 已实施：P0/P2/P5 完成，P1/P3/P4 部分（见路线图完成度） |
