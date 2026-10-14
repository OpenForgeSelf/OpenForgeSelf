# 05 Evidence — 2.4.4 出包阻塞修复

| 验证项 | 命令/动作 | 结果 | 等级 |
|---|---|---|---|
| 前端类型+lint | `cd ForgeSelf.Web && pnpm run check` | exit 0，0 errors（143 既有 warnings 为历史风格告警，Warning 级不阻断） | Verified |
| 发布链 | `pwsh scripts/release/release-local.ps1 -Version 2.4.4 -Sign` | 全链跑通（前端 build → publish → package → 签名 → zip → SHA256SUMS → 拷 updates/） | Verified |
| 产物 zip | `updates/OpenForgeSelf-2.4.4.2610140853-win-x64.zip`（161,965,598 B） | 磁盘 SHA256 与 SHA256SUMS.txt 记录值 MATCH | Verified |
| exe 签名 | `Get-AuthenticodeSignature`（根启动器 + versions/2.4.4.2610140853/ForgeSelf.exe） | Status=Valid，Sectigo RFC3161 时间戳 | Verified |
| 更新源配置 | `GET /api/update/config`（运行实例 :51888） | provider=local，localDir=`D:\src\my-proj\OpenForgeSelf\updates` | Verified |
| UI 代点 | 设置-版本更新：检查更新→下载→重启并更新→弹窗确认 | 页面显示"正在重启应用并安装更新"，宿主自更新完成 | Verified |
| 恢复确认 | `GET /api/update/status` | currentVersion 由 2.4.3.2610111757 → **2.4.4.2610140853**（首轮轮询即拿到） | Verified |
| 运行实例只读复验 | 页面自动刷新完整渲染（无 401 空态）；版本更新页显示 v2.4.4.2610140853、"已是最新版本" | 截图存档 `ForgeSelf.Web/screenshots/live-51888/version-update-after-2.4.4.2610140853.png` | Verified |

## 过程记录
- 首发发布链在前端 `vue-tsc -b` 处 exit 2：3 个 TS6133（`BG_SOLID_PRESETS`/`selectSolid` 仅存在于已注释的旧纯色选择器块；`onMounted` 全文未用）。最小删除后 check 转绿。
- 全程未停/启/杀任何宿主进程（铁律1）；宿主重启由 update-agent 自更新完成。
