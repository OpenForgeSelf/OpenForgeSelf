# 07 Final Report — 2026-10-15 聊天页展示优化三项

## 交付

| 需求 | 交付 |
| --- | --- |
| ① 状态提示条贴输入框上方、不居中 | `.aiw-error` / `.aiw-retry` / `.aiw-reconnect` / `.aiw-fallback` 四条统一搬入新增 `.aiw-status`（在消息区之外、footer 之上） |
| ② 刻度条点最后一条后高亮错位 | 根因修复（容差 8px < 跳转偏移 12px）+ 点位即时置亮 + 400ms 兜底 |
| ③ 助手去头像 + 长回复折叠 | `config.showBotAvatar`（默认 true，宿主 false）；助手正文超 240px 默认折叠 + 渐隐 + 「展开全文」 |

## 改动文件

| 文件 | 变更 |
| --- | --- |
| `Plugins/AIAgent/web/src/agent-core/components/ChatWidget.vue` | 状态条搬出消息流进 `.aiw-status`；`TICK_TOLERANCE` 常量化、`jumpToMessage` 即时置亮 + 兜底；流式消息标记 `streaming: true` |
| `Plugins/AIAgent/web/src/agent-core/components/MessageItem.vue` | 头像按 `showBotAvatar` 开关；长回复折叠（只折助手 / 流式不折 / DOM 全文保留） |
| `Plugins/AIAgent/web/src/agent-core/styles/widget.css` | `.aiw-status` 定位与分隔线处理；`.aiw-msg__fold` 折叠样式（240px + mask）；`is-noavatar` 宽度 92% 与圆角 |
| `Plugins/AIAgent/web/src/agent-core/config.js` | 新增 `showBotAvatar: true`（含 FR-205 契约注释） |
| `Plugins/AIAgent/web/src/agentBridge.ts` | `showBotAvatar: false`（宿主裁剪） |
| `Plugins/AIAgent/web/src/agent-core/components/MessageItem.test.ts` | **新增** 6 条单测 |
| `Plugins/AIAgent/web/dist/*` | 重新构建产物（已含上述改动） |

未碰：后端（`Plugins/AIAgent/Controllers`、`Services`）、宿主 `ForgeSelf.Web/src/**`、`features.ts`、e2e 用例断言。

## 验证

- 门禁：插件 build ✅ / 宿主 check 0 error ✅ / design:check PASS ✅ / 全量单测 804 过（2 红为既有 AppearancePanel）✅ / 新增 MessageItem.test.ts 6/6 ✅
- 冒烟 e2e：`聊天主链路冒烟（v1.8.0 agent-core 组件）` → **1 passed (1.5m)**（真实宿主 + 真实后端、零 mock）
- 现场（:7399）：状态条底边 = footer 顶边 479 且 `closest('.aiw-body')=false`；点最后一条刻度 `is-on=3`；助手带头像数 5→0、默认折叠 4 条、展开 240→522px、用户消息 0 个折叠按钮
- 截图：`screenshots/dev/ai-agent/compact-01-collapsed.png`、`compact-02-expanded.png`、`compact-03-dark.png`

## 状态

✅ 需求②③（刻度条 / 去头像+折叠）与本批①（状态条）均已实现并验收。
⏸ 发布（维护闭环④）与运行实例只读复验（⑤）按用户「发布等我看完效果再说」暂缓。
⏸ git 提交未授权，未动。

## 下一步建议

1. 笑笑看完效果后授权发布 → 走 `release-local.ps1 -UpdateDir` 本地更新源 + 运行实例只读复验；
2. 若「展开全文」按钮觉得多余，可改为「点击气泡展开」；
3. 步骤卡 / 工具卡折叠需后端补 `blocks` 输出（已登记 TODO P1）。

## Validation / Review（2026-10-16 补记，指向既有证据）

- Validation：见 05-evidence（静态门禁 + 现场实测表 + §5 来源等级标注，均 Verified）；
- Review：见 06-review（§7 Final Decision = APPROVED）。
