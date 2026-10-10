# Specification

> Task ID: PILOT-2026-1010-ABOUTVER

## Acceptance Criteria（AC，机械可校验；AC1-AC4 见「交互验收标准」）
- AC5 AboutPanel 挂载时调用 `updateApi.getStatus()`（唯一版本数据源，同 UpdatePanel）。
- AC6 展示位置 = 原 `<code>` 行，格式 `v{{ version }}`，类名不变。

## Input
无用户输入；数据源为 `GET /api/update/status` → `UpdateStatus.currentVersion: string`。

## Output
`<code class="text-sm text-text-secondary">` 文本：
- 加载中：`v…`
- 成功：`v{currentVersion}`
- 失败或空值：`v未知`

## Business Rules
- 版本文案来源于运行实例 API，不读构建期常量（package.json 等不属于真实运行版本）。

## Boundary Conditions
- 接口慢：持续显示 `v…`（无超时需求，与 UpdatePanel 行为一致）。
- `currentVersion` 为 ''/null：按"空值降级"显示 `v未知`（等同失败语义）。

## Error Handling
- try/catch 包裹整个取数调用；失败置「未知」，不抛错、不弹 ElMessage（避免污染设置页）。

## Compatibility
- 不改 updateApi/request 接口；不改 UpdatePanel；组件对外无 props/事件变化。

## Non-functional Requirements
- 不新增依赖；不引状态库；单组件内完成；TS 严格类型通过 vue-tsc。

## Interaction Design（UI 变更必填档 · 2026-10-08 用户规制）
本轮为既有静态展示位的取数替换，无新增交互元素。清单化如下：

| 交互点 | 触发 | 用户看到什么 | 状态模型 | 反馈 | 动态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 版本号展示 | 面板挂载 | `v…` → `v2.4.0.x`（真实回显） | 双态 hidden/loaded（+失败态复用 loaded 文案） | 无额外反馈（不弹气泡） | 随升级自动刷新来源 | 慢网络占位不闪跳；失败“未知”不误报为可用版本 |

### 交互验收标准（AC，人工可测）
- AC1 进设置→关于，版本行以 `v` 开头；加载短暂 `v…`。
- AC2 接口正常时显示与 UpdatePanel「当前版本」一致的字符串。
- AC3 接口异常时显示 `v未知`，页面其他区域不受影响，无控制台未处理错误。
- AC4 布局与改前一致（code 样式类不变）。

### 走查项（DoD 绑定）
- 对照 UpdatePanel 当前版本文案格式一致性（e2e 未跑，理由见 03-plan；走查经由单测断言完成，live 断言标注 Unknown/Inferred）。

## Acceptance Criteria
- [x] AC1 挂载占位 `v…`（单测覆盖）
- [x] AC2 成功回显真实版本？真实值经 API 复核（见 05-evidence：curl 复核 401 则记 Unknown）
- [x] AC3 失败降级 `v未知` 且不抛错（单测覆盖）
- [x] AC4 样式类与布局不变（diff 审查：仅 v0.1.0 行 + script 块变更）

## Unknown
| 不确定项 | 影响 | 澄清方式 |
| --- | --- | --- |
| 51888 运行实例接口真实返回值（可能 401 需 token） | 无法直接在 live 界面取证 | 尝试只读 GET 复核；不行则以单测 + UpdatePanel 同源引用为据，标注证据等级 |
