# 03 Plan — 执行步骤

1. **闸门1**：向用户给出 A/B/C 三方案 + 推荐 A，用户回复 "a" 授权。
2. **调查**：读 UpdateAgent.csproj / Api.csproj 引用 / publish-host.ps1 参数；最小复现（publish 失败 + build 成功对照）；读 SDK 11 targets 67-78 / 85-89 / 1340；WebFetch 官方 ValidateExecutableReferences.cs 确认判定逻辑。
3. **实现**：Edit UpdateAgent.csproj，SelfContained 去 Condition 无条件 false + 中文注释。
4. **验证**：最小 publish（EXIT=0、FDD 产物形态）→ 完整发布链（首次仍卡沙箱配额 → -SkipFrontend 分阶段完成）→ SHA256 校验。
5. **守卫**：新增 G2 `Pc049_…`，跑 trx 确认 Passed。
6. **归档**：PC-049 + case + 三处索引 + 本 pilot 00-07；check-problem-classes + Registry 全绿；pilot 工件门禁。
7. **收口**：日志 + TODO + 汇报。

## 关键决策
- 完整链在本工具环境卡 safe-delete 配额（PC-047 §遗留，环境约束），不调阈值不改 shim；用脚本已有 `-SkipFrontend`（前端 15/15 dist 齐全）分阶段出包——不绕过安全守卫、不污染仓库。
