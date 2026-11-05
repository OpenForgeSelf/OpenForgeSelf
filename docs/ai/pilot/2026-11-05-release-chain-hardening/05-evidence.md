# 05 Evidence：发行链加固（task-13）

| AC | 验收项 | 结果 | 来源 |
|---|---|---|---|
| AC1 | 水位中止测试：`-MinFreeGB 9999` 立即 throw「disk watermark: free 6.75GB on D: (< 9999GB)」、exit 1、零构建 | ✅ | 会话输出（Verified） |
| AC2 | ESBUILD 护栏实测：沙箱轮打印「removed ESBUILD_BINARY_PATH env guard conflict (was: C:\Users\12504\esbuild-bin\esbuild.exe)」，随后 12 插件 web install 全过 | ✅ | .temp/tmp/t13-release-verify.log（Verified） |
| AC3 | worktree 锚定逻辑双场景单测（wt-* 子路径→主仓上级 D:\src\my-proj\OpenForgeSelf；主仓根→原行为同值）+ 真轮 main 路径正常 | ✅ | 会话单测输出（Verified） |
| AC4 | 沙箱发布轮 exit 0、ALL DONE 125s、三件套落位（zip 177.9MB+NOTES+SUMS）、脚本 SHA256 复算 PASS + 独立 Get-FileHash 复算一致 | ✅ | .temp/tmp/update-verify-t13/ 与日志（Verified） |
| AC5 | 12/12 插件 web lockfile esbuild→0.25.12；git 面仅 12 个 lockfile（package.json 零改动）；沙箱轮 install 全过 | ✅ | git status/diff 抽样（Verified） |
| 附加 | 中档全量 #3：3711 例 25 红/15m17s——RepositoryScriptTests+RepositoryReleaseVersionTests **0 红**（脚本守卫过）；红全为已知基线+波动尾巴 | ✅ | .temp/tmp/tb-full-3.log（Verified） |
| 附加 | 签名段未验证（本轮未加 -Sign；签名逻辑本批未动，以 2.4.22.2611051134 回归锚轮为准） | ⚠️ Unknown（显式声明） | — |
