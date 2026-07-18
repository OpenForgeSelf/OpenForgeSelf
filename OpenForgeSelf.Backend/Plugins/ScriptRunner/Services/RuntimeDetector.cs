using System.Diagnostics;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;

public class RuntimeDetector : IRuntimeDetector
{
    private readonly Dictionary<ScriptLanguage, RuntimeEnvironment> _cachedEnvironments = [];
    private readonly object _lock = new();

    public Task<List<RuntimeEnvironment>> DetectAllAsync()
    {
        var languages = Enum.GetValues<ScriptLanguage>();
        var results = new List<RuntimeEnvironment>();

        foreach (var lang in languages)
        {
            results.Add(DetectAsync(lang).GetAwaiter().GetResult());
        }

        return Task.FromResult(results);
    }

    public Task<RuntimeEnvironment> DetectAsync(ScriptLanguage language)
    {
        lock (_lock)
        {
            if (_cachedEnvironments.TryGetValue(language, out var cached))
            {
                return Task.FromResult(cached);
            }
        }

        var env = new RuntimeEnvironment
        {
            Language = language,
            IsAvailable = false,
            InterpreterPath = string.Empty,
            Version = string.Empty
        };

        try
        {
            var (interpreter, versionArg) = GetInterpreterAndVersionArg(language);

            if (string.IsNullOrEmpty(interpreter))
            {
                env.IsAvailable = false;
            }
            else
            {
                var path = FindExecutable(interpreter);
                if (!string.IsNullOrEmpty(path))
                {
                    env.IsAvailable = true;
                    env.InterpreterPath = path;
                    env.Version = GetVersion(path, versionArg);
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[RuntimeDetector] 检测 {0} 运行环境失败: {1}", language, ex.Message);
            env.IsAvailable = false;
        }

        lock (_lock)
        {
            _cachedEnvironments[language] = env;
        }

        return Task.FromResult(env);
    }

    public string GetInterpreterPath(ScriptLanguage language)
    {
        lock (_lock)
        {
            if (_cachedEnvironments.TryGetValue(language, out var cachedEnv))
            {
                return cachedEnv.InterpreterPath;
            }
        }

        var environment = DetectAsync(language).GetAwaiter().GetResult();
        return environment.InterpreterPath;
    }

    public string GetVersion(ScriptLanguage language)
    {
        lock (_lock)
        {
            if (_cachedEnvironments.TryGetValue(language, out var cachedEnv))
            {
                return cachedEnv.Version;
            }
        }

        var environment = DetectAsync(language).GetAwaiter().GetResult();
        return environment.Version;
    }

    private static (string interpreter, string versionArg) GetInterpreterAndVersionArg(ScriptLanguage language)
    {
        return language switch
        {
            ScriptLanguage.PowerShell => OperatingSystem.IsWindows() ? ("powershell.exe", "-NoProfile -Command $PSVersionTable.PSVersion.ToString()") : ("pwsh", "--version"),
            ScriptLanguage.Python => OperatingSystem.IsWindows() ? ("python.exe", "--version") : ("python3", "--version"),
            ScriptLanguage.NodeJs => ("node", "--version"),
            ScriptLanguage.Shell => OperatingSystem.IsWindows() ? ("bash.exe", "--version") : ("bash", "--version"),
            ScriptLanguage.Cmd => ("cmd.exe", "/c ver"),
            _ => (string.Empty, string.Empty)
        };
    }

    private static string? FindExecutable(string executable)
    {
        try
        {
            var paths = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var pathSeparators = OperatingSystem.IsWindows() ? ';' : ':';
            var pathExts = OperatingSystem.IsWindows()
                ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM").Split(';')
                : [""];

            foreach (var pathDir in paths.Split(pathSeparators))
            {
                if (string.IsNullOrWhiteSpace(pathDir)) continue;

                var fullPath = Path.Combine(pathDir.Trim(), executable);

                if (File.Exists(fullPath))
                {
                    return fullPath;
                }

                foreach (var ext in pathExts)
                {
                    var pathWithExt = fullPath + ext;
                    if (File.Exists(pathWithExt))
                    {
                        return pathWithExt;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug("[RuntimeDetector] 查找可执行文件 {0} 失败: {1}", executable, ex.Message);
        }

        return null;
    }

    private static string GetVersion(string executablePath, string versionArg)
    {
        try
        {
            var args = versionArg;
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return string.Empty;

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            var version = !string.IsNullOrEmpty(output) ? output.Trim() : error.Trim();
            if (version.Length > 100)
            {
                version = version[..100];
            }
            return version;
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug("[RuntimeDetector] 获取版本信息失败: {0}", ex.Message);
            return string.Empty;
        }
    }
}
