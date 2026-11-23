# Review — 发布链 pnpm install 恒败修复（输入5 2026-11-23）

| # | 问题 | 结论 |
|---|------|------|
| 1 | 需求是否满足（发布链恢复可跑）？ | 是——主仓原位复跑 `pnpm install --frozen-lockfile` exit=0，postinstall 两个 hook 安装成功；用户重跑发布命令即可过 install 段 |
| 2 | 是否修「类」不修「例」？ | 是——平台短路前置规则升为 PC-043（不变量+lens 4 条），G2 顺序守卫入库，同类扫查 scripts/** 仅此一处 POSIX 探测 |
| 3 | 改动是否最小且未越界？ | 是——1 个脚本 2 处分支顺序/静默化 + 1 个守卫测试 + 归档文档；postinstall 响亮失败语义保留（未改成吞错）；build-frontend/release-local 未动 |
| 4 | 验证是否真实且分档正确？ | 是——中档全量后端（scripts/** 触发）+ 主仓行为级复跑 + hygiene + problem-classes 机检；红/绿均有日志实录 |
| 5 | Windows 语义是否正确（直通 vs 校验）？ | 正确——NTFS 无可执行位概念，git 在 Windows 执行 hook 不看模式位；dispatcher +x 是 Linux 侧关切，`return $true` 不放走任何真实门禁 |
| 6 | 会不会把真故障静默化？ | 不会——修的是误判（恒 false），不是吞错；Linux 侧 chmod 失败/文件缺失仍 throw；hook 文件存在性/非空校验保留 |
| 7 | 主仓未提交 patch 是否构成风险？ | 已披露——主仓 `scripts/install-git-hooks.ps1` 为工作区改动（解锁用户命令所需），与 worktree 同内容；用户提交时二选一合流即可 |
| 8 | 逃逸分析 | 非复发，潜伏缺陷首次暴露（4a3f67f 落地时验证环境均无此触发组合：CI=linux、纯 Windows 无 bash）；教训入 PC-043 lens（环境探测≠平台判定） |

## Final Decision

**APPROVED** —— 缺陷修复 + 类级归档 + 守卫齐备；门禁绿（以实际套件结果为准，见 05-evidence）。
