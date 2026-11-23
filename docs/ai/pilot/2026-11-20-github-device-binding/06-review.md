# 06 Review — GitHub 账号绑定（PILOT-083）

> 审查方式：自审（本批为恢复性重建，原批走查结论随 2026-11-23 提交丢失；本件为当日重建自审）。

## 八问

1. **需求满足了吗？** AC1–AC12 中 10 项 Verified 绿（52/52 后端过滤集 + 32/32 前端过滤集）；AC8 extraheader 真实克隆、AC12 e2e 整跑为 Inferred/Pending（环境依赖，见 05 §3）。
2. **实现与 Spec 一致吗？** 一致——端点 5 个、四态 UX、门控 `GetBearerToken` 单点、密文存储均按 02-spec §4/§5；偏差一处：AC11 收敛为绑定域自定义面组合（EP 横幅暗色欠账另批，已在 spec 注明）。
3. **测试覆盖够吗？** 设备流协议态（pending/slow_down/expired/denied/未知）、失败透传（合并/截断/非 JSON/空体）、预填四形态、门控三态、密文不落明文、投影无令牌、Bearer 附着/缺省均有锚定测试；UI 层四态 + 开关回滚 + 解绑确认有组件测试。
4. **范围超了吗？** 未超——全部改动在 04 Allowed 表内；主题 css 未动；并行流的 UpdateAgent/OutboundSocket 修复不在本批提交。
5. **风险残留在哪？** ① e2e 整跑依赖插件前端产物齐备（DbAdmin 依赖修复中）；② `ForgeSetting` 进程全局单例在测试间的串扰已用用例首解绑消解；③ extraheader 参数串仅走查级验证。
6. **有没有吞错误？** 没有——AC4/AC5 专项锁定错误透传与协议等待态；控制器 HTTP 200 业务信封 + 400 透传。
7. **安全面过一遍？** 令牌只落 `v2:` 密文（`ISecretEncryptionService`）；明文不进日志/响应/DTO（投影序列化守卫锁定）；URL 预填走 `Uri.EscapeDataString`；ClientId 为公开应用无 Secret。
8. **文档与真源回写了吗？** `docs/02-features/101-github-account.md` 新建（功能真源）、README 功能表 +1、not-taken-decisions +068、本目录八件齐。

## Final Decision

**APPROVED**（2026-11-23 重建自审；闸门2 待用户验收，验收后提交归档）。
遗留：e2e 整跑与 extraheader 真实克隆两项待环境修复后补验（不阻塞本批提交，风险已显式登记）。
