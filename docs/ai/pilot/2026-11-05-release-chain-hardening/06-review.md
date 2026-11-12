# 06 Review（task-13）

1. 水位阈值 2.5GB 是否合理？lead 拍板值；发布链实测需 ~3GB 峰值，2.5 预检留余量；可 -MinFreeGB 调。
2. ESBUILD 护栏会不会误伤？只在变量存在时摘除（print+remove），无副作用；真正校验方是 postinstall，摘除即根治 install 阶段。
3. worktree 锚定鲁棒性？仅匹配 `<主仓>\.temp\wt-*` 精确形态，误判面小；拿不准时文档指路 -UpdateDir 显式。
4. lockfile-only 是否破坏锁文件语义？pnpm update esbuild --depth=Infinity 在 vite 声明范围内重解析，package.json 零改动，SmartTerminal/DbAdmin 先例已在 0.25.12 运行。
5. Final Decision: **APPROVED**（自审；沙箱轮+中止测试双实证）。
