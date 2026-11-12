# 03 Plan（task-13）

| # | 改什么 | 文件 | 验证 |
|---|---|---|---|
| 1 | 水位预检（+参数化） | scripts/release/release-local.ps1 | -MinFreeGB 9999 立即 throw（零构建） |
| 2 | ESBUILD 护栏 | 同上 | 带 var 跑一轮，观察 removed 日志行 |
| 3 | worktree 锚定 | 同上 | 双场景逻辑单测（wt-* 子路径/主仓根）+ 真轮走 main 路径 |
| 4 | UpdateDir SHA256 复算 | 同上 | 沙箱 -UpdateDir 轮：三件套落位+PASS 行+独立复算一致 |
| 5 | 12 lockfile 对齐 | Plugins/*/web/pnpm-lock.yaml | git 面只 lockfile；沙箱轮 install 全过 |
