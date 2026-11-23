using ForgeSelf.UpdateAgent;

// 原生自更新代理入口（2026-11-21 输入15）。
// 由宿主 StagedUpdateService.TryStartExeAgent 拉起（先拷 Updates\agent-run\ 再运行，
// DOTNET_ROOT 指向 staged 解压根的公共运行时副本）。参数契约：
//   --host-pid <pid>          业务层宿主进程 pid（等其退出后换文件）
//   --install-dir <dir>       安装根（公共层；业务层落 versions/<ver>/）
//   --staged-dir <dir>        staged 解压根（QQNT 布局：公共层 + versions/<ver>/）
//   --exe-name <name>         根启动器文件名（重启目标）
//   --arg-list <a;b;c>        宿主原命令行参数（分号拼接；重启时原样回传）
// 退出码：0 = 成功；1 = 失败（详情见 %LOCALAPPDATA%\ForgeSelf\Logs\update-agent*.log）。

int exitCode;
try
{
    var options = UpdateAgentOptions.Parse(args);
    exitCode = await UpdateAgentRunner.RunAsync(options);
}
catch (Exception ex)
{
    AgentLog.Write($"致命异常: {ex}");
    exitCode = 1;
}
return exitCode;
