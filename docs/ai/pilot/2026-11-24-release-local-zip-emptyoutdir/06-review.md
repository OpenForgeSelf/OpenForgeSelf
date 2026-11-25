# 06 Review — 发布本地 zip 排障与插件 emptyOutDir 类级修复

## 九问审查
1. **满足 Intent？** PASS。方案 A（15 插件 emptyOutDir false + G2 守卫 + PC/Case 归档）全部落地，user 回复「a」批准。
2. **符合 Spec？** PASS。FR1/FR3/AC2/AC3/AC4 全 Verified；AC1（完整 zip 产出）因沙箱环境约束未达成，非仓库代码问题，如实记 Limitation + TODO。
3. **超出 Scope？** 否。仅 15 个 vite.config 的 emptyOutDir 行 + 守卫 + 归档文件；未改发布脚本/shim/宿主 config/csproj 版本。
4. **改了不该改的文件？** 否。顺手补的 `2026-11-23-hover-reveal-width-jump` case 索引行属台账守卫（Ledgers_ListEveryPc_And_EveryCase）强制要求，非无关改动。
5. **测试覆盖 AC？** PASS。AC2=grep+diff；AC3=G2 守卫 Passed；AC4=PC 签名+Registry 全绿。
6. **回归风险？** 低。`emptyOutDir:false` 是 Vite 标准选项，产物固定文件名覆盖写，与宿主既有实践一致；CI 无 shim 同样有效。
7. **架构不一致？** 无。修法使插件配置与宿主 SOP 对齐，消除横切分叉。
8. **Evidence 充分？** 是。红/绿日志、diff 统计、守卫 TRX、PC 校验全 Verified，等级未混用。
9. **问题类闭环（缺陷类）？** PASS。同类扫查命中表（15 插件 + 宿主正例）在 case §4；G2 守卫 active；PC-047 + case + 三处索引齐全；悬空教训清单为空（见 07 §12）。

## Risk
- L0（代码回归）：15 行同构配置值调整，无逻辑变更。
- L1（沙箱环境）：完整 zip 产出受 tool-call 删除配额约束，属环境层，待用户拍板。

## Findings
- Major：无。
- Minor：既有 PC-034 双文件导致一个枚举测试假红（存量，另批 TODO）。

## Final Decision
**APPROVED**（代码侧修复与归档完成；完整 zip 产出为环境约束，不阻塞本任务验收，转 TODO 待拍板）。
