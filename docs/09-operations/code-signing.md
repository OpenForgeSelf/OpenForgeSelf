# 09-operations — 代码签名（Authenticode）

> 状态：已实现（2026-09-10 落地）
> 最后更新：2026-09-10

给发布产物 `publish\ForgeSelf.exe`（可选连同主程序集）做 **Windows 代码签名（Authenticode）**，
使资源管理器「属性 → 数字签名」显示「已验证的发布者」，UAC/下载警告不再显示「未知发布者」。

> 代码事实：证书策略**可插拔**——默认自签跑通流程，将来换商业/云证书只需传 `-PfxPath`，脚本其余逻辑无需改动。

## 1. 它能解决什么

| 场景 | 签名前 | 签名后（自签，本机信任） |
|---|---|---|
| 资源管理器属性页 | 无数字签名 | 显示发布者 `CN=OpenForgeSelf 铸己匣` |
| UAC / SmartScreen（本机） | 未知发布者 | 已验证发布者 |
| 分发给他人 | — | 仍「未知发布者」+ SmartScreen（需公共信任证书，见 §6） |

签名**不**改变运行行为，只附加签名块；带 RFC3161 时间戳，证书过期后签名依然有效。

## 2. 脚本与开关

- `scripts/sign-publish.ps1`：核心签名脚本（5 步：定位 signtool → 解析证书 → 装信任根 → 签名 → verify）。
- `build.ps1 -Sign`：发布完成后自动调用签名（**默认不签**，行为不变）。
- `build.ps1 -Sign -SignAll`：签名范围覆盖 `publish` 下**全部** DLL。

## 3. 证书策略（可插拔）

| 用法 | 命令 | 适用 |
|---|---|---|
| 自签（默认） | `.\scripts\sign-publish.ps1` | 本机开发/自迭代，零成本 |
| 复用已有证书 | `... -Thumbprint <指纹>` | 证书已在当前用户「个人」存储 |
| 商业/云证书 | `... -PfxPath a.pfx -PfxPassword xxx` | 对外分发（OV/EV/Azure Trusted Signing/Certum） |
| 导出 pfx 备份 | `... -ExportPfx <路径>` | 自签时导出含私钥的 pfx（**慎存**） |
| 跳过时间戳 | `... -NoTimestamp` | 离线环境（代价：证书过期后签名失效） |

默认范围：签 `*.exe` + `ForgeSelf*.dll`；`-AllAssemblies` 覆盖全部 DLL；插件 DLL 走各自发布流程，**不在此签名**。

## 4. 典型用法

```powershell
# 1) 发布 + 签名（推荐，一条龙）
.\build.ps1 -Sign

# 2) 仅对已有 publish 目录补签
.\scripts\sign-publish.ps1 -PublishDir publish

# 3) 换商业证书（对外分发用）
.\scripts\sign-publish.ps1 -PfxPath .\certs\forgeself.pfx -PfxPassword $env:CODESIGN_PWD
```

## 5. 校验

```powershell
$s = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe'
& $s verify /pa publish\ForgeSelf.exe
```

预期：退出码 0，输出含 `Signing Certificate Chain`、时间戳由 `DigiCert` 等 TSA 签署。

## 6. 局限性（何时需要公共信任证书）

自签证书**只在本机被信任**（已导入当前用户「受信任的根证书颁发机构」）。要消除他人机器上的
「未知发布者」+ SmartScreen，需下列之一（拿到后仅改 `-PfxPath` 入参）：

- **商业 OV/EV 证书**：年费数百~数千元；EV 即时获 SmartScreen 声誉，但需 USB token。
- **Azure Trusted Signing**：约 $10/月，公共信任 + 声誉，需 Azure 订阅与身份审批。
- **Certum 开源代码签名**：≈€14/年起，需证明开源项目身份。

> 当前项目愿景为自用自迭代工具，自签已解决「UAC 显示发布者」这一真实痛点；是否对外分发再决定是否升级证书。

## 7. 已记坑（务必先看，避免重踩）

1. **运行中实例不可签**：exe 被内存映射，signtool 写入失败且有损坏 PE 风险。签名前先停实例。
2. **`X509Store.Add` 弹确认框挂死**：往受信任根写会弹 Windows 确认框，非交互会话永久挂起 → 改用 `certutil -user -addstore Root`。
3. **PS 5.1 + `$ErrorActionPreference='Stop'` 下 signtool 的 stderr 触发终止错误**（`verify` 对未签名文件必报 `No signature found.`）→ 脚本统一包 `cmd /c … 2>&1` 取 `$LASTEXITCODE`。
4. **含中文注释的 ps1 必须 UTF-8 BOM**，否则 PS 5.1 按 GBK 解析导致中文/参数解析错乱。
5. **exe 偶发 `0x80093102`**：多为 Defender 实时扫描瞬时占用文件 → 单独重试一次即可（同批 DLL 通常已成功）。

## 8. 端口说明（与签名无关，但易混）

- **本地开发**：`7102`（后端）/ `7002`（前端 dev server），见 `start.ps1` 与 `vite.config.ts` dev proxy。
- **日常运行实例**：`51888`，由 `ForgeSetting.config` 的 `PortNumber` 决定（`publish\ForgeSelf.exe` 读取）。
- 签名针对 `publish\` 产物文件本身，与运行时端口无关；无论实例跑在哪个端口，签名流程一致。
