using System.Text;
using System.Text.Json;

namespace ForgeSelf.Api.Plugins.Services;

/// <summary>在数据根 <c>plugins/{id}/src</c> 生成独立仓库骨架。不改 <c>PluginScaffolderService</c> 的字节包。</summary>
/// <remarks>
/// 2026-11-06 验收收尾批重写（守卫 <c>PluginScaffoldReleaseTemplateTests</c> 锁定）：
/// 布局对齐目标仓库结构（src/&lt;Name&gt;/ + tests/ + scripts/ + web/ + lib/ + .github/workflows/）；
/// 引用走仓内 lib/ 提交程序集（GitHub runner 上不存在宿主仓的 8 级相对路径）；
/// workflow 的密钥只允许 step env 映射（secrets 不能出现在 if:），全部命令显式工程路径；
/// 打包步把根 plugin.json 拷进产物并剔除宿主共享程序集。
/// 模板用 __SAFENAME__/__SHORTNAME__ 占位符 + Replace，不用插值原始字符串（防大括号转义事故）。
/// </remarks>
public static class PluginSrcScaffold
{
    public static void Write(string srcDirectory, string pluginId, string name)
    {
        Directory.CreateDirectory(srcDirectory);
        var safeName = string.IsNullOrWhiteSpace(name) ? "Plugin" : name.Trim();
        var shortName = safeName.StartsWith("Plugin.", StringComparison.Ordinal) ? safeName["Plugin.".Length..] : safeName;
        var projectDir = Path.Combine(srcDirectory, "src", safeName);
        var testsDir = Path.Combine(srcDirectory, "tests", safeName + ".Tests");
        var scriptsDir = Path.Combine(srcDirectory, "scripts");
        var webDir = Path.Combine(srcDirectory, "web");
        var workflowDir = Path.Combine(srcDirectory, ".github", "workflows");
        var libDir = Path.Combine(srcDirectory, "lib");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(testsDir);
        Directory.CreateDirectory(scriptsDir);
        Directory.CreateDirectory(webDir);
        Directory.CreateDirectory(workflowDir);
        Directory.CreateDirectory(libDir);

        var manifest = new
        {
            Id = pluginId,
            Name = safeName,
            Version = "1.0.0",
            HostApiVersion = "1.0",
            EntryAssembly = safeName + ".dll",
            EntryType = safeName + ".HelloPlugin",
            Description = "OpenForgeSelf 独立插件骨架"
        };
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(srcDirectory, "plugin.json"), json);
        File.WriteAllText(Path.Combine(projectDir, "ForgeSelf.PluginManifest.json"), json);
        File.WriteAllText(Path.Combine(projectDir, "HelloPlugin.cs"), HelloPluginCs.Replace("__SAFENAME__", safeName));
        File.WriteAllText(Path.Combine(projectDir, safeName + ".csproj"), Csproj.Replace("__SAFENAME__", safeName));
        File.WriteAllText(Path.Combine(testsDir, safeName + ".Tests.csproj"), TestsCs.Replace("__SAFENAME__", safeName));
        File.WriteAllText(Path.Combine(testsDir, "HelloPluginTests.cs"), HelloPluginTestsCs.Replace("__SAFENAME__", safeName));
        File.WriteAllText(Path.Combine(srcDirectory, ".gitignore"), Gitignore);
        File.WriteAllText(Path.Combine(srcDirectory, "README.md"), ReadmeMd.Replace("__SAFENAME__", safeName).Replace("__SHORTNAME__", shortName));
        File.WriteAllText(Path.Combine(srcDirectory, "CHANGELOG.md"), ChangelogMd);
        File.WriteAllText(Path.Combine(srcDirectory, "LICENSE"), License);
        File.WriteAllText(Path.Combine(webDir, "README.md"), WebReadmeMd);
        // 脚本可能被旧 shell 拉起，按 AGENTS §2.3 带 UTF-8 BOM。
        File.WriteAllText(Path.Combine(scriptsDir, "build.ps1"), BuildPs1.Replace("__SAFENAME__", safeName), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.WriteAllText(Path.Combine(workflowDir, "release.yml"), ReleaseWorkflow.Replace("__SAFENAME__", safeName).Replace("__SHORTNAME__", shortName));
        CopyReferenceAssemblies(libDir);
    }

    private static void CopyReferenceAssemblies(string libDir)
    {
        foreach (var dll in new[] { "ForgeSelf.Abstractions.dll", "ForgeSelf.Core.dll" })
        {
            var from = Path.Combine(AppContext.BaseDirectory, dll);
            if (File.Exists(from))
                File.Copy(from, Path.Combine(libDir, dll), overwrite: true);
        }
    }

    private const string HelloPluginCs = """
using ForgeSelf.Abstractions;
using ForgeSelf.Core;

namespace __SAFENAME__;

public sealed class HelloPlugin : IPlugin
{
    public void Apply(IContext ctx) { }
}
""";

    private const string Csproj = """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>__SAFENAME__</RootNamespace>
    <AssemblyName>__SAFENAME__</AssemblyName>
    <VersionPrefix>1.0.0</VersionPrefix>
    <Version>1.0.0</Version>
    <PackageVersion>1.0.0.0</PackageVersion>
    <FileVersion>1.0.0.0</FileVersion>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <Description>NuGet PackageVersion uses three segments plus .0; plugin.json Version stays in lockstep with the git tag.</Description>
  </PropertyGroup>
  <ItemGroup>
    <EmbeddedResource Include="ForgeSelf.PluginManifest.json" LogicalName="ForgeSelf.PluginManifest.json" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="ForgeSelf.Abstractions" Private="false">
      <HintPath>..\..\lib\ForgeSelf.Abstractions.dll</HintPath>
    </Reference>
    <Reference Include="ForgeSelf.Core" Private="false">
      <HintPath>..\..\lib\ForgeSelf.Core.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
""";

    private const string TestsCs = """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\__SAFENAME__\__SAFENAME__.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="ForgeSelf.Abstractions" Private="true">
      <HintPath>..\..\lib\ForgeSelf.Abstractions.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
""";

    private const string HelloPluginTestsCs = """
using ForgeSelf.Abstractions;
using Xunit;

namespace __SAFENAME__.Tests;

public class HelloPluginTests
{
    [Fact]
    public void Entry_Implements_IPlugin()
    {
        IPlugin plugin = new HelloPlugin();
        Assert.Equal("__SAFENAME__.HelloPlugin", plugin.GetType().FullName);
    }
}
""";

    private const string BuildPs1 = """
# Build and test the __SAFENAME__ plugin. Run with pwsh, not Windows PowerShell 5.1.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet test (Join-Path $root 'tests/__SAFENAME__.Tests/__SAFENAME__.Tests.csproj') --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
""";

    private const string ReadmeMd = """
# __SAFENAME__

OpenForgeSelf 独立插件骨架（一个插件一个仓库）。

- 源码在 `src/__SAFENAME__/`；运行版本由宿主安装到数据根 `plugins/{id}/versions/`，源码目录 `src/` 与作业记录 `.forge/` 在卸载时保留。
- 版本统一：git tag（如 `v1.0.1`）= `plugin.json` 的 `Version` = GitHub Release 包内清单版本。
- NuGet 包名 = 仓库名（`OpenForgeSelf.Plugin.*`）；NuGet `PackageVersion` 用三段（10 位时间码不进 NuGet）。
- 发行：push tag `v*` → `.github/workflows/release.yml` 产 `__SHORTNAME__-<ver>.zip` + `SHA256SUMS.txt` 并发 GitHub Release；NuGet 上传需仓库配置 `NUGET_API_KEY` secret，未配置时只产 nupkg 不上传（CI 保持绿）。
- 安装：宿主插件市场填 GitHub 仓库地址即可拉取最新 Release。
""";

    private const string ChangelogMd = """
# Changelog

## 1.0.0
- 首个骨架（PluginSrcScaffold 生成）。
""";

    private const string License = """
MIT License

Copyright (c) 2026 OpenForgeSelf

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
""";

    private const string WebReadmeMd = """
# web

可选前端。构建产物放在 `web/dist/`，打包脚本会把它抄进 zip。本骨架没有界面。
""";

    private const string Gitignore = """
bin/
obj/
*.zip
*.nupkg
.vs/
artifacts/
""";

    private const string ReleaseWorkflow = """
name: release
on:
  push:
    tags:
      - "v*"
permissions:
  contents: write
jobs:
  pack:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
      - name: test
        run: dotnet test tests/__SAFENAME__.Tests/__SAFENAME__.Tests.csproj --nologo
      - name: package zip
        shell: pwsh
        run: |
          $name = $env:GITHUB_REPOSITORY.Split('/')[1] -replace '^OpenForgeSelf\.Plugin\.', ''
          $ver = $env:GITHUB_REF_NAME.TrimStart("v")
          dotnet publish src/__SAFENAME__/__SAFENAME__.csproj -c Release -o "$env:RUNNER_TEMP/pub" --nologo
          Copy-Item plugin.json "$env:RUNNER_TEMP/pub/plugin.json"
          Get-ChildItem "$env:RUNNER_TEMP/pub" -File | Where-Object {
            $n = $_.Name
            $n.StartsWith('ForgeSelf.') -or $n.StartsWith('NewLife.') -or $n.StartsWith('Microsoft.Extensions.') -or $n -in @('XCode.dll','XCode.pdb','MX.dll','MX.pdb')
          } | Remove-Item -Force
          Compress-Archive -Path "$env:RUNNER_TEMP/pub/*" -DestinationPath "__SHORTNAME__-$ver.zip"
          Get-FileHash "__SHORTNAME__-$ver.zip" -Algorithm SHA256 | ForEach-Object { "$($_.Hash)  __SHORTNAME__-$ver.zip" } | Set-Content SHA256SUMS.txt -Encoding utf8
      - name: github release
        uses: softprops/action-gh-release@v2
        with:
          files: |
            __SHORTNAME__-*.zip
            SHA256SUMS.txt
      - name: nuget pack and push
        shell: pwsh
        env:
          NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
        run: |
          $id = $env:GITHUB_REPOSITORY.Split('/')[1]
          $ver = $env:GITHUB_REF_NAME.TrimStart("v")
          $nu = if ($ver -match '^(\d+\.\d+\.\d+)\.\d+$') { $Matches[1] + '.0' } else { $ver }
          dotnet pack src/__SAFENAME__/__SAFENAME__.csproj -c Release -o "$env:RUNNER_TEMP/nupkg" -p:PackageId="$id" -p:PackageVersion="$nu" --nologo
          if ([string]::IsNullOrWhiteSpace($env:NUGET_API_KEY)) {
            Write-Host "NUGET_API_KEY not configured: nupkg built, upload skipped (human-gated)."
            exit 0
          }
          Get-ChildItem "$env:RUNNER_TEMP/nupkg" -Filter *.nupkg | ForEach-Object {
            dotnet nuget push $_.FullName --source https://api.nuget.org/v3/index.json --api-key "$env:NUGET_API_KEY" --skip-duplicate
          }
""";
}
