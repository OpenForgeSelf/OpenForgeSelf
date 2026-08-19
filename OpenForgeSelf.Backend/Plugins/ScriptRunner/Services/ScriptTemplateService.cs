using OpenForgeSelf.Abstractions;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;

public class ScriptTemplateService : IScriptTemplateService
{
    private readonly List<ScriptTemplate> _templates;
    private readonly List<ScriptTemplateCategory> _categories;

    public ScriptTemplateService()
    {
        _categories =
        [
            new ScriptTemplateCategory { Id = "file", Name = "文件处理", Icon = "📁", Description = "文件和目录操作相关脚本" },
            new ScriptTemplateCategory { Id = "system", Name = "系统管理", Icon = "⚙️", Description = "系统管理和监控相关脚本" },
            new ScriptTemplateCategory { Id = "network", Name = "网络请求", Icon = "🌐", Description = "网络请求和测试相关脚本" },
            new ScriptTemplateCategory { Id = "data", Name = "数据转换", Icon = "🔄", Description = "数据格式转换和处理脚本" },
            new ScriptTemplateCategory { Id = "text", Name = "文本处理", Icon = "📝", Description = "文本处理和分析脚本" }
        ];

        _templates = BuildTemplates();
        UpdateCategoryCounts();
    }

    public async Task<List<ScriptTemplate>> GetTemplatesAsync(string? category = null, string? keyword = null, ScriptLanguage? language = null)
    {
        try
        {
            XTrace.Log.Debug("[ScriptTemplateService] 获取模板列表，category={0}, keyword={1}, language={2}", category, keyword, language);

            var query = _templates.AsEnumerable();

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(keyword))
            {
                var kw = keyword.Trim().ToLowerInvariant();
                query = query.Where(t =>
                    t.Name.ToLowerInvariant().Contains(kw) ||
                    t.Description.ToLowerInvariant().Contains(kw) ||
                    t.Tags.Any(tag => tag.ToLowerInvariant().Contains(kw)));
            }

            if (language.HasValue)
            {
                query = query.Where(t => t.Language == language.Value);
            }

            return await Task.FromResult(query.ToList());
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptTemplateService] 获取模板列表失败: {0}", ex.Message);
            return [];
        }
    }

    public async Task<ScriptTemplate?> GetTemplateByIdAsync(string id)
    {
        try
        {
            XTrace.Log.Debug("[ScriptTemplateService] 获取模板详情，id={0}", id);
            return await Task.FromResult(_templates.FirstOrDefault(t => t.Id == id));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptTemplateService] 获取模板详情失败: {0}", ex.Message);
            return null;
        }
    }

    public async Task<List<ScriptTemplateCategory>> GetCategoriesAsync()
    {
        try
        {
            XTrace.Log.Debug("[ScriptTemplateService] 获取模板分类列表");
            return await Task.FromResult(_categories);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptTemplateService] 获取模板分类列表失败: {0}", ex.Message);
            return [];
        }
    }

    private void UpdateCategoryCounts()
    {
        foreach (var category in _categories)
        {
            category.TemplateCount = _templates.Count(t => t.Category == category.Id);
        }
    }

    private static List<ScriptTemplate> BuildTemplates()
    {
        return
        [
            new ScriptTemplate
            {
                Id = "file-batch-rename-py",
                Name = "文件批量重命名（Python）",
                Description = "批量重命名指定目录下的文件，支持前缀、后缀、序号等模式",
                Language = ScriptLanguage.Python,
                Category = "file",
                Tags = ["文件", "重命名", "批量", "Python"],
                Code = "import os\nimport argparse\nfrom pathlib import Path\n\ndef batch_rename(directory, prefix=\"\", suffix=\"\", start_num=1, extension=None):\n    path = Path(directory)\n    if not path.is_dir():\n        print(f\"错误: {directory} 不是有效的目录\")\n        return\n\n    files = [f for f in path.iterdir() if f.is_file()]\n    files.sort()\n\n    count = 0\n    for idx, file_path in enumerate(files, start=start_num):\n        name_stem = file_path.stem\n        name_ext = file_path.suffix\n\n        if extension and name_ext.lower() != extension.lower():\n            continue\n\n        new_name = f\"{prefix}{idx:03d}{suffix}{name_ext}\"\n        new_path = path / new_name\n\n        if new_path.exists():\n            print(f\"跳过（已存在）: {file_path.name} -> {new_name}\")\n            continue\n\n        file_path.rename(new_path)\n        print(f\"重命名: {file_path.name} -> {new_name}\")\n        count += 1\n\n    print(f\"\\n完成！共重命名 {count} 个文件\")\n\nif __name__ == \"__main__\":\n    parser = argparse.ArgumentParser(description=\"批量重命名文件\")\n    parser.add_argument(\"directory\", help=\"目标目录路径\")\n    parser.add_argument(\"--prefix\", default=\"\", help=\"文件名前缀\")\n    parser.add_argument(\"--suffix\", default=\"\", help=\"文件名后缀\")\n    parser.add_argument(\"--start-num\", type=int, default=1, help=\"起始序号\")\n    parser.add_argument(\"--extension\", default=None, help=\"只处理指定扩展名\")\n\n    args = parser.parse_args()\n    batch_rename(args.directory, prefix=args.prefix, suffix=args.suffix,\n                 start_num=args.start_num, extension=args.extension)\n",
                Parameters =
                [
                    new() { Name = "directory", Type = ScriptParameterType.DirectoryPath, Description = "目标目录路径", IsRequired = true },
                    new() { Name = "prefix", Type = ScriptParameterType.String, Description = "文件名前缀", DefaultValue = "" },
                    new() { Name = "suffix", Type = ScriptParameterType.String, Description = "文件名后缀", DefaultValue = "" },
                    new() { Name = "extension", Type = ScriptParameterType.String, Description = "只处理指定扩展名（如 .txt）", DefaultValue = "" }
                ],
                Version = "1.0.0",
                Author = "System"
            },

            new ScriptTemplate
            {
                Id = "file-dir-size-ps",
                Name = "目录大小统计（PowerShell）",
                Description = "统计指定目录及其子目录的大小，按大小排序显示",
                Language = ScriptLanguage.PowerShell,
                Category = "file",
                Tags = ["目录", "大小", "统计", "PowerShell"],
                Code = "param(\n    [Parameter(Mandatory=$true)]\n    [string]$Path,\n    [int]$Top = 20\n)\n\nfunction Get-DirectorySize {\n    param([string]$DirPath)\n    $size = 0\n    try {\n        $files = Get-ChildItem -Path $DirPath -File -Recurse -ErrorAction SilentlyContinue\n        foreach ($file in $files) {\n            $size += $file.Length\n        }\n    } catch {\n        Write-Warning \"无法访问目录: $DirPath\"\n    }\n    return $size\n}\n\nfunction Format-Size {\n    param([long]$Bytes)\n    $units = @(\"B\", \"KB\", \"MB\", \"GB\", \"TB\")\n    $unitIndex = 0\n    $size = [double]$Bytes\n    while ($size -ge 1024 -and $unitIndex -lt $units.Count - 1) {\n        $size /= 1024\n        $unitIndex++\n    }\n    return \"{0:N2} {1}\" -f $size, $units[$unitIndex]\n}\n\nWrite-Host \"正在计算目录大小...\" -ForegroundColor Cyan\nWrite-Host \"目标路径: $Path\" -ForegroundColor Gray\nWrite-Host \"\"\n\nif (-not (Test-Path -Path $Path)) {\n    Write-Error \"路径不存在: $Path\"\n    exit 1\n}\n\n$totalSize = Get-DirectorySize -DirPath $Path\n$totalDisplay = Format-Size -Bytes $totalSize\nWrite-Host \"总大小: $totalDisplay\" -ForegroundColor Green\nWrite-Host \"\"\n\n$subDirs = Get-ChildItem -Path $Path -Directory -ErrorAction SilentlyContinue\n$dirSizes = @()\n\nforeach ($dir in $subDirs) {\n    $size = Get-DirectorySize -DirPath $dir.FullName\n    $dirSizes += [PSCustomObject]@{\n        Name = $dir.Name\n        Size = $size\n        SizeDisplay = Format-Size -Bytes $size\n    }\n}\n\n$dirSizes | Sort-Object Size -Descending | Select-Object -First $Top | Format-Table -AutoSize\nWrite-Host \"完成！\" -ForegroundColor Green\n",
                Parameters =
                [
                    new() { Name = "Path", Type = ScriptParameterType.DirectoryPath, Description = "目标目录路径", IsRequired = true },
                    new() { Name = "Top", Type = ScriptParameterType.Number, Description = "显示前 N 个目录", DefaultValue = "20" }
                ],
                Version = "1.0.0",
                Author = "System"
            },

            new ScriptTemplate
            {
                Id = "file-backup-py",
                Name = "文件备份脚本（Python）",
                Description = "将指定目录备份到目标位置，支持增量备份和压缩",
                Language = ScriptLanguage.Python,
                Category = "file",
                Tags = ["备份", "文件", "压缩", "Python"],
                Code = "import os\nimport shutil\nimport zipfile\nfrom pathlib import Path\nfrom datetime import datetime\n\ndef backup_directory(source_dir, backup_dir, compress=False, incremental=False):\n    source = Path(source_dir)\n    backup = Path(backup_dir)\n\n    if not source.is_dir():\n        print(f\"错误: 源目录不存在: {source_dir}\")\n        return False\n\n    backup.mkdir(parents=True, exist_ok=True)\n    timestamp = datetime.now().strftime(\"%Y%m%d_%H%M%S\")\n    backup_name = f\"{source.name}_{timestamp}\"\n    backup_path = backup / backup_name\n\n    print(f\"开始备份: {source_dir} -> {backup_path}\")\n\n    if compress:\n        zip_path = backup / f\"{backup_name}.zip\"\n        with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zipf:\n            for file in source.rglob('*'):\n                if file.is_file():\n                    arcname = file.relative_to(source)\n                    zipf.write(file, arcname)\n        print(f\"备份完成（压缩）: {zip_path}\")\n    else:\n        shutil.copytree(source, backup_path)\n        print(f\"备份完成: {backup_path}\")\n\n    return True\n\nif __name__ == \"__main__\":\n    import argparse\n    parser = argparse.ArgumentParser(description=\"目录备份脚本\")\n    parser.add_argument(\"source\", help=\"源目录路径\")\n    parser.add_argument(\"backup_dir\", help=\"备份目标目录\")\n    parser.add_argument(\"--compress\", action=\"store_true\", help=\"启用压缩备份\")\n    args = parser.parse_args()\n    backup_directory(args.source, args.backup_dir, compress=args.compress)\n",
                Parameters =
                [
                    new() { Name = "source", Type = ScriptParameterType.DirectoryPath, Description = "源目录路径", IsRequired = true },
                    new() { Name = "backup_dir", Type = ScriptParameterType.DirectoryPath, Description = "备份目标目录", IsRequired = true },
                    new() { Name = "compress", Type = ScriptParameterType.Boolean, Description = "启用压缩备份", DefaultValue = "false" }
                ],
                Version = "1.0.0",
                Author = "System"
            },

            new ScriptTemplate
            {
                Id = "network-http-test-py",
                Name = "HTTP 请求测试（Python）",
                Description = "测试 HTTP 请求，支持 GET/POST、自定义头、超时设置",
                Language = ScriptLanguage.Python,
                Category = "network",
                Tags = ["HTTP", "网络", "测试", "Python"],
                Code = "import requests\nimport time\nfrom datetime import datetime\n\ndef test_http_request(url, method=\"GET\", headers=None, data=None, timeout=30):\n    print(f\"请求时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\")\n    print(f\"请求方法: {method}\")\n    print(f\"请求 URL: {url}\")\n    print(f\"超时时间: {timeout}s\")\n    print(\"-\" * 50)\n\n    start_time = time.time()\n    try:\n        if method.upper() == \"GET\":\n            response = requests.get(url, headers=headers, timeout=timeout)\n        elif method.upper() == \"POST\":\n            response = requests.post(url, headers=headers, json=data, timeout=timeout)\n        elif method.upper() == \"PUT\":\n            response = requests.put(url, headers=headers, json=data, timeout=timeout)\n        elif method.upper() == \"DELETE\":\n            response = requests.delete(url, headers=headers, timeout=timeout)\n        else:\n            print(f\"不支持的方法: {method}\")\n            return\n\n        elapsed = time.time() - start_time\n        print(f\"状态码: {response.status_code}\")\n        print(f\"响应时间: {elapsed:.3f}s\")\n        print(f\"内容长度: {len(response.content)} bytes\")\n        print(\"-\" * 50)\n        print(\"响应头:\")\n        for key, value in response.headers.items():\n            print(f\"  {key}: {value}\")\n        print(\"-\" * 50)\n        print(\"响应内容（前 500 字符）:\")\n        print(response.text[:500])\n        if len(response.text) > 500:\n            print(\"...\")\n\n    except requests.exceptions.Timeout:\n        print(f\"请求超时（{timeout}s）\")\n    except requests.exceptions.ConnectionError:\n        print(\"连接失败：无法连接到服务器\")\n    except Exception as e:\n        print(f\"请求失败: {e}\")\n\nif __name__ == \"__main__\":\n    import argparse\n    parser = argparse.ArgumentParser(description=\"HTTP 请求测试工具\")\n    parser.add_argument(\"url\", help=\"目标 URL\")\n    parser.add_argument(\"--method\", default=\"GET\", help=\"HTTP 方法\")\n    parser.add_argument(\"--timeout\", type=int, default=30, help=\"超时时间（秒）\")\n    args = parser.parse_args()\n    test_http_request(args.url, method=args.method, timeout=args.timeout)\n",
                Parameters =
                [
                    new() { Name = "url", Type = ScriptParameterType.String, Description = "目标 URL 地址", IsRequired = true },
                    new() { Name = "method", Type = ScriptParameterType.Select, Description = "HTTP 方法", DefaultValue = "GET", Options = ["GET", "POST", "PUT", "DELETE"] },
                    new() { Name = "timeout", Type = ScriptParameterType.Number, Description = "超时时间（秒）", DefaultValue = "30" }
                ],
                Version = "1.0.0",
                Author = "System"
            },

            new ScriptTemplate
            {
                Id = "network-port-scan-ps",
                Name = "端口扫描（PowerShell）",
                Description = "扫描指定主机的端口，检测端口是否开放",
                Language = ScriptLanguage.PowerShell,
                Category = "network",
                Tags = ["端口", "扫描", "网络", "PowerShell"],
                Code = "param(\n    [Parameter(Mandatory=$true)]\n    [string]$HostName,\n    [string]$Ports = \"80,443,3389,22,21,8080,3306,5432\",\n    [int]$Timeout = 1000\n)\n\nWrite-Host \"端口扫描工具\" -ForegroundColor Cyan\nWrite-Host \"目标主机: $HostName\" -ForegroundColor Gray\nWrite-Host \"扫描端口: $Ports\" -ForegroundColor Gray\nWrite-Host \"超时时间: ${Timeout}ms\" -ForegroundColor Gray\nWrite-Host \"\"\n\n$portList = $Ports -split ',' | ForEach-Object { $_.Trim() }\n$openPorts = @()\n$closedPorts = @()\n\nforeach ($port in $portList) {\n    $portNum = [int]$port\n    try {\n        $tcpClient = New-Object System.Net.Sockets.TcpClient\n        $connect = $tcpClient.BeginConnect($HostName, $portNum, $null, $null)\n        $wait = $connect.AsyncWaitHandle.WaitOne($Timeout, $false)\n        \n        if ($wait -and $tcpClient.Connected) {\n            Write-Host \"  [开放] 端口 $portNum\" -ForegroundColor Green\n            $openPorts += $portNum\n        } else {\n            Write-Host \"  [关闭] 端口 $portNum\" -ForegroundColor Red\n            $closedPorts += $portNum\n        }\n        $tcpClient.Close()\n    } catch {\n        Write-Host \"  [错误] 端口 $portNum : $($_.Exception.Message)\" -ForegroundColor Yellow\n    }\n}\n\nWrite-Host \"\"\nWrite-Host \"扫描完成！\" -ForegroundColor Cyan\nWrite-Host \"开放端口: $($openPorts.Count) 个\" -ForegroundColor Green\nWrite-Host \"关闭端口: $($closedPorts.Count) 个\" -ForegroundColor Red\n",
                Parameters =
                [
                    new() { Name = "HostName", Type = ScriptParameterType.String, Description = "目标主机名或 IP", IsRequired = true },
                    new() { Name = "Ports", Type = ScriptParameterType.String, Description = "端口列表（逗号分隔）", DefaultValue = "80,443,3389,22,21,8080,3306,5432" },
                    new() { Name = "Timeout", Type = ScriptParameterType.Number, Description = "超时时间（毫秒）", DefaultValue = "1000" }
                ],
                Version = "1.0.0",
                Author = "System"
            },

            new ScriptTemplate
            {
                Id = "data-json-format-py",
                Name = "JSON 格式化与验证（Python）",
                Description = "格式化 JSON 数据，验证 JSON 语法正确性",
                Language = ScriptLanguage.Python,
                Category = "data",
                Tags = ["JSON", "格式化", "验证", "Python"],
                Code = "import json\nimport sys\nfrom pathlib import Path\n\ndef format_json(input_data, indent=2, sort_keys=False):\n    try:\n        data = json.loads(input_data)\n        formatted = json.dumps(data, indent=indent, sort_keys=sort_keys, ensure_ascii=False)\n        print(\"JSON 验证通过！\")\n        print(f\"数据类型: {type(data).__name__}\")\n        if isinstance(data, dict):\n            print(f\"键的数量: {len(data)}\")\n        elif isinstance(data, list):\n            print(f\"元素数量: {len(data)}\")\n        print(\"-\" * 50)\n        print(formatted)\n        return True\n    except json.JSONDecodeError as e:\n        print(f\"JSON 验证失败！\")\n        print(f\"错误位置: 第 {e.lineno} 行，第 {e.colno} 列\")\n        print(f\"错误信息: {e.msg}\")\n        return False\n    except Exception as e:\n        print(f\"处理失败: {e}\")\n        return False\n\ndef format_json_file(file_path, indent=2, sort_keys=False):\n    path = Path(file_path)\n    if not path.exists():\n        print(f\"错误: 文件不存在: {file_path}\")\n        return False\n    \n    print(f\"处理文件: {file_path}\")\n    print(\"\")\n    \n    try:\n        content = path.read_text(encoding='utf-8')\n        return format_json(content, indent, sort_keys)\n    except Exception as e:\n        print(f\"读取文件失败: {e}\")\n        return False\n\nif __name__ == \"__main__\":\n    import argparse\n    parser = argparse.ArgumentParser(description=\"JSON 格式化与验证工具\")\n    parser.add_argument(\"input\", help=\"输入文件路径或 JSON 字符串\")\n    parser.add_argument(\"--indent\", type=int, default=2, help=\"缩进空格数\")\n    parser.add_argument(\"--sort-keys\", action=\"store_true\", help=\"按字母排序键\")\n    parser.add_argument(\"--file\", action=\"store_true\", help=\"输入是文件路径\")\n    args = parser.parse_args()\n    \n    if args.file:\n        format_json_file(args.input, indent=args.indent, sort_keys=args.sort_keys)\n    else:\n        format_json(args.input, indent=args.indent, sort_keys=args.sort_keys)\n",
                Parameters =
                [
                    new() { Name = "input", Type = ScriptParameterType.String, Description = "JSON 字符串或文件路径", IsRequired = true },
                    new() { Name = "indent", Type = ScriptParameterType.Number, Description = "缩进空格数", DefaultValue = "2" },
                    new() { Name = "sort_keys", Type = ScriptParameterType.Boolean, Description = "按字母排序键", DefaultValue = "false" }
                ],
                Version = "1.0.0",
                Author = "System"
            },

            new ScriptTemplate
            {
                Id = "data-csv-process-ps",
                Name = "CSV 数据处理（PowerShell）",
                Description = "读取和处理 CSV 文件，支持筛选、排序、导出",
                Language = ScriptLanguage.PowerShell,
                Category = "data",
                Tags = ["CSV", "数据", "处理", "PowerShell"],
                Code = "param(\n    [Parameter(Mandatory=$true)]\n    [string]$InputFile,\n    [string]$OutputFile = \"\",\n    [string]$FilterColumn = \"\",\n    [string]$FilterValue = \"\",\n    [string]$SortColumn = \"\",\n    [switch]$Descending\n)\n\nWrite-Host \"CSV 数据处理工具\" -ForegroundColor Cyan\nWrite-Host \"输入文件: $InputFile\" -ForegroundColor Gray\nWrite-Host \"\"\n\nif (-not (Test-Path -Path $InputFile)) {\n    Write-Error \"输入文件不存在: $InputFile\"\n    exit 1\n}\n\ntry {\n    $data = Import-Csv -Path $InputFile -Encoding UTF8\n    Write-Host \"读取成功，共 $($data.Count) 行数据\" -ForegroundColor Green\n    Write-Host \"列名: $($data[0].PSObject.Properties.Name -join ', ')\" -ForegroundColor Gray\n    Write-Host \"\"\n} catch {\n    Write-Error \"读取 CSV 文件失败: $($_.Exception.Message)\"\n    exit 1\n}\n\n$result = $data\n\nif ($FilterColumn -and $FilterValue) {\n    Write-Host \"筛选条件: $FilterColumn = $FilterValue\" -ForegroundColor Yellow\n    $result = $result | Where-Object { $_.$FilterColumn -eq $FilterValue }\n    Write-Host \"筛选后: $($result.Count) 行\" -ForegroundColor Yellow\n    Write-Host \"\"\n}\n\nif ($SortColumn) {\n    Write-Host \"排序依据: $SortColumn $($Descending ? '(降序)' : '(升序)')\" -ForegroundColor Yellow\n    $result = $result | Sort-Object -Property $SortColumn -Descending:$Descending\n    Write-Host \"\"\n}\n\nWrite-Host \"处理结果（前 10 行）:\" -ForegroundColor Cyan\n$result | Select-Object -First 10 | Format-Table -AutoSize\n\nif ($OutputFile) {\n    try {\n        $result | Export-Csv -Path $OutputFile -NoTypeInformation -Encoding UTF8\n        Write-Host \"\"\n        Write-Host \"已导出到: $OutputFile\" -ForegroundColor Green\n    } catch {\n        Write-Error \"导出失败: $($_.Exception.Message)\"\n    }\n}\n\nWrite-Host \"\"\nWrite-Host \"完成！\" -ForegroundColor Green\n",
                Parameters =
                [
                    new() { Name = "InputFile", Type = ScriptParameterType.FilePath, Description = "输入 CSV 文件路径", IsRequired = true },
                    new() { Name = "OutputFile", Type = ScriptParameterType.String, Description = "输出文件路径（可选）", DefaultValue = "" },
                    new() { Name = "FilterColumn", Type = ScriptParameterType.String, Description = "筛选列名（可选）", DefaultValue = "" },
                    new() { Name = "FilterValue", Type = ScriptParameterType.String, Description = "筛选值（可选）", DefaultValue = "" },
                    new() { Name = "SortColumn", Type = ScriptParameterType.String, Description = "排序列名（可选）", DefaultValue = "" }
                ],
                Version = "1.0.0",
                Author = "System"
            },

            new ScriptTemplate
            {
                Id = "system-info-ps",
                Name = "系统信息收集（PowerShell）",
                Description = "收集系统硬件、操作系统、网络等信息",
                Language = ScriptLanguage.PowerShell,
                Category = "system",
                Tags = ["系统", "信息", "硬件", "PowerShell"],
                Code = "Write-Host \"系统信息收集\" -ForegroundColor Cyan\nWrite-Host \"=\" * 50\n\nWrite-Host \"`n操作系统信息\" -ForegroundColor Yellow\n$os = Get-CimInstance Win32_OperatingSystem\nWrite-Host \"  操作系统: $($os.Caption)\"\nWrite-Host \"  版本: $($os.Version)\"\nWrite-Host \"  架构: $($os.OSArchitecture)\"\nWrite-Host \"  安装日期: $($os.InstallDate)\"\nWrite-Host \"  系统目录: $($os.SystemDirectory)\"\n\nWrite-Host \"`n计算机信息\" -ForegroundColor Yellow\n$computer = Get-CimInstance Win32_ComputerSystem\nWrite-Host \"  计算机名: $($computer.Name)\"\nWrite-Host \"  制造商: $($computer.Manufacturer)\"\nWrite-Host \"  型号: $($computer.Model)\"\nWrite-Host \"  总内存: $([math]::Round($computer.TotalPhysicalMemory / 1GB, 2)) GB\"\n\nWrite-Host \"`n处理器信息\" -ForegroundColor Yellow\n$cpu = Get-CimInstance Win32_Processor\nforeach ($c in $cpu) {\n    Write-Host \"  CPU: $($c.Name)\"\n    Write-Host \"  核心数: $($c.NumberOfCores)\"\n    Write-Host \"  逻辑处理器: $($c.NumberOfLogicalProcessors)\"\n    Write-Host \"  时钟频率: $($c.MaxClockSpeed) MHz\"\n}\n\nWrite-Host \"`n磁盘信息\" -ForegroundColor Yellow\n$disks = Get-CimInstance Win32_LogicalDisk -Filter \"DriveType=3\"\nforeach ($disk in $disks) {\n    $freeGB = [math]::Round($disk.FreeSpace / 1GB, 2)\n    $totalGB = [math]::Round($disk.Size / 1GB, 2)\n    $usedPercent = [math]::Round(($disk.Size - $disk.FreeSpace) / $disk.Size * 100, 1)\n    Write-Host \"  盘符 $($disk.DeviceID): ${freeGB}GB / ${totalGB}GB (${usedPercent}% 已用)\"\n}\n\nWrite-Host \"`n网络信息\" -ForegroundColor Yellow\n$adapters = Get-NetAdapter -Physical | Where-Object { $_.Status -eq 'Up' }\nforeach ($adapter in $adapters) {\n    Write-Host \"  网卡: $($adapter.Name)\"\n    Write-Host \"  速度: $($adapter.LinkSpeed)\"\n    $ip = Get-NetIPAddress -InterfaceIndex $adapter.InterfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue\n    if ($ip) {\n        Write-Host \"  IP 地址: $($ip.IPAddress)\"\n    }\n}\n\nWrite-Host \"`n\" -ForegroundColor Cyan\nWrite-Host \"信息收集完成！\" -ForegroundColor Green\n",
                Parameters = [],
                Version = "1.0.0",
                Author = "System"
            },

            new ScriptTemplate
            {
                Id = "text-log-analyze-py",
                Name = "日志分析脚本（Python）",
                Description = "分析日志文件，统计错误、警告数量，提取关键信息",
                Language = ScriptLanguage.Python,
                Category = "text",
                Tags = ["日志", "分析", "文本", "Python"],
                Code = "import re\nfrom pathlib import Path\nfrom collections import Counter\nfrom datetime import datetime\n\ndef analyze_log(file_path, error_patterns=None, warning_patterns=None):\n    path = Path(file_path)\n    if not path.exists():\n        print(f\"错误: 文件不存在: {file_path}\")\n        return\n\n    print(f\"分析日志文件: {file_path}\")\n    print(f\"文件大小: {path.stat().st_size / 1024:.2f} KB\")\n    print(\"-\" * 50)\n\n    total_lines = 0\n    error_lines = []\n    warning_lines = []\n    error_keywords = error_patterns or [\"ERROR\", \"Error\", \"error\", \"FAILED\", \"Failed\", \"failed\"]\n    warning_keywords = warning_patterns or [\"WARN\", \"warn\", \"WARNING\", \"warning\"]\n    level_counter = Counter()\n\n    try:\n        with open(path, 'r', encoding='utf-8', errors='ignore') as f:\n            for line_num, line in enumerate(f, 1):\n                total_lines += 1\n                line_lower = line.lower()\n\n                for kw in error_keywords:\n                    if kw.lower() in line_lower:\n                        error_lines.append((line_num, line.strip()))\n                        level_counter['error'] += 1\n                        break\n\n                for kw in warning_keywords:\n                    if kw.lower() in line_lower:\n                        warning_lines.append((line_num, line.strip()))\n                        level_counter['warning'] += 1\n                        break\n\n        print(f\"总行数: {total_lines}\")\n        print(f\"错误数: {len(error_lines)}\")\n        print(f\"警告数: {len(warning_lines)}\")\n        print()\n\n        if error_lines:\n            print(\"错误样例（前 10 条）:\")\n            for line_num, line in error_lines[:10]:\n                print(f\"  行 {line_num}: {line[:100]}\")\n            print()\n\n        if warning_lines:\n            print(\"警告样例（前 10 条）:\")\n            for line_num, line in warning_lines[:10]:\n                print(f\"  行 {line_num}: {line[:100]}\")\n\n        print()\n        print(\"分析完成！\")\n\n    except Exception as e:\n        print(f\"分析失败: {e}\")\n\nif __name__ == \"__main__\":\n    import argparse\n    parser = argparse.ArgumentParser(description=\"日志分析工具\")\n    parser.add_argument(\"file\", help=\"日志文件路径\")\n    args = parser.parse_args()\n    analyze_log(args.file)\n",
                Parameters =
                [
                    new() { Name = "file_path", Type = ScriptParameterType.FilePath, Description = "日志文件路径", IsRequired = true }
                ],
                Version = "1.0.0",
                Author = "System"
            },

            new ScriptTemplate
            {
                Id = "text-batch-replace-py",
                Name = "批量文本替换（Python）",
                Description = "批量替换目录中文件的文本内容，支持正则表达式",
                Language = ScriptLanguage.Python,
                Category = "text",
                Tags = ["文本", "替换", "批量", "Python"],
                Code = "import os\nimport re\nfrom pathlib import Path\n\ndef batch_replace(directory, find_text, replace_text, file_pattern=\"*.txt\", use_regex=False, recursive=True):\n    path = Path(directory)\n    if not path.is_dir():\n        print(f\"错误: 目录不存在: {directory}\")\n        return\n\n    print(f\"批量文本替换\")\n    print(f\"目录: {directory}\")\n    print(f\"查找: {find_text}\")\n    print(f\"替换为: {replace_text}\")\n    print(f\"文件模式: {file_pattern}\")\n    print(f\"使用正则: {use_regex}\")\n    print(\"-\" * 50)\n\n    files_processed = 0\n    total_replacements = 0\n\n    if recursive:\n        files = list(path.rglob(file_pattern))\n    else:\n        files = list(path.glob(file_pattern))\n\n    for file_path in files:\n        if not file_path.is_file():\n            continue\n\n        try:\n            content = file_path.read_text(encoding='utf-8')\n            original_content = content\n\n            if use_regex:\n                content, count = re.subn(find_text, replace_text, content)\n            else:\n                count = content.count(find_text)\n                content = content.replace(find_text, replace_text)\n\n            if count > 0:\n                file_path.write_text(content, encoding='utf-8')\n                print(f\"  {file_path.name}: {count} 处替换\")\n                files_processed += 1\n                total_replacements += count\n\n        except Exception as e:\n            print(f\"  {file_path.name}: 处理失败 - {e}\")\n\n    print()\n    print(f\"完成！处理了 {files_processed} 个文件，共 {total_replacements} 处替换\")\n\nif __name__ == \"__main__\":\n    import argparse\n    parser = argparse.ArgumentParser(description=\"批量文本替换工具\")\n    parser.add_argument(\"directory\", help=\"目标目录\")\n    parser.add_argument(\"find\", help=\"要查找的文本\")\n    parser.add_argument(\"replace\", help=\"替换为的文本\")\n    parser.add_argument(\"--pattern\", default=\"*.txt\", help=\"文件匹配模式\")\n    parser.add_argument(\"--regex\", action=\"store_true\", help=\"使用正则表达式\")\n    parser.add_argument(\"--no-recursive\", action=\"store_true\", help=\"不递归子目录\")\n    args = parser.parse_args()\n    batch_replace(args.directory, args.find, args.replace,\n                  file_pattern=args.pattern, use_regex=args.regex,\n                  recursive=not args.no_recursive)\n",
                Parameters =
                [
                    new() { Name = "directory", Type = ScriptParameterType.DirectoryPath, Description = "目标目录路径", IsRequired = true },
                    new() { Name = "find_text", Type = ScriptParameterType.String, Description = "要查找的文本", IsRequired = true },
                    new() { Name = "replace_text", Type = ScriptParameterType.String, Description = "替换为的文本", IsRequired = true },
                    new() { Name = "file_pattern", Type = ScriptParameterType.String, Description = "文件匹配模式", DefaultValue = "*.txt" },
                    new() { Name = "use_regex", Type = ScriptParameterType.Boolean, Description = "使用正则表达式", DefaultValue = "false" }
                ],
                Version = "1.0.0",
                Author = "System"
            }
        ];
    }
}
