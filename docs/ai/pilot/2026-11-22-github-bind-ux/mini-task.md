# Mini Task — GitHub 绑定 UX 一轮（四态面板）

> Task ID：PILOT-2026-11-22-github-bind-ux｜轻量形态｜主任务：`2026-11-20-github-device-binding`（feature: 101）。

## Intent（为什么）

设备流后端就绪后，绑定过程若没有引导 UX，用户要手抄用户码、手动开页、失败无从重试——绑定转化与容错都差。一轮交付「设置页四态绑定面板 + 复制工具 + 对话框」。

## Spec（做什么·到什么程度）

- `GithubBindPanel.vue` 四态：idle（绑定按钮）→ preparing（整块 loading「正在准备设备码，请稍候…」，结构性防连点）→ code（自动复制横幅「设备码已复制到剪贴板」+ 大号设备码 + 复制按钮 + 3s 倒计时自动打开预填授权页 + 手动打开按钮 + 复制失败引导横幅）→ error（透传上游原因 + 重试）；bound 交由设置面板承接。
- 轮询语义：pending 静默续轮询、slow_down 自退避 +5s、expired/denied/failed 终态进 error、bound 发 `bound` 事件。
- `clipboard.ts`：secure-context 优先 navigator.clipboard，execCommand 兜底，失败返回 false。
- `GithubBindDialog.vue`：480px ElDialog 包面板，destroy-on-close，透传 bound。
- `GithubAccountPanel.vue`：未绑定（说明 + 绑定入口）/ 已绑定（账号、范围、解绑二次确认、自动附凭据开关即改即存失败回滚）。

## Interaction Design

- **点什么出现什么**：见上述四态流转；出码即复制 + 3s 自动开页；错误一律带重试。
- **空态**：未绑定态 = 说明文案 + 主按钮。
- **加载**：status 拉取 v-loading；准备期整块 loading 替换按钮（连点结构性不可能）。
- **出错**：start 失败/轮询终态全部透传上游 error+error_description 合并文案。
- **边界**：解绑走 ElMessageBox.confirm；开关失败回滚原值并报错；复制失败显示手动复制引导。
- **验收标准**：主任务 02-spec AC9/AC10/AC11（面板部分）。

## Plan / Allowed

| 文件 | 动作 |
| --- | --- |
| `src/types/githubAccount.ts`、`src/services/githubAccountApi.ts` | 新（authFetch 五端点封装，信封不抛） |
| `src/utils/clipboard.ts`(+test) | 新（5 测） |
| `src/components/github/GithubBindPanel.vue`(+test 14) | 新 |
| `src/components/github/GithubBindDialog.vue` | 新 |
| `src/components/settings/GithubAccountPanel.vue`(+test 7) | 新 |
| `src/views/SettingsView.vue` | 集成分类/导航/挂载/pageHeadMap |

**Forbidden**：不改后端；不改 PluginStore（二轮批）；不引入裸色值（`--el-*` 红线）。

## 验证

`pnpm run test -- --run src/components/github src/components/settings/GithubAccountPanel.test.ts src/utils/__tests__/clipboard.test.ts` → 26/26 绿。
