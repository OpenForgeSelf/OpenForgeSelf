using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Options;
using NewLife.Log;
using OpenForgeSelf.Backend.Models;

namespace OpenForgeSelf.Backend.Services;

/// <summary>
/// 辅助进程启动器，用于在服务模式下将托盘辅助进程启动到当前活跃用户会话中。
/// 解决 Windows Session 0 隔离问题：服务运行在 Session 0（非交互式），
/// 无法直接显示托盘图标，需通过 <c>CreateProcessAsUser</c> 在用户会话中启动辅助进程。
/// </summary>
/// <remarks>
/// 参考 research.md §5 Session 0 隔离问题：
/// Windows 服务运行在 Session 0，无法与用户桌面交互（包括托盘图标）。
/// 标准做法是启动一个用户会话中的辅助进程来承载托盘图标。
/// </remarks>
public static class TrayProcessStarter
{
    // ── Win32 P/Invoke ──

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreateProcessAsUser(
        IntPtr hToken,
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(
        uint sessionId,
        out IntPtr phToken);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr hExistingToken,
        uint dwDesiredAccess,
        IntPtr lpTokenAttributes,
        SECURITY_IMPERSONATION_LEVEL ImpersonationLevel,
        TOKEN_TYPE TokenType,
        out IntPtr phNewToken);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(
        out IntPtr lpEnvironment,
        IntPtr hToken,
        bool bInherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint TOKEN_QUERY = 0x0008;
    private const uint TOKEN_DUPLICATE = 0x0002;
    private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
    private const uint TOKEN_ADJUST_DEFAULT = 0x0080;
    private const uint TOKEN_ADJUST_SESSIONID = 0x0100;

    private const uint STANDARD_RIGHTS_REQUIRED = 0x000F0000;
    private const uint TOKEN_ALL_ACCESS = STANDARD_RIGHTS_REQUIRED | TOKEN_ASSIGN_PRIMARY |
        TOKEN_DUPLICATE | TOKEN_QUERY | TOKEN_ADJUST_DEFAULT | TOKEN_ADJUST_SESSIONID;

    private const uint CREATE_UNICODE_ENVIRONMENT = 0x0400;
    private const uint CREATE_NO_WINDOW = 0x08000000;

    private enum SECURITY_IMPERSONATION_LEVEL
    {
        SecurityAnonymous,
        SecurityIdentification,
        SecurityImpersonation,
        SecurityDelegation
    }

    private enum TOKEN_TYPE
    {
        TokenPrimary = 1,
        TokenImpersonation
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXCountChars;
        public uint dwYCountChars;
        public uint dwFillAttribute;
        public uint dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    /// <summary>
    /// 在活动用户会话中启动托盘辅助进程。
    /// 使用 <c>WTSGetActiveConsoleSessionId</c> 获取当前活跃会话 ID，
    /// 通过 <c>WTSQueryUserToken</c> 获取用户令牌，
    /// 最后调用 <c>CreateProcessAsUser</c> 在用户会话中启动进程。
    /// </summary>
    /// <param name="executablePath">可执行文件路径</param>
    /// <param name="arguments">命令行参数</param>
    /// <returns>启动成功返回 true，否则 false</returns>
    public static bool StartTrayProcess(string executablePath, string arguments)
    {
        // 如果当前不在 Windows 平台，跳过
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            XTrace.Log.Warn("TrayProcessStarter: 非 Windows 平台，跳过启动托盘辅助进程");
            return false;
        }

        // 获取当前活跃控制台会话 ID
        var sessionId = WTSGetActiveConsoleSessionId();
        if (sessionId == 0xFFFFFFFF)
        {
            XTrace.Log.Warn("TrayProcessStarter: 未找到活跃用户会话，跳过启动托盘辅助进程");
            return false;
        }

        XTrace.Log.Info("TrayProcessStarter: 活跃会话 ID = {0}", sessionId);

        // 查询该会话的用户令牌
        if (!WTSQueryUserToken(sessionId, out var userToken))
        {
            var error = Marshal.GetLastWin32Error();
            XTrace.Log.Error("TrayProcessStarter: WTSQueryUserToken 失败 (error={0})", error);
            return false;
        }

        try
        {
            // 复制令牌为主令牌
            if (!DuplicateTokenEx(
                    userToken,
                    TOKEN_ALL_ACCESS,
                    IntPtr.Zero,
                    SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                    TOKEN_TYPE.TokenPrimary,
                    out var primaryToken))
            {
                var error = Marshal.GetLastWin32Error();
                XTrace.Log.Error("TrayProcessStarter: DuplicateTokenEx 失败 (error={0})", error);
                return false;
            }

            try
            {
                // 创建用户环境块
                if (!CreateEnvironmentBlock(out var envBlock, primaryToken, false))
                {
                    var error = Marshal.GetLastWin32Error();
                    XTrace.Log.Warn("TrayProcessStarter: CreateEnvironmentBlock 失败 (error={0})，继续", error);
                    envBlock = IntPtr.Zero;
                }

                try
                {
                    // 构建命令行
                    var cmdLine = new StringBuilder($"\"{executablePath}\" {arguments}");

                    // 配置 STARTUPINFO：在用户会话桌面上启动
                    var startupInfo = new STARTUPINFO
                    {
                        cb = Marshal.SizeOf<STARTUPINFO>(),
                        lpDesktop = "winsta0\\default",
                    };

                    // 启动进程
                    if (!CreateProcessAsUser(
                            primaryToken,
                            null,
                            cmdLine,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            false,
                            CREATE_UNICODE_ENVIRONMENT | CREATE_NO_WINDOW,
                            envBlock,
                            null,
                            ref startupInfo,
                            out var processInfo))
                    {
                        var error = Marshal.GetLastWin32Error();
                        XTrace.Log.Error("TrayProcessStarter: CreateProcessAsUser 失败 (error={0})", error);
                        return false;
                    }

                    // 关闭进程和线程句柄
                    CloseHandle(processInfo.hProcess);
                    CloseHandle(processInfo.hThread);

                    XTrace.Log.Info("TrayProcessStarter: 托盘辅助进程已启动 (PID={0})", processInfo.dwProcessId);
                    return true;
                }
                finally
                {
                    if (envBlock != IntPtr.Zero)
                        DestroyEnvironmentBlock(envBlock);
                }
            }
            finally
            {
                CloseHandle(primaryToken);
            }
        }
        finally
        {
            CloseHandle(userToken);
        }
    }

    /// <summary>
    /// 创建启动参数：--tray --pipe-name <name> --port <port> [--service-pid <pid>]
    /// </summary>
    public static string BuildTrayArguments(int port, string pipeName)
    {
        return $"--tray --pipe-name \"{pipeName}\" --port {port}";
    }
}