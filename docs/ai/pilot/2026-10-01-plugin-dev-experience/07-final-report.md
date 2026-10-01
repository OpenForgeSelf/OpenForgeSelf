# Final Report —— 插件开发/调试体验优化（P1+P2+P3 全套）

> Task ID：PILOT-plugin-dev-experience ｜ 日期：2026-10-01 ｜ 流程：AI-Native 九阶段闭环（闸门1 = 用户「实现并验证该方案」+ 范围两项拍板）
> 状态：**实现与验证完成，等待闸门2（用户验收）与闸门3（提交授权）**

## 1. Repository Understanding

见 `00-repository-understanding.md`。要点：宿主已有 collectible ALC / 版本化侧载 / 动态 MVC / 内容指纹四项底座，摩擦根因是「更新链路与版本号单调递增强耦合（`PluginVersionService.cs:167-173`）」「无 shadow copy 致 DLL 锁 + 禁停宿主铁律」「前端 dist 与运行目录无同步」。

## 2. Selected Task

P1+P2+P3 全套：DevMode 总闸 + shadow-copy 同版本热重载 + 前端 dev 直读源 dist + 异常可见 + dev 诊断 + 日志插件维度 + 生成式 HMR（opt-in）+ 6 处文档漂移修正。

## 3. Changed Files

新增 12（4 个 Dev 基础类 + DevController + 2 脚本 + 5 测试文件…）／修改 13（PluginManager、AppBuilder、PluginController、PluginInfoDto/DetailDto、激活器、注册表、中间件、csproj、前端 types/PluginStore、4 处文档 + launchSettings）。全表见 `05-evidence.md` Changed Files 节。

## 4. Validation（真实结果）

| 项 | 结果 |
| --- | --- |
| 构建 | 0 错误（与基线持平） |
| 新增测试 | 49/49，3 连跑稳定 |
| 全量测试（TMP 重定向干净轮） | 27 失败 / 1959 通过；真新增 2 条均已修复（测试竞态 + 脚本 BOM），当前无本任务引入失败；基线 118 条中 91 条环境性失败随 TMP 重定向消失 |
| 前端门禁 | check 0 错误（81 既有 warning）；vitest 568/568 |
| dev 宿主实跑 | 同版本 reload×3 全过；改码→重建→reload 后新日志行为出现且版本号未变；坏插件完整错误透出；日志 `[plugin:sample]` 前缀；web 资源 no-store；dev-off 宿主 dev 端点 401/404 分层正确 |

## 5. Evidence

`05-evidence.md`（17 项验证，Verified 15 / Unknown 2；基线与终轮日志存 `evidence/`）。

## 6. Review

`06-review.md`：八问全 PASS，Final Decision = **APPROVED（附条件）**，Risk 最高 L2。

## 7. Risk

最高 L2：2B HMR 浏览器级未验证（默认关、回退零成本）；shadow 陈旧目录可能短暂残留（双保险清扫）；并行会话 Temp 拒访会放大全量失败数（已留对照方法）。

## 8. Problems Found

- `ForgeConfigTests.ForgeSetting_配置文件归一` 基线即失败（环境性，既有）。
- 前端 PluginState 枚举注释与后端实态（11 值）偏差（既有）。
- **全量跑测必须停自起实例**（ConfigUnifier .tmp 争抢）——本次两轮实证，建议升格进 agent-workflow.md 门禁清单。

## 9. Process Evaluation

卡点：① Write 工具产无 BOM UTF-8 → PS 5.1 解析烂（user-memory 已录坑的再实证，修复=补 BOM）；② xUnit 跨类并行 × 进程级静态（DevMode）竞态——测试不得断言全局静态值；③ 系统 Temp 对测试主机拒访（本日新环境变化，TMP 重定向可绕）。

## 10. 最重要的问题

**dev-off 宿主行为零变化已实机证明**（401→鉴权先拦、404→dev-gate），这是本任务可安全合入的先决条件；其余一切均服务于它。

## 11. 下一步建议

1. 用户验收（闸门2）→ 授权后提交（闸门3，仅提交不推送）。
2. 3B per-plugin 分文件日志另立工单；2B HMR 使用时补浏览器走查。
3. TODO.md:67「诊断可观测性」大部分已由本任务覆盖，可销项/收敛。
