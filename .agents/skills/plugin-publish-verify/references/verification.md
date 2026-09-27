# 验证清单：宿主能否访问更新后的插件

前置：宿主已在 `publish/` 运行（端口见 `ForgeSetting.config` 的 `PortNumber`，本环境长期实例 51888）。
⚠ **全部 `/api/plugin*` 请求必须带 `Authorization: Bearer <token>`**（PluginController 类级鉴权，2026-09-24）。
token 用仓内工具：`node scripts/get-forge-token.cjs`（一键解密当前宿主明文 token，勿每次现写探针）。

## 端点速查（前缀是单数 `api/plugin`）

| 用途 | 方法 | 路径 |
|------|------|------|
| 插件列表 | GET | `/api/plugin` |
| 插件详情 | GET | `/api/plugin/{id}` |
| 前端清单 | GET | `/api/plugin/frontend-manifest?id={id}` |
| 插件静态资源 | GET | `/plugins/{id}/web/dist/index.js`（不鉴权，版本化读取） |
| 检查更新 | GET | `/api/plugin/updates` |
| 触发更新 | POST | `/api/plugin/update/{id}` |
| 版本历史 | GET | `/api/plugin/{id}/versions` |
| 回滚 | POST | `/api/plugin/rollback/{id}` |
| 启停 | POST | `/api/plugin/{id}/enable` / `/api/plugin/{id}/disable` |

## 更新成功判定（核心规则）

> **取某个插件接口返回的版本，与「当前插件的清单文件（plugin.json）」里的版本比对；一致即认为更新成功。**

```powershell
$tok = node scripts/get-forge-token.cjs
$h = @{ Authorization = "Bearer $tok" }

# ① 清单文件里的版本（期望值）—— 以版本化快照目录的 plugin.json 为准
$manifestVer = (Get-Content "publish\Plugins\_backups\<id>\<version>\plugin.json" -Raw | ConvertFrom-Json).Version

# ② 接口返回的版本（实际值）
$apiVer = (Invoke-RestMethod "http://localhost:51888/api/plugin" -Headers $h).data |
          Where-Object { $_.id -eq '<id>' } | Select-Object -ExpandProperty version

# ③ 一致 = 更新成功
$success = ($manifestVer -eq $apiVer)
```

- **一致** → 更新成功，宿主已加载新版本。
- **不一致** → 更新未生效（产物没落到位 / 版本号没升 / 插件没被重新发现），排查见 troubleshooting.md。

**为什么以清单文件为准**：版本号唯一真源是 `plugin.json`，宿主扫描后经接口暴露的版本必须回落到同一个值。

## 更新路径：主路径 vs 兜底

| 路径 | 触发方式 | 何时用 |
|------|---------|--------|
| **主路径：版本化显式更新** | `publish-plugin.ps1` stage → `POST /api/plugin/update/{id}`（落 `versions/<new>/` + 切 current + ALC 换载） | **日常首选**（2026-09-24 起）；发布动作不触碰在加载文件 |
| **兜底：界面安装 / 冷启动** | `.forgeself-plugin` 包走 `POST /api/plugin/install`；或停 → 覆盖 → 起宿主 | 新增插件 / 包分发 / 强制冷启动 |

> `PluginHotReloadWatcher` 已**一刀切移除**（2026-09-24 用户拍板）——**没有自动热重载**，
> 改完产物后必须显式走更新接口或冷启动，不要等文件监听。

## 三步验证

### ① 版本已更新（= 上面的「更新成功判定」）

```powershell
$tok = node scripts/get-forge-token.cjs
$j = Invoke-RestMethod "http://localhost:51888/api/plugin" -Headers @{ Authorization = "Bearer $tok" }
($j.data | Where-Object { $_.id -eq '<id>' }).version   # 期望 = 新版本
```

### ② 前端清单正确

```powershell
$e = (Invoke-RestMethod "http://localhost:51888/api/plugin/frontend-manifest?id=<id>" -Headers $h).data |
     Where-Object { $_.id -eq '<id>' }
$e.version          # 期望 = 新版本
$e.frontend.entry   # 期望 = "web/dist/index.js"
```

### ③ 静态资源可达（最关键，证明中间件 + 产物都在）

```powershell
$r = [System.Net.HttpWebRequest]::Create(
       "http://localhost:51888/plugins/<id>/web/dist/index.js")
$r.Timeout = 8000
$resp = $r.GetResponse()
[int]$resp.StatusCode          # 期望 200
$resp.ContentType              # 期望 text/javascript
```

## 通过判据（四条全绿）

- [ ] **接口返回的插件版本 == 版本快照 `plugin.json` 的版本**（更新成功判定，首要）
- [ ] `frontend-manifest` 的 `version` 一致，且 `entry == web/dist/index.js`
- [ ] `GET /plugins/<id>/web/dist/index.js` → **200 + `text/javascript`**
- [ ] 入口 DLL 哈希与 staged 一致（防「版本升了但跑旧二进制」的假成功）：
      `node scripts/probe-dll-string.cjs <dll> <关键字符串>`（UTF-8+UTF-16LE 双检，FOUND→exit 0）

## 排障时多看一眼

- 若版本没变：确认 staged 的是 `_backups/<id>/<新版本>/`，且 `plugin.json` 的 `Version` 确实升了
- 若资源 404：确认版本快照 `versions/<current>/web/dist/index.js` 真实存在（版本化读取生效）
- 若 401：token 用 `scripts/get-forge-token.cjs` 重取（宿主重启会轮换，旧 token 失效）
