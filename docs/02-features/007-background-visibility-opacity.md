---
feature_key: F007
feature_no: 007
status: implemented
last_updated: 2026-10-06
aliases: ["007-background-visibility-opacity"]
---

# 007 背景可见性与透明度 — 功能需求与设计

> 功能编号：007
> 状态：已实现
> 关联：006 背景图模式（历史 specs/007 已弃用）
> 最后更新：2026-08-12

## 1. 功能需求

### 1.1 背景
背景图模式下，用户需要调节背景的可见度/透明度，让前景内容（卡片、文本）在背景上清晰可读，又不丢失氛围。

### 1.2 目标
1. 提供背景图"开关"与"透明度/遮罩浓度"调节；
2. 调节实时生效，不刷新页面；
3. 与 006 的 `<img>` 渲染方案、token 体系一致。

## 2. 设计

### 2.1 实现方式
- 透明度直接作用于背景 `<img>` 的内联 `:style="{ opacity: appearanceStore.backgroundOpacity / 100 }"`（`App.vue`），**不**使用 `color-mix` 遮罩层；
- 状态存 `stores/appearance.ts`，由 `bg-image-mode.css` 消费；
- 不引入任何自定义 token，遵循 006 铁律。

### 2.2 实现位置
| 文件 | 职责 |
|------|------|
| `src/stores/appearance.ts` | 可见性/透明度状态 |
| `src/styles/themes/bg-image-mode.css` | 遮罩派生 |
| `src/components/settings/AppearancePanel.vue` | 调节控件 |

## 3. 使用指南
设置 → 外观 → 背景图模式下拖动"透明度"滑杆；关闭开关即恢复无背景。

## 4. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| `AppearancePanel.test.ts` | 透明度滑杆交互、开关切换 |

## 5. 注意事项
- 透明度调过低（背景过亮）会稀释前景对比度，属用户主观选择，不在代码强制下限。
