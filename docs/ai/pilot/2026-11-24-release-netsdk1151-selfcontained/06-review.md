# 06 Review — 复核

## 技术核对（结论先行）
修复正确且最小：把被 SDK 推断误判的"空 SelfContained"变成显式 false，直接掐断 NETSDK1151 的触发条件；不改任何 publish 参数或校验开关。

## 逐项复核
| 检查项 | 结论 | 说明 |
|--------|------|------|
| 修复是否针对根因 | ✅ | 原属性因求值顺序未生效，无条件声明直接消除"空值→推断"链路 |
| 是否误伤 UpdateAgent 自身发布 | ✅ | publish-updateagent.ps1 体积断言防误打自包含，FDD 是其设计形态；完整链中该步 EXIT=0 |
| 是否绕过安全/校验机制 | ✅ 否 | 未关 ValidateExecutableReferences、未调 shim 阈值；-SkipFrontend 只用脚本已有参数跳过已完成的构建阶段 |
| 守卫是否防空转 | ✅ | Pc049 含阳性对照（OutputType=Exe）；grep 签名 + G2 双层 |
| 台账是否自洽 | ✅ | check-problem-classes 39/39、Registry 6/6 |
| 是否混入并行会话改动 | ✅ 否 | 未碰 AGENTS.md / 工程流程文档 / hooks / build-frontend.ps1 等并行在途文件 |

## 遗留
- 本工具环境下完整链（不跳前端）仍卡 safe-delete 配额（PC-047 §遗留，环境约束）；本次用 -SkipFrontend 分阶段出包，用户终端有弹窗确认可一次跑完。
- PC-034 存量假红（双文件 .SingleOrDefault）与本任务无关，已记 TODO。

## Final Decision
**APPROVED**：修复针对根因、最小改动、守卫与台账全绿、本地 zip 已产出并通过 SHA256 校验，AC1–AC4 全部 Verified，任务完成。
