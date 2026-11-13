# 06 Review — 门禁违例修复批（PILOT-080）

> 阶段：Stage 9 · Review｜审查对象：05 Evidence + 工件链 00–05 + 已落地代码面
> 分档：**中**｜Final Decision：**APPROVED**（2026-11-13 定稿，§5）

## 1. 审查八问

1. **需求覆盖**：handoff §3 A→F 六批是否全部有对应实现与证据？→ A/A1b/A2/B/C/D/E 全部有代码实测 + 门禁佐证（05 §1/§5）；F 为记录项（05 §3.3）。覆盖 ✅（AC-1..8 全闭环，判据 #1-11 全 Verified）。
2. **实现正确性**：C 批 byte 路径语义是否与 `StreamReader.ReadLineAsync` 对齐（CR 剥离、空行、GBK 尾字节不误切）？→ `ProcessOutputLineBuffer.cs` L7-10 设计依据 + PILOT-073 既有用例；本批未改解码器，仅接线。✅
3. **测试覆盖**：行为变更是否有测试？→ PC-027 守卫（JsonNonAsciiEscapingGuardTests，本批仅 E 批相邻精度修正于 schema 测试）+ PILOT-073 GBK 用例覆盖解码语义；A 批生效性由 st-tests 36/36 + be-build 0 错误间接验证。✅
4. **边界与 Forbidden**：是否触碰在途批文件？→ git status 复核：本批触面 = 03 §2 清单；在途文件零修改（含 AnthropicProtocolBridge 归因路径勘误）。✅
5. **分档合规**：中档是否跑全？→ 后端全量 12m33s 已跑（be-test-full.log）；设计棘轮已跑；插件 build/单测/pc-check 已跑；e2e 绿（run5 `1 passed (43.6s)`，05 §4 ②）；③ 起源码走查双绿（05 §4 ③）。✅ 全跑齐，无以快档报绿。
6. **回滚可行**：逐批独立回滚路径？→ 03 §6（B 批棘轮基线 2501→1761 的回滚联动已标注，须同步还原基线）。✅
7. **证据等级**：Unknown 是否如实？→ 05 §6 两条 Unknown 均显式标注 pending 槽位与阻塞关系，无虚构、无「应该可以」式措辞。✅
8. **流程合规**：工件链 / 闸门 / 交互设计节 / 铁律6？→ 00–07 齐（07 已定稿）；02 §5 交互设计节已写（插件任务必写，唯一可见改动面=B 批）；铁律6 双形态 lead 已批——07 磁盘版权威 + §13 群消息正文版由 lead 贴群。✅

## 2. 四项检查

| Check | 结果 | 依据 |
|-------|------|------|
| check1 门禁 | ✅ PASS（除 PC-033 并行批） | 05 §1 #1-9 |
| check2 问题类 | ✅ PASS（PC-011/020/027 guarded；pc.log 原始 3 违例 S1×1+S2×2 → 清零；pc-check3 归档后复跑 33/33 完成，唯一 FAIL=PC-033 在途批归属不修 → **问题类闭环门禁达成**：check 绿 + 07 三节齐 + 逃逸分析在） | gate-logs/pc-check2.log / pc-check3.log / pc.log |
| check3 设计棘轮 | ✅ PASS（1761 vs 基线 2501，无新增违例；compliance a=209 b=0 c=0 d=0） | gate-logs/fe-design2.log |
| check4 插件闭环 | ✅ PASS（① 门禁 ✅；② e2e run5 绿；③ 起源码走查双绿——亮/暗显式 html.dark + 五态全 ✅ + 对比度 7.2:1/5.1:1/12.4:1 ≥4.5:1 + GBK 运行态无乱码，样式面无 Finding（新发现范围外缺陷 BUG#1-5 见 §3）；④ 发布 ✅ 完成（release-local -UpdateDir -Sign ALL DONE 125s，gate-logs/release-local4.log）；⑤ 运行实例复验待用户更新 :51888 后执行（属插件六步⑤非阻塞项）；⑥ lead 域随收口） | 05 §4 ②③；gate-logs/st-walkthrough.log + 截图目录 |

## 3. 风险分级（L0-L4）

| 级 | 风险 | 说明 / 缓解 |
|----|------|-------------|
| ~~L2~~ | B 批暗色对比度未走查 | **已解除**（task-3 走查：标签 7.2:1 / 次要文字 5.1:1 / .view-title 12.4:1，均 ≥4.5:1；无白底白字无裸色残留，05 §4 ③） |
| L1 | filtered 唯一红为在途批点名 | 归因记录；**在途批完成后须复跑守卫确认清零**（移交 lead TODO 候选） |
| ~~L1~~ | st-e2e.log 未经 lead 确认 | **已解除**：run5 绿经日志正文自查 + lead 转发确认（05 §4 ②），AC-8 前半闭环；L2 暗色走查仍挂 |
| L0 | TimeoutKill 抖动 ×2 | 负载抖动非缺陷，观察记录 |
| BUG#1 P1 | 走查新发现（lead 补录 2026-11-13，不在本批 A-F 范围）：新建会话默认「起始目录 ~」前端原样发送 → 400「工作目录不存在: …\ForgeSelf.Api\bin\Debug\net10.0-windows\~」——开箱即坏 + 泄露后端进程路径 | 分流：TODO P1 独立批次（默认目录解析/前端占位展开 + 错误信息脱敏） |
| BUG#2 P1 | 走查新发现（系统性）：命令 stdout 永久缺失——echo/dir 真实执行 exit 0，输出区仅输入回显 + 截断的 cmd 包装 echo + 退出码；复现 2 次非时序。**尚未判责**：可能与 fix C 输出管线相关，也可能为存量缺陷；e2e smoke 断言不覆盖 stdout 内容故未拦截 | 分流：TODO P1 独立批次（**先判是否 fix C 回归**，判责后定修法与守卫） |
| BUG#3 P2 | 走查新发现：AskWrite 批准前乐观假渲染（$ cmd + 退出码 0，命令未执行） | 分流：TODO P2 |
| BUG#4/#5 minor | 走查新发现：权限档位菜单 Escape 不关；片段 tab 无空态提示 | 分流：TODO P3 |
| P2 合规 | 走查新发现（DESIGN.md 合规缺口）：插件根六 CSS 变量全空 / 空态缺 icon / 「已连接」徽标双份 / 插件图标 example.com 占位外链 ERR_BLOCKED_BY_ORB | 分流：TODO P2 合规扫查批次 |
| P3 Unknown | 走查未覆盖边界：AskWrite 取消路径 / SSH 真实连接 / 命令面板全功能 / Auto 档行为 | 分流：TODO P3 独立批次验收清单 |

> 走查新发现（2026-11-13 lead 补录），分流：BUG#1/#2 记 TODO P1 待独立批次（先判 BUG#2 是否 fix C 回归），其余 TODO P2/P3。均不在本批 A-F 修复范围，仅记录 + 分流，不改变 §5 Final Decision（本批 A→F 判据与门禁不受影响）；TODO 登记（id + expires）= lead 域。

## 4. Findings

1. E 批 +5 行 Regex 精度修正是「守卫误报」类修正——注释已互引 PC-027 同类教训；建议 07 §11 R1 沉淀「字面量匹配须区分读侧/写侧」为通用测试规约。
2. B 批棘轮基线 2501→1761 的**回滚联动**（回滚代码须同步还原基线值）需 lead 在基线管理侧留痕。
3. e2e Info 发现：右上角宿主壳图标暗底对比度偏低——非 SmartTerminal 改动面（宿主壳观察项），移交 lead 定夺是否入 TODO，不阻断本批。task-3 走查结论：样式清单面**无 Finding**（双态全绿）；另有走查新发现缺陷 BUG#1-5 + DESIGN.md 合规缺口（均在本批 A-F 范围外），已记 §3 分流 TODO，不属本槽。

## 5. Final Decision

状态：✅ **APPROVED**（2026-11-13 定稿）
- 依据：05 §1 判据 #1-11 全 Verified（#10 运行态 / #11 双态走查经 task-3 解除）；05 §4 ①②③ 全绿（e2e run5 `1 passed (43.6s)` + 起源码走查双态）；06 §2 check1-4 全 PASS；问题类闭环门禁达成（pc-check3 33/33，07 §10）；走查样式清单面零 Finding（新发现范围外缺陷 BUG#1-5 + 合规缺口已记 §3 并分流 TODO，不阻断）。
- 条件复核：走查双绿 ✅（亮/暗显式 html.dark，system 未充数）、对比度达标 ✅（7.2:1 / 5.1:1 / 12.4:1 ≥4.5:1）、e2e 主链路通过 ✅——原 CHANGES_REQUIRED 触发条件未触发。
- 残余事项（不阻断）：⑤ 运行实例只读复验待用户把 :51888 更新到新版本后执行（lead 域，token 有效性预检先行）；PC-033 并行批归属（不修，在途批完成后守卫复跑候选）；e2e Info 宿主壳图标对比度（06 §4 #3，lead 定夺）；走查新发现 BUG#1/#2（TODO P1 独立批次，先判 BUG#2 是否 fix C 回归）+ BUG#3-5、DESIGN.md 合规缺口、走查 Unknown 边界（TODO P2/P3）——分流记录见 06 §3，TODO 登记（id + expires）= lead 域。
