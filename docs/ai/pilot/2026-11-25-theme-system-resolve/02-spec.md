# 02 Spec

## Functional Requirements

- FR-1 外观设置与侧栏主题控件只暴露 `light` / `dark` 两个主题选项。
- FR-2 “跟随系统”作为独立开关存在；开启时读取 `prefers-color-scheme`，解析为实际 `light` 或 `dark`。
- FR-3 跟随系统开启时，用户界面的浅色/深色选中态始终反映实际解析结果。
- FR-4 系统偏好变化时，跟随系统模式实时更新实际主题和根节点；手动模式不监听系统变化覆盖用户选择。
- FR-5 持久化只保存实际主题和独立的跟随开关；根节点不得把 `system` 当作实际主题。
- FR-6 旧版本持久化的 `system` 值自动迁移为“跟随系统开关开启”，不继续将 `system` 写回实际主题键。

## Input / Output

- Input: 用户选择浅色、深色或切换跟随系统；操作系统 prefers-color-scheme change。
- Output: 根节点 `data-theme` 与实际主题一致；浅色/深色 radio 按钮只允许一个选中；系统偏好变化时页面同步。

## Business Rules

- BR-1 实际主题枚举只能是 `light` / `dark`。
- BR-2 只有跟随开关控制系统监听是否启动。
- BR-3 手动选择浅色或深色等价于关闭跟随开关。
- BR-4 系统变化不覆盖手动选择。
- BR-5 legacy `"system"` 存储值只用于迁移，迁移后不得保留为实际主题。

## Boundary Conditions

- 浏览器或测试环境不提供 `matchMedia`: 缺省解析为浅色，跟随开关仍可显示实际浅色。
- localStorage 不可用：运行态仍可切换，不要求持久化成功。
- 初始化时系统为浅色或深色：对应浅色/深色选中态立即生效。

## Failure Modes

- PC-025: 明暗主题切换后文字或选中态不一致；本任务通过实际解析主题驱动根节点和选中态，且亮/暗走查双态验证。
- Existing `system` value from older builds: migration must not leave an invalid third actual mode.

## Interaction Design

| 交互点 | 触发 | 结果 | 状态模型 | 反馈 | 空态 | 边界 |
|---|---|---|---|---|---|---|
| 主题选择 | 点击浅色或深色 | `light`/`dark` 激活，跟随系统开关关闭，页面立即应用 | 手动浅色 / 手动深色 | radio 选中态立即变化 | 无 | 任一时刻只一个主题激活 |
| 跟随系统开关 | 开/关 | 开：按系统偏好解析并同步；关：固定到当前实际浅/深主题 | 跟随中 / 手动固定 | 开关与实际 radio 双处同步 | 不适用 | 系统变化仅跟随中生效 |
| 系统偏好变化 | OS 切换浅/深 | 实际主题、html 类、radio 高亮同时切换 | 自动浅色 / 自动深色 | 无额外 toast | 不适用 | 手动模式不响应 |

## Acceptance Criteria

- [ ] AC-1 AppearancePanel 和 Sidebar 不渲染独立的第三主题值。
- [ ] AC-2 开启跟随系统后系统 dark/light 分别得到 dark/light 实际状态和选中态。
- [ ] AC-3 系统偏好运行时变化后实际主题与选中态同步。
- [ ] AC-4 手动切换后系统变化不覆盖手动主题。
- [ ] AC-5 legacy system 存储值迁移为跟随开关开启且不再持久化 system 实际值。
- [ ] AC-6 快档测试绿色。
