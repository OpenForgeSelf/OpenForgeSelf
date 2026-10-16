# AI-Native Pilot Result（最终汇报）

> 任务：2026-10-16-settings-network-proxy｜状态只报事实。

## 1. Repository Understanding

我确认了：模型请求出口 M1~M5 全部收口 IHttpClientFactory（typed AIService / AIProviderRegistry / AIModelService / AIProviderService / AIProviderController），M6 AgentChatController 自建 OpenAIClient 需接 transport，M7 AIAgent 插件 HttpClient 实测休眠；设置机制 = ForgeSetting XML + SettingsController /api/settings + SettingsView 面板分发；密码加密先例 ISecretEncryptionService。

## 2. Selected Task

设置页新增「网络设置」：HTTP/HTTPS 代理（开关/地址/端口/用户名/密码），后端全部模型请求走代理；关闭=显式直连忽略系统代理；保存即生效不重启。SOCKS5 / 连通性检测按闸门1拍板记 TODO。

## 3. Changed Files

后端 7 文件（ForgeSetting / SettingsController / ModelProxy 新增 / AppBuilder / AgentChatController / AIProviderRegistry / AIModelService）+ 3 新测试类；前端 4 修改（types / settingsApi / SettingsView / SettingsView.test）+ NetworkPanel.vue 新增 + 2 新测试文件。明细见 05-evidence.md §Changed Files。

## 4. Validation

Build: `dotnet build`（BuildCheck 目录）0 errors — PASS
Unit Test: 后端过滤集 26/26；全量第二轮 23F/2956P（本批域零红，红项=存量基线+并行会话残留+并发 flaky，隔离复跑全绿）；前端 `pnpm run test` **985/985** — PASS
E2E: `settings-network.spec.ts` **3/3**（真实前后端零 mock）+ dev-stack 源码实例现场验证「保存即生效」 — PASS

## 5. Evidence

见 `05-evidence.md`。全部结论 Verified；如实披露：真实代理服务器转发行为未端到端验证（本机无代理服务器，单测层面已覆盖 GetProxy/凭据/回环）。

## 6. Review

`06-review.md` Final Decision：**APPROVED**（Risk L1）。

## 7. Risk

L1 — 关闭态语义从「跟随系统代理」变为「显式直连」（Spec 批准语义，面板文案已明示）；配置新字段向后兼容。

## 8. Problems Found

1. **真实 UI 缺陷（e2e 暴露并修复）**：行内错误 `<p v-if>` 出现/消失改变布局，el-input-number blur 提交清错发生在 mousedown/mouseup 之间 → 保存按钮上移 20px → 首次点击被吞。修复=错误行常驻等高占位，单测+e2e 注释双锁回归。
2. ModelProxy 运行期兜底缺陷（测试先行抓出）：`new Uri("http://http://host")` 不抛异常而规整成非法地址，scheme 粘贴可绕过 try/catch → 改与 BR-5 同规则 Regex 校验。
3. .NET 10 两处断言基建差异：HttpClient._handler 移至基类（反射沿继承链）；ISecretEncryptionService 接口扩至 4 成员（存量桩补齐）。
4. 本机 config 句柄争抢家族再次命中（测试 Dispose 还原时）→ Restore 加退避重试。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

## 10. 最重要的问题

「测试失败」与「产品缺陷」的边界判定需要探针证据而非猜测：e2e 用例2 连红多轮，先后排除了绑定丢失、遮挡、进程干扰，最终以 mousedown/mouseup 目标差异 + MutationObserver 时间线证明是**布局跳动吞点击的真实用户缺陷**——若当时选择"改测试写法绕过"（如 dispatchEvent），缺陷将带着进入发布。规则已可提炼：*行内校验文案禁止以改变相邻控件几何位置的方式出现/消失*，应回写 agent-workflow Part B（本次已回写）。

## 11. 下一步建议

把「错误占位防跳动」推广为设置页/表单类面板的统一走查项：对既有面板（ApiServerPanel 等）grep `v-if.*error` 类模式，凡行内错误用 v-if 且下方有可点控件的，统一改占位形态（小改动，可并入下批）。
