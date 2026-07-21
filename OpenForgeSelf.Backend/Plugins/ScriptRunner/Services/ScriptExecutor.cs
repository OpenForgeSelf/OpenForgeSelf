using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ScriptEntity = OpenForgeSelf.Backend.Plugins.ScriptRunner.Entities.Script;
using ScriptExecutionEntity = OpenForgeSelf.Backend.Plugins.ScriptRunner.Entities.ScriptExecution;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Hubs;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;

public class ScriptExecutor : IScriptExecutor
{
    private readonly IRuntimeDetector _runtimeDetector;
    private readonly Dictionary<long, ProcessExecutionContext> _runningExecutions = [];
    private readonly object _lock = new();

    public ScriptExecutor(IRuntimeDetector runtimeDetector)
    {
        _runtimeDetector = runtimeDetector;
    }

    public Task<ScriptExecution> ExecuteAsync(long scriptId, Dictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
    {
        var scriptEntity = ScriptEntity.FindById(scriptId);
        if (scriptEntity == null)
        {
            throw new FileNotFoundException($"脚本不存在，ID: {scriptId}");
        }

        var script = MapToScript(scriptEntity);
        var parametersJson = parameters != null ? JsonSerializer.Serialize(parameters) : string.Empty;

        var executionEntity = new ScriptExecutionEntity
        {
            ScriptId = scriptId,
            ScriptName = script.Name,
            Status = (int)ScriptExecutionStatus.Pending,
            StartTime = DateTime.Now,
            ParametersJson = parametersJson,
            OutputLogsJson = "[]"
        };

        executionEntity.Insert();

        var executionId = executionEntity.Id;

        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteScriptInternalAsync(executionId, script, parameters, cancellationToken);
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("[ScriptExecutor] 脚本执行异常: {0}", ex.Message);
                await UpdateExecutionStatusAsync(executionId, ScriptExecutionStatus.Failed, null, ex.Message);
            }
        }, cancellationToken);

        // 返回刚插入的实体快照，此刻状态确定为 Pending；
        // 不再回查数据库，避免与后续后台任务的 Running 更新产生竞态（调用方对返回状态断言 Pending 时更稳健）
        return Task.FromResult(MapToExecution(executionEntity));
    }

    public Task<ScriptExecution> ExecuteCodeAsync(string code, ScriptLanguage language, Dictionary<string, object?>? parameters = null, string? workingDirectory = null, CancellationToken cancellationToken = default)
    {
        var executionEntity = new ScriptExecutionEntity
        {
            ScriptId = 0,
            ScriptName = $"临时{language}脚本",
            Status = (int)ScriptExecutionStatus.Pending,
            StartTime = DateTime.Now,
            ParametersJson = parameters != null ? JsonSerializer.Serialize(parameters) : string.Empty,
            OutputLogsJson = "[]"
        };

        executionEntity.Insert();

        var executionId = executionEntity.Id;

        var tempScript = new Script
        {
            Id = 0,
            Name = $"临时{language}脚本",
            Language = language,
            Code = code,
            TimeoutSeconds = 300
        };

        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteScriptInternalAsync(executionId, tempScript, parameters, cancellationToken, workingDirectory);
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("[ScriptExecutor] 代码执行异常: {0}", ex.Message);
                await UpdateExecutionStatusAsync(executionId, ScriptExecutionStatus.Failed, null, ex.Message);
            }
        }, cancellationToken);

        // 返回刚插入的实体快照，此刻状态确定为 Pending；
        // 不再回查数据库，避免与后续后台任务的 Running 更新产生竞态（调用方对返回状态断言 Pending 时更稳健）
        return Task.FromResult(MapToExecution(executionEntity));
    }

    public async Task<bool> CancelAsync(long executionId)
    {
        ProcessExecutionContext? context;
        lock (_lock)
        {
            if (_runningExecutions.TryGetValue(executionId, out context))
            {
                _runningExecutions.Remove(executionId);
            }
        }

        if (context == null)
        {
            return false;
        }

        try
        {
            context.CancellationTokenSource.Cancel();
            if (context.Process != null && !context.Process.HasExited)
            {
                context.Process.Kill(entireProcessTree: true);
            }

            await UpdateExecutionStatusAsync(executionId, ScriptExecutionStatus.Cancelled);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[ScriptExecutor] 取消执行 {0} 失败: {1}", executionId, ex.Message);
            return false;
        }
    }

    public Task<ScriptExecution?> GetExecutionAsync(long executionId)
    {
        return Task.FromResult(GetExecution(executionId));
    }

    public async Task<List<ScriptExecutionLog>> GetOutputAsync(long executionId, DateTime? sinceTimestamp = null)
    {
        var execution = await GetExecutionAsync(executionId);
        if (execution == null)
        {
            return [];
        }

        if (sinceTimestamp == null)
        {
            return execution.OutputLogs;
        }

        return execution.OutputLogs.Where(l => l.Timestamp > sinceTimestamp.Value).ToList();
    }

    private async Task ExecuteScriptInternalAsync(long executionId, Script script, Dictionary<string, object?>? parameters, CancellationToken externalCancellationToken, string? workingDirectory = null)
    {
        var runtime = await _runtimeDetector.DetectAsync(script.Language);
        if (!runtime.IsAvailable)
        {
            await UpdateExecutionStatusAsync(executionId, ScriptExecutionStatus.Failed, null, $"{script.Language} 运行环境不可用");
            return;
        }

        await UpdateExecutionStatusAsync(executionId, ScriptExecutionStatus.Running);

        var stopwatch = Stopwatch.StartNew();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
        cts.CancelAfter(script.TimeoutSeconds * 1000);

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        var outputLogs = new List<ScriptExecutionLog>();

        Process? process = null;

        try
        {
            var tempFile = Path.GetTempFileName();
            var fileExt = GetFileExtension(script.Language);
            var scriptFile = Path.ChangeExtension(tempFile, fileExt);
            File.Move(tempFile, scriptFile);
            File.WriteAllText(scriptFile, script.Code);

            var (fileName, arguments) = GetProcessStartInfo(script.Language, scriptFile, parameters);

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(scriptFile) ?? string.Empty
            };

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    var key = $"SCRIPT_PARAM_{param.Key.ToUpperInvariant()}";
                    var value = param.Value?.ToString() ?? string.Empty;
                    startInfo.EnvironmentVariables[key] = value;
                }
            }

            process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            var executionContext = new ProcessExecutionContext
            {
                Process = process,
                CancellationTokenSource = cts
            };

            lock (_lock)
            {
                _runningExecutions[executionId] = executionContext;
            }

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    var log = new ScriptExecutionLog
                    {
                        Timestamp = DateTime.Now,
                        StreamType = "stdout",
                        Message = e.Data
                    };
                    lock (outputLogs)
                    {
                        outputLogs.Add(log);
                    }
                    _ = ScriptExecutionHub.BroadcastOutputLogAsync(executionId, log);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                    var log = new ScriptExecutionLog
                    {
                        Timestamp = DateTime.Now,
                        StreamType = "stderr",
                        Message = e.Data
                    };
                    lock (outputLogs)
                    {
                        outputLogs.Add(log);
                    }
                    _ = ScriptExecutionHub.BroadcastOutputLogAsync(executionId, log);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await ScriptExecutionHub.BroadcastStatusUpdateAsync(executionId, ScriptExecutionStatus.Running);

            var tcs = new TaskCompletionSource<bool>();
            process.Exited += (sender, e) => tcs.TrySetResult(true);

            using (cts.Token.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch { }
            }))
            {
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));
                if (completed == tcs.Task)
                {
                    process.WaitForExit();
                }
            }

            stopwatch.Stop();

            ScriptExecutionStatus status;
            if (cts.IsCancellationRequested)
            {
                status = script.TimeoutSeconds > 0 && !externalCancellationToken.IsCancellationRequested
                    ? ScriptExecutionStatus.Timeout
                    : ScriptExecutionStatus.Cancelled;
            }
            else if (process.ExitCode == 0)
            {
                status = ScriptExecutionStatus.Completed;
            }
            else
            {
                status = ScriptExecutionStatus.Failed;
            }

            var outputLogsJson = JsonSerializer.Serialize(outputLogs);
            await UpdateExecutionAsync(executionId, status, process.ExitCode,
                outputBuilder.ToString(), errorBuilder.ToString(),
                stopwatch.ElapsedMilliseconds, outputLogsJson);

            try
            {
                if (File.Exists(scriptFile))
                {
                    File.Delete(scriptFile);
                }
            }
            catch { }

            await ScriptExecutionHub.BroadcastStatusUpdateAsync(executionId, status);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            var status = cts.IsCancellationRequested && !externalCancellationToken.IsCancellationRequested
                ? ScriptExecutionStatus.Timeout
                : ScriptExecutionStatus.Cancelled;

            var outputLogsJson = JsonSerializer.Serialize(outputLogs);
            await UpdateExecutionAsync(executionId, status, null,
                outputBuilder.ToString(), errorBuilder.ToString(),
                stopwatch.ElapsedMilliseconds, outputLogsJson);

            await ScriptExecutionHub.BroadcastStatusUpdateAsync(executionId, status);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            errorBuilder.AppendLine($"执行异常: {ex.Message}");

            var outputLogsJson = JsonSerializer.Serialize(outputLogs);
            await UpdateExecutionAsync(executionId, ScriptExecutionStatus.Failed, null,
                outputBuilder.ToString(), errorBuilder.ToString(),
                stopwatch.ElapsedMilliseconds, outputLogsJson);

            await ScriptExecutionHub.BroadcastStatusUpdateAsync(executionId, ScriptExecutionStatus.Failed);
        }
        finally
        {
            lock (_lock)
            {
                _runningExecutions.Remove(executionId);
            }

            process?.Dispose();
            cts.Dispose();
        }
    }

    private Task UpdateExecutionStatusAsync(long executionId, ScriptExecutionStatus status, int? exitCode = null, string? errorMessage = null)
    {
        var entity = ScriptExecutionEntity.FindById(executionId);
        if (entity == null) return Task.CompletedTask;

        entity.Status = (int)status;
        if (status == ScriptExecutionStatus.Completed ||
            status == ScriptExecutionStatus.Failed ||
            status == ScriptExecutionStatus.Cancelled ||
            status == ScriptExecutionStatus.Timeout)
        {
            entity.EndTime = DateTime.Now;
            if (entity.DurationMs == 0)
            {
                entity.DurationMs = (long)(DateTime.Now - entity.StartTime).TotalMilliseconds;
            }
        }

        if (exitCode.HasValue)
        {
            entity.ExitCode = exitCode.Value;
        }

        if (!string.IsNullOrEmpty(errorMessage))
        {
            entity.ErrorOutput += errorMessage + Environment.NewLine;
        }

        entity.Update();
        return Task.CompletedTask;
    }

    private Task UpdateExecutionAsync(long executionId, ScriptExecutionStatus status, int? exitCode,
        string output, string errorOutput, long durationMs, string outputLogsJson)
    {
        var entity = ScriptExecutionEntity.FindById(executionId);
        if (entity == null) return Task.CompletedTask;

        entity.Status = (int)status;
        entity.EndTime = DateTime.Now;
        entity.ExitCode = exitCode ?? 0;
        entity.Output = output;
        entity.ErrorOutput = errorOutput;
        entity.DurationMs = durationMs;
        entity.OutputLogsJson = outputLogsJson;

        entity.Update();
        return Task.CompletedTask;
    }

    private static ScriptExecution? GetExecution(long executionId)
    {
        var entity = ScriptExecutionEntity.FindById(executionId);
        return entity == null ? null : MapToExecution(entity);
    }

    private static string GetFileExtension(ScriptLanguage language)
    {
        return language switch
        {
            ScriptLanguage.PowerShell => ".ps1",
            ScriptLanguage.Python => ".py",
            ScriptLanguage.NodeJs => ".js",
            ScriptLanguage.Shell => ".sh",
            ScriptLanguage.Cmd => ".bat",
            _ => ".txt"
        };
    }

    private static (string fileName, string arguments) GetProcessStartInfo(ScriptLanguage language, string scriptFile, Dictionary<string, object?>? parameters)
    {
        var paramArgs = parameters != null
            ? string.Join(" ", parameters.Select(p => EscapeArgument(p.Value?.ToString() ?? string.Empty)))
            : string.Empty;

        return language switch
        {
            ScriptLanguage.PowerShell => ("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptFile}\" {paramArgs}"),
            ScriptLanguage.Python => ("python", $"\"{scriptFile}\" {paramArgs}"),
            ScriptLanguage.NodeJs => ("node", $"\"{scriptFile}\" {paramArgs}"),
            ScriptLanguage.Shell => ("bash", $"\"{scriptFile}\" {paramArgs}"),
            ScriptLanguage.Cmd => ("cmd.exe", $"/c \"\"{scriptFile}\" {paramArgs}\""),
            _ => (scriptFile, paramArgs)
        };
    }

    private static string EscapeArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        if (arg.Contains(' ') || arg.Contains('"') || arg.Contains('\''))
        {
            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        }
        return arg;
    }

    private static Script MapToScript(ScriptEntity entity)
    {
        return new Script
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Language = (ScriptLanguage)entity.Language,
            Code = entity.Code,
            Category = entity.Category,
            Tags = JsonSerializer.Deserialize<List<string>>(entity.TagsJson) ?? [],
            IsFavorite = entity.IsFavorite,
            Parameters = JsonSerializer.Deserialize<List<ScriptParameter>>(entity.ParametersJson) ?? [],
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            UsageCount = entity.UsageCount,
            LastUsedAt = entity.LastUsedAt == DateTime.MinValue ? null : entity.LastUsedAt,
            TimeoutSeconds = entity.TimeoutSeconds
        };
    }

    private static ScriptExecution MapToExecution(ScriptExecutionEntity entity)
    {
        return new ScriptExecution
        {
            Id = entity.Id,
            ScriptId = entity.ScriptId,
            ScriptName = entity.ScriptName,
            Status = (ScriptExecutionStatus)entity.Status,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime == DateTime.MinValue ? null : entity.EndTime,
            ExitCode = entity.ExitCode,
            Output = entity.Output,
            ErrorOutput = entity.ErrorOutput,
            DurationMs = entity.DurationMs,
            ParametersJson = entity.ParametersJson,
            OutputLogs = JsonSerializer.Deserialize<List<ScriptExecutionLog>>(entity.OutputLogsJson) ?? []
        };
    }

    private class ProcessExecutionContext
    {
        public Process? Process { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; } = null!;
    }
}
