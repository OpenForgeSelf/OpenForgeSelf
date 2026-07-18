using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using OpenForgeSelf.Backend.Plugins.FileTools.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.FileTools.Services;

public class RenameService : IRenameService
{
    private static readonly ConcurrentDictionary<string, List<RenameResultItem>> _operationHistory = new();

    public Task<List<RenamePreviewItem>> PreviewRenameAsync(List<string> files, List<RenameRule> rules)
    {
        XTrace.Log.Debug("[FileTools] 预览重命名，文件数: {0}, 规则数: {1}", files.Count, rules.Count);

        var sortedRules = rules.Where(r => r.Enabled).OrderBy(r => r.Order).ToList();
        var previewItems = new List<RenamePreviewItem>();
        var newPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var sequenceCounters = new Dictionary<int, int>();
        for (int i = 0; i < sortedRules.Count; i++)
        {
            if (sortedRules[i].RuleType == RenameRuleType.Sequence)
            {
                var startIndex = GetIntParameter(sortedRules[i].Parameters, "startIndex", 1);
                sequenceCounters[i] = startIndex;
            }
        }

        foreach (var filePath in files)
        {
            if (!File.Exists(filePath) && !Directory.Exists(filePath))
            {
                continue;
            }

            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            var originalName = Path.GetFileName(filePath);
            var newName = originalName;

            for (int i = 0; i < sortedRules.Count; i++)
            {
                var rule = sortedRules[i];
                if (rule.RuleType == RenameRuleType.Sequence && sequenceCounters.TryGetValue(i, out var counter))
                {
                    newName = ApplySequenceRule(newName, rule, counter);
                    sequenceCounters[i] = counter + 1;
                }
                else
                {
                    newName = ApplyRule(newName, rule);
                }
            }

            var newPath = Path.Combine(directory, newName);
            var willConflict = !string.Equals(newPath, filePath, StringComparison.OrdinalIgnoreCase) &&
                              (File.Exists(newPath) || Directory.Exists(newPath) || !newPaths.Add(newPath));

            previewItems.Add(new RenamePreviewItem
            {
                OriginalPath = filePath,
                NewPath = newPath,
                OriginalName = originalName,
                NewName = newName,
                WillConflict = willConflict,
                ConflictWith = willConflict ? (File.Exists(newPath) || Directory.Exists(newPath) ? newPath : "列表中其他文件") : null
            });
        }

        return Task.FromResult(previewItems);
    }

    public async Task<RenameExecuteResult> ExecuteRenameAsync(List<string> files, List<RenameRule> rules)
    {
        XTrace.Log.Info("[FileTools] 执行重命名，文件数: {0}, 规则数: {1}", files.Count, rules.Count);

        var result = new RenameExecuteResult
        {
            TotalCount = files.Count,
            Results = new List<RenameResultItem>()
        };

        try
        {
            var preview = await PreviewRenameAsync(files, rules);
            var operationId = Guid.NewGuid().ToString();
            var operationResults = new List<RenameResultItem>();

            foreach (var item in preview)
            {
                var resultItem = new RenameResultItem
                {
                    OriginalPath = item.OriginalPath,
                    NewPath = item.NewPath
                };

                try
                {
                    if (string.Equals(item.OriginalPath, item.NewPath, StringComparison.OrdinalIgnoreCase))
                    {
                        resultItem.Success = true;
                        result.SuccessCount++;
                    }
                    else if (item.WillConflict)
                    {
                        resultItem.Success = false;
                        resultItem.ErrorMessage = $"目标路径已存在: {item.NewPath}";
                        result.FailedCount++;
                    }
                    else
                    {
                        if (File.Exists(item.OriginalPath))
                        {
                            File.Move(item.OriginalPath, item.NewPath);
                        }
                        else if (Directory.Exists(item.OriginalPath))
                        {
                            Directory.Move(item.OriginalPath, item.NewPath);
                        }

                        resultItem.Success = true;
                        result.SuccessCount++;
                        operationResults.Add(resultItem);
                    }
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("[FileTools] 重命名失败 {0}: {1}", item.OriginalPath, ex.Message);
                    resultItem.Success = false;
                    resultItem.ErrorMessage = ex.Message;
                    result.FailedCount++;
                }

                result.Results.Add(resultItem);
            }

            if (operationResults.Count > 0)
            {
                _operationHistory[operationId] = operationResults;
                result.OperationId = operationId;
            }

            result.Success = result.FailedCount == 0;
            result.Message = result.Success
                ? $"成功重命名 {result.SuccessCount} 个文件/文件夹"
                : $"重命名完成，成功 {result.SuccessCount} 个，失败 {result.FailedCount} 个";
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] 执行重命名异常: {0}", ex.Message);
            result.Success = false;
            result.Message = $"重命名执行失败: {ex.Message}";
        }

        return result;
    }

    public Task<RenameExecuteResult> UndoRenameAsync(string operationId)
    {
        XTrace.Log.Info("[FileTools] 撤销重命名操作: {0}", operationId);

        var result = new RenameExecuteResult
        {
            Results = new List<RenameResultItem>()
        };

        try
        {
            if (!_operationHistory.TryRemove(operationId, out var operationResults))
            {
                result.Success = false;
                result.Message = "操作记录不存在或已过期";
                return Task.FromResult(result);
            }

            result.TotalCount = operationResults.Count;

            for (int i = operationResults.Count - 1; i >= 0; i--)
            {
                var item = operationResults[i];
                var undoItem = new RenameResultItem
                {
                    OriginalPath = item.NewPath,
                    NewPath = item.OriginalPath
                };

                try
                {
                    if (File.Exists(item.NewPath))
                    {
                        File.Move(item.NewPath, item.OriginalPath);
                        undoItem.Success = true;
                        result.SuccessCount++;
                    }
                    else if (Directory.Exists(item.NewPath))
                    {
                        Directory.Move(item.NewPath, item.OriginalPath);
                        undoItem.Success = true;
                        result.SuccessCount++;
                    }
                    else
                    {
                        undoItem.Success = false;
                        undoItem.ErrorMessage = "源文件不存在";
                        result.FailedCount++;
                    }
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("[FileTools] 撤销重命名失败 {0}: {1}", item.NewPath, ex.Message);
                    undoItem.Success = false;
                    undoItem.ErrorMessage = ex.Message;
                    result.FailedCount++;
                }

                result.Results.Add(undoItem);
            }

            result.Success = result.FailedCount == 0;
            result.Message = result.Success
                ? $"成功撤销 {result.SuccessCount} 个文件/文件夹"
                : $"撤销完成，成功 {result.SuccessCount} 个，失败 {result.FailedCount} 个";
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] 撤销重命名异常: {0}", ex.Message);
            result.Success = false;
            result.Message = $"撤销失败: {ex.Message}";
        }

        return Task.FromResult(result);
    }

    private static string ApplyRule(string fileName, RenameRule rule)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        switch (rule.RuleType)
        {
            case RenameRuleType.Prefix:
                var prefix = GetStringParameter(rule.Parameters, "prefix");
                return prefix + fileName;

            case RenameRuleType.Suffix:
                var suffix = GetStringParameter(rule.Parameters, "suffix");
                return nameWithoutExt + suffix + extension;

            case RenameRuleType.ExtensionChange:
                var newExt = GetStringParameter(rule.Parameters, "newExtension");
                if (!newExt.StartsWith("."))
                    newExt = "." + newExt;
                return nameWithoutExt + newExt;

            case RenameRuleType.FindReplace:
                var find = GetStringParameter(rule.Parameters, "find");
                var replace = GetStringParameter(rule.Parameters, "replace");
                var ignoreCase = GetBoolParameter(rule.Parameters, "ignoreCase");
                var useRegex = GetBoolParameter(rule.Parameters, "useRegex");

                if (useRegex)
                {
                    var regexOptions = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
                    return Regex.Replace(fileName, find, replace, regexOptions);
                }
                else
                {
                    var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                    return fileName.Replace(find, replace, comparison);
                }

            case RenameRuleType.Regex:
                var pattern = GetStringParameter(rule.Parameters, "pattern");
                var replacement = GetStringParameter(rule.Parameters, "replacement");
                var regexIgnoreCase = GetBoolParameter(rule.Parameters, "ignoreCase");
                var options = regexIgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
                return Regex.Replace(fileName, pattern, replacement, options);

            case RenameRuleType.Sequence:
                return ApplySequenceRule(fileName, rule, GetIntParameter(rule.Parameters, "startIndex", 1));

            case RenameRuleType.Date:
                var format = GetStringParameter(rule.Parameters, "format", "yyyyMMdd");
                var datePosition = GetIntParameter(rule.Parameters, "position", 0);
                var dateSeparator = GetStringParameter(rule.Parameters, "separator", "_");
                var useModifiedDate = GetBoolParameter(rule.Parameters, "useModifiedDate");
                var dateStr = DateTime.Now.ToString(format);
                return datePosition switch
                {
                    0 => dateStr + dateSeparator + fileName,
                    1 => nameWithoutExt + dateSeparator + dateStr + extension,
                    2 => fileName + dateSeparator + dateStr,
                    _ => dateStr + dateSeparator + fileName
                };

            default:
                return fileName;
        }
    }

    private static string ApplySequenceRule(string fileName, RenameRule rule, int sequenceNumber)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        var padding = GetIntParameter(rule.Parameters, "padding", 3);
        var position = GetIntParameter(rule.Parameters, "position", 0);
        var separator = GetStringParameter(rule.Parameters, "separator", "_");
        var seqStr = sequenceNumber.ToString().PadLeft(padding, '0');

        return position switch
        {
            0 => seqStr + separator + fileName,
            1 => nameWithoutExt + separator + seqStr + extension,
            2 => fileName + separator + seqStr,
            _ => seqStr + separator + fileName
        };
    }

    private static string GetStringParameter(Dictionary<string, object> parameters, string key, string defaultValue = "")
    {
        if (parameters.TryGetValue(key, out var value) && value != null)
        {
            return value.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    private static int GetIntParameter(Dictionary<string, object> parameters, string key, int defaultValue = 0)
    {
        if (parameters.TryGetValue(key, out var value) && value != null)
        {
            if (int.TryParse(value.ToString(), out var result))
                return result;
        }
        return defaultValue;
    }

    private static bool GetBoolParameter(Dictionary<string, object> parameters, string key, bool defaultValue = false)
    {
        if (parameters.TryGetValue(key, out var value) && value != null)
        {
            if (bool.TryParse(value.ToString(), out var result))
                return result;
        }
        return defaultValue;
    }
}
