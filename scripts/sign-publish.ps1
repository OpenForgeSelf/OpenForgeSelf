# ForgeSelf 发布产物 Authenticode 签名脚本
#
# 作用：给 publish\ForgeSelf.exe（可选连同主程序集）做 Windows 代码签名，
#       使资源管理器属性页显示「已验证的发布者」、UAC 不再是「未知发布者」。
#
# 用法:
#   .\scripts\sign-publish.ps1                      # 自签证书（自动装到本机受信任根）签 exe + ForgeSelf*.dll
#   .\scripts\sign-publish.ps1 -AllAssemblies       # 连同 publish 下全部 DLL 一起签
#   .\scripts\sign-publish.ps1 -Force               # 已签名的也重签
#   .\scripts\sign-publish.ps1 -Thumbprint <指纹>   # 使用当前用户「个人」存储里已有的证书
#   .\scripts\sign-publish.ps1 -PfxPath a.pfx -PfxPassword xxx   # 使用 pfx（商业/云证书）
#   .\scripts\sign-publish.ps1 -ExportPfx <路径>    # 自签时同时导出 pfx 备份（含私钥，慎存）
#   .\scripts\sign-publish.ps1 -NoTimestamp         # 跳过 RFC3161 时间戳（离线环境；证书过期后签名失效）
#
# 说明：证书策略可插拔——先用自签跑通流程，将来换商业/云证书只需传 -PfxPath，
#       脚本其余逻辑无需改动。

param(
    [string]$PublishDir,
    [string]$PublisherName = 'OpenForgeSelf 铸己匣',
    [string]$Thumbprint,
    [string]$PfxPath,
    [string]$PfxPassword,
    [string]$ExportPfx,
    [switch]$AllAssemblies,
    [switch]$Force,
    [switch]$NoTimestamp,
    [string]$TimestampServer = 'http://timestamp.digicert.com',
    [int]$ValidYears = 3
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PublishDir)) { $PublishDir = Join-Path $root 'publish' }

# ── 第 1 步：定位 signtool（Windows SDK，通常不在 PATH）──
function Resolve-SignTool {
    $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path $kits) {
        $hit = Get-ChildItem -Path $kits -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    return $null
}

$signtool = Resolve-SignTool
if (-not $signtool) {
    Write-Host '[错误] 未找到 signtool.exe。请安装 Windows SDK（Signing Tools for Desktop Apps）后重试。' -ForegroundColor Red
    exit 1
}

# 统一经 cmd 调用原生命令：PS 5.1 在 $ErrorActionPreference='Stop' 下会把原生进程的 stderr
# 当作终止错误（signtool verify 对未签名文件必然输出 "SignTool Error: No signature found."），
# 直接 & 调用会让脚本在预期分支上崩掉。
function Invoke-Native {
    param([string]$Exe, [string[]]$Arguments)
    $quoted = ($Arguments | ForEach-Object { '"{0}"' -f $_ }) -join ' '
    $output = & cmd.exe /c "`"$Exe`" $quoted 2>&1"
    return @{ ExitCode = $LASTEXITCODE; Text = ($output | Out-String) }
}

Write-Host "[1/5] signtool: $signtool" -ForegroundColor Gray

if (-not (Test-Path $PublishDir)) {
    Write-Host "[错误] 发布目录不存在: $PublishDir（请先运行 build.ps1）" -ForegroundColor Red
    exit 1
}

# ── 第 2 步：解析/准备签名证书 ──
$cert = $null
$selfSigned = $false

if (-not [string]::IsNullOrWhiteSpace($Thumbprint)) {
    $cert = Get-ChildItem -Path "Cert:\CurrentUser\My\$Thumbprint" -ErrorAction SilentlyContinue
    if (-not $cert) {
        Write-Host "[错误] 在当前用户「个人」存储中找不到指纹为 $Thumbprint 的证书" -ForegroundColor Red
        exit 1
    }
    Write-Host "[2/5] 使用指定证书: $($cert.Subject) ($($cert.Thumbprint))" -ForegroundColor Green
}
elseif (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
    if (-not (Test-Path $PfxPath)) {
        Write-Host "[错误] pfx 文件不存在: $PfxPath" -ForegroundColor Red
        exit 1
    }
    $securePwd = ConvertTo-SecureString -String $PfxPassword -AsPlainText -Force
    $cert = Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation Cert:\CurrentUser\My -Password $securePwd
    Write-Host "[2/5] 已导入 pfx 证书: $($cert.Subject) ($($cert.Thumbprint))" -ForegroundColor Green
}
else {
    # 复用同主题的未过期自签证书，避免每次发布都生成新证书
    $subject = "CN=$PublisherName"
    $cert = Get-ChildItem -Path Cert:\CurrentUser\My -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date).AddDays(1) } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if ($cert) {
        $selfSigned = $true
        Write-Host "[2/5] 复用已有自签证书: $($cert.Subject) ($($cert.Thumbprint))" -ForegroundColor Green
    }
    else {
        $cert = New-SelfSignedCertificate `
            -Type CodeSigningCert `
            -Subject $subject `
            -CertStoreLocation Cert:\CurrentUser\My `
            -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 `
            -NotAfter ((Get-Date).AddYears($ValidYears)) `
            -FriendlyName 'ForgeSelf Code Signing'
        $selfSigned = $true
        Write-Host "[2/5] 已生成自签代码签名证书: $($cert.Subject) ($($cert.Thumbprint))" -ForegroundColor Green
    }
}

# ── 第 3 步：自签证书需要被本机信任，否则属性页仍显示「未知发布者」──
if ($selfSigned) {
    # ⚠ 不能用 X509Store.Add 写「受信任的根证书颁发机构」：Windows 会弹出确认对话框，
    #   在非交互（CI/脚本）会话里会永久挂起（已实测踩坑）。改用 certutil 静默导入。
    $already = Get-ChildItem -Path Cert:\CurrentUser\Root -ErrorAction SilentlyContinue |
        Where-Object { $_.Thumbprint -eq $cert.Thumbprint }
    if ($already) {
        Write-Host '[3/5] 证书已在受信任根列表中，跳过' -ForegroundColor Gray
    }
    else {
        $cerPath = Join-Path $env:TEMP "ForgeSelfCodeSigning-$($cert.Thumbprint).cer"
        Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT -Force | Out-Null
        $r = Invoke-Native -Exe 'certutil.exe' -Arguments @('-user', '-addstore', 'Root', $cerPath)
        Remove-Item $cerPath -Force -ErrorAction SilentlyContinue
        if ($r.ExitCode -eq 0) {
            Write-Host '[3/5] 已加入「受信任的根证书颁发机构」（仅当前用户，非全机）' -ForegroundColor Green
        }
        else {
            Write-Host '[3/5] 警告：证书未加入受信任根，属性页仍会显示发布者未知（可重跑本脚本）' -ForegroundColor Yellow
            Write-Host $r.Text.Trim() -ForegroundColor DarkYellow
        }
    }

    # 记录指纹，便于下次复用 / CI 传入 -Thumbprint
    $markDir = Join-Path $root '.forgeself'
    if (-not (Test-Path $markDir)) { New-Item -Path $markDir -ItemType Directory -Force | Out-Null }
    Set-Content -Path (Join-Path $markDir 'codesign-thumbprint.txt') -Value $cert.Thumbprint -Encoding UTF8

    if (-not [string]::IsNullOrWhiteSpace($ExportPfx)) {
        $exportPwd = ConvertTo-SecureString -String $PfxPassword -AsPlainText -Force
        Export-PfxCertificate -Cert $cert -FilePath $ExportPfx -Password $exportPwd | Out-Null
        Write-Host "      已导出 pfx（含私钥）: $ExportPfx" -ForegroundColor Yellow
    }
}
else {
    Write-Host '[3/5] 使用外部证书，跳过信任安装' -ForegroundColor Gray
}

# ── 第 4 步：收集待签文件并签名 ──
$targets = @()
$targets += Get-ChildItem -Path $PublishDir -Filter *.exe -File
if ($AllAssemblies) {
    $targets += Get-ChildItem -Path $PublishDir -Filter *.dll -File
}
else {
    $targets += Get-ChildItem -Path $PublishDir -Filter 'ForgeSelf*.dll' -File
}

if ($targets.Count -eq 0) {
    Write-Host "[错误] $PublishDir 下没有可签名的 PE 文件" -ForegroundColor Red
    exit 1
}

Write-Host "[4/5] 待签名 $($targets.Count) 个文件（时间戳: $(if ($NoTimestamp) { '关闭' } else { $TimestampServer })）" -ForegroundColor Cyan

$signed = 0
$skipped = 0
$failed = 0
$locked = @()

foreach ($file in $targets) {
    # 已签名且未要求强制重签 → 跳过
    if (-not $Force) {
        $probe = Invoke-Native -Exe $signtool -Arguments @('verify', '/pa', $file.FullName)
        if ($probe.ExitCode -eq 0) {
            Write-Host "      跳过（已签名）: $($file.Name)" -ForegroundColor Gray
            $skipped++
            continue
        }
    }

    $signArgs = @('sign', '/fd', 'SHA256', '/td', 'SHA256', '/sha1', $cert.Thumbprint, '/v')
    if (-not $NoTimestamp) { $signArgs += @('/tr', $TimestampServer) }
    $signArgs += $file.FullName

    $result = Invoke-Native -Exe $signtool -Arguments $signArgs
    if ($result.ExitCode -eq 0) {
        Write-Host "      已签名: $($file.Name)" -ForegroundColor Green
        $signed++
    }
    else {
        $text = $result.Text
        if ($text -match 'being used by another process|Access is denied|共享冲突') {
            $locked += $file.Name
            Write-Host "      被占用，无法写入: $($file.Name)（多半是实例正在运行，停止后重跑即可）" -ForegroundColor Yellow
        }
        else {
            Write-Host "      签名失败: $($file.Name)" -ForegroundColor Red
            Write-Host $text.Trim() -ForegroundColor DarkRed
        }
        $failed++
    }
}

# ── 第 5 步：校验 ──
Write-Host '[5/5] 校验签名...' -ForegroundColor Cyan
$okCount = 0
foreach ($file in $targets) {
    $v = Invoke-Native -Exe $signtool -Arguments @('verify', '/pa', $file.FullName)
    if ($v.ExitCode -eq 0) { $okCount++ }
}

Write-Host ''
Write-Host '================================================' -ForegroundColor Cyan
Write-Host "  签名完成：新增 $signed / 跳过 $skipped / 失败 $failed" -ForegroundColor Yellow
if ($okCount -eq $targets.Count) {
    Write-Host "  校验通过：$okCount / $($targets.Count)" -ForegroundColor Green
}
else {
    Write-Host "  校验通过：$okCount / $($targets.Count)" -ForegroundColor Yellow
}
Write-Host "  发布者：$($cert.Subject)" -ForegroundColor Green
if ($locked.Count -gt 0) {
    Write-Host '  ⚠ 以下文件被占用（多半是实例正在运行），请先停止再重签：' -ForegroundColor Yellow
    foreach ($n in $locked) { Write-Host "      - $n" -ForegroundColor Yellow }
}
Write-Host '================================================' -ForegroundColor Cyan

if ($failed -gt 0) { exit 1 }
