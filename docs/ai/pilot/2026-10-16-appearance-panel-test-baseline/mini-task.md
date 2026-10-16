# mini-task：修正 AppearancePanel 过期测试断言

- 日期：2026-10-16
- 类型：轻量缺陷修复（只更新测试期望，不改 UI 行为）
- 关联：`TODO.md` AppearancePanel.test.ts 过期断言项

## Intent / Spec

全量 Vitest 当前有两项过期 UI 断言；修正后重跑又暴露 `portConfig.test.ts` 用例留下 1.5 秒真实定时器，测试环境销毁后触发 `window is not defined`。按当前界面设计更新 UI 测试，并让端口重启用例清理 fake timer；不改变产品行为。

## Scope

- 允许：`ForgeSelf.Web/src/__tests__/AppearancePanel.test.ts`、`ForgeSelf.Web/src/__tests__/portConfig.test.ts`、本 pilot 工件、`TODO.md`。
- 禁止：修改任意产品组件、样式、状态管理或设计行为；改动与本缺陷无关的测试。

## Acceptance Criteria

- [ ] AppearancePanel 测试只断言当前实际存在的可见项，不再要求「外观」标题或字体大小控件。
- [ ] BackgroundSettings 测试断言纯色 swatch 为 0，且图片预设、上传、URL、遮罩、恢复默认均符合当前实现。
- [ ] 定向测试通过；全量 Vitest 不再有这两条已知失败或定时器销毁后的未处理异常。
- [ ] Evidence 与 Review 记录真实命令和结果；移除 TODO 中该完成项。

## Plan / Task

1. 对照 `settings/AppearancePanel.vue`、`BackgroundSettings.vue` 与 `appearance.ts` 确认真实 UI 能力及预设数量。
2. 更新 `AppearancePanel.test.ts` 的过期期望，并在端口重启成功测试中清理 fake timer；先跑定向测试，再跑全量 Vitest。
3. 更新 TODO，补 `05-evidence.md`、`06-review.md`、`07-final-report.md`。

## Verification

- `pnpm run test -- run src/__tests__/AppearancePanel.test.ts`
- `pnpm run test`

## Gate 1

用户在 2026-10-16 明确要求「剩下的看看还有什么问题，解决掉并验收提交」；本轻量任务只修正测试以匹配已经落地的 UI，不改变产品行为。
