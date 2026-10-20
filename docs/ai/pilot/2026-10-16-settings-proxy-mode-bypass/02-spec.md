# Specification

> 任务：2026-10-16-settings-proxy-mode-bypass ｜ 演进自 042 功能（`docs/02-features/042-network-proxy-settings.md`）

## Functional Requirements

- **FR-1 代理三模式**：`ProxyMode` 三值 `system`（跟随系统代理，默认）/ `manual`（手动配置）/ `direct`（直连·不走任何代理）。手动=现行为（指定代理转发）；直连=GetProxy 恒 null；跟随系统=委托 .NET 系统代理对象（含其自带旁路）。
- **FR-2 旁路条目**：`ProxyBypass` 多行文本（每行一条），`manual` 与 `system` 模式下均先于其他判定生效：目标 host 命中任一条目 → 直连。回环永不走代理的既有规则保留（兜底，无需写条目）。
- **FR-3 测试代理**：`POST /api/settings/test-proxy` 接收**未保存的表单值**（host/port/username/password/url 可选），经该代理向探测目标发一次性 GET，返回 `{success, statusCode, latencyMs, message}`；配置不落盘、密码不写日志、不进任何缓存。前端在地址行右侧提供「测试」按钮，pending 态禁用防重复，行内展示成败反馈。
- **FR-4 条目编辑**：面板内「不走代理的条目」列表，一行一条，可添加可删除，随「保存」统一落盘（点即保存语义不变）。
- **FR-5 契约更新**：GET/POST `/api/settings` 增 `proxyMode`、`proxyBypass`（DTO 为字符串数组）；**移除 `proxyEnabled`**——上一批未发布未提交，无既有用户配置，直接以 `proxyMode` 替代（`manual`≈旧 true，`direct`≈旧 false，默认变为 `system`=旧 .NET 原生行为）。

## Input / Output

- 配置字段（ForgeSetting，XML 落盘）：`ProxyMode`（String，默认 `"system"`，合法值同 FR-1）、`ProxyBypass`（String，`\n` 分隔，默认空）。其余五字段（Host/Port/Username/PasswordCipher）原样保留。
- `POST /api/settings/test-proxy` 入参：`{proxyHost, proxyPort, proxyUsername?, proxyPassword?, proxyTestUrl?}`；出参信封 `{success, data:{reachable, statusCode?, latencyMs, message}}`。探测默认目标 = `https://www.baidu.com`（国内可达性实测优先；gstatic 在墙内不可达故不作默认——决策记入 06）。
- `proxyTestUrl` 提供时须为 `http(s)://` 绝对地址（Regex 白名单，同 BR-5 精神）。

## Business Rules

- **BR-1 决策顺序**（ModelProxy.GetProxy/IsBypassed）：① 目标 host 命中旁路条目 → 直连；② `direct` → 直连；③ `manual` → 校验 host/port（无效 → 直连 + Warn 留痕，同上一批兜底）后返回 `http://{host}:{port}`；④ `system` → 委托 `WebRequest.GetSystemWebProxy()` 的 GetProxy/IsBypassed；取不到或抛异常 → 直连 + Warn。
- **BR-2 旁路匹配语义**（大小写不敏感、trim、空行忽略）：`*.example.com` → 后缀匹配（含 example.com 本身）；不带星号的域名 → 同样后缀匹配（NO_PROXY 惯例）；IPv4 精确 → host 相等；`10.*` / `192.168.*` 段前缀 → host 以 `10.` 开头即匹配；其他形态保存时拒绝。
- **BR-3 保存即生效**：模式/旁路与上一批同——每次回调实时读 `ForgeSetting.Current`，无需重启。
- **BR-4 测试端点隔离**：test-proxy 用临时 HttpClient（显式指定入参代理），不受当前已保存配置影响，也不修改任何配置；超时 10s；返回的 message 供 UI 直显（错误原文透出）。
- **BR-5 校验**（POST /api/settings）：`proxyMode` 非法值 → 400；`manual` 时地址/端口沿用既有规则；`proxyBypass` 每条 ≤128 字符、条数 ≤50、字符集 `^[A-Za-z0-9.*_:-]+$`，违规 400 并带行号；密码三语义不变。

## Boundary Conditions / Error Handling

- 跟随系统模式下系统未配置代理 → 等效直连（系统代理对象自然行为，非错误）。
- 测试按钮：host/port 未填 → 前端拦截行内提示「请先填写地址和端口」，不发请求；代理不可达 → 红字错误原文（如连接被拒/超时）；HTTP 4xx/5xx **也算代理可用**（转发成功），反馈文案区分「代理可用（上游返回 {code}）」与网络层失败——对齐输入6 排障经验（502=请求已达代理）。
- 后端 400：错误原文透出、用户输入保留（同上一批）。
- 未知代理模式值（手改配置文件）→ 按 `system` 处理 + Warn（运行期兜底，不落盘不改写）。

## Compatibility / Unknown

- 配置兼容：XML 旧文件里的 `ProxyEnabled` 残留字段解析时被忽略、下次保存不再写出——无迁移逻辑（未发布，无存量）。
- **Unknown（实现期实测钉死）**：① `WebRequest.GetSystemWebProxy()` 在 Win10/11 .NET 10 是否携带 ProxyOverride 旁路（影响 `system` 模式文案承诺——若不带，面板说明改为「跟随系统代理地址，旁路请用下方条目」）；② HTTPS 目标在转发代理下走 CONNECT，旁路条目对 https 目标同样按 host 匹配（预期成立，e2e/现场验证一条）。
- Non-functional：无新依赖；测试端点不产生持久副作用；UI 遵循 `--el-*` + 既有面板行式布局（CRAP-Repetition）。

## Interaction Design（交互设计 · 必写节）

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果 | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 模式选择 | 点 跟随系统/手动/直连 任一 | 即时切换表单区：系统→只读说明「使用 Windows 系统代理」+旁路条目区；手动→地址/端口/认证/测试/旁路/保存区；直连→说明「不发任何代理」（无旁路区，因全部直连旁路无意义） | 单选，当前项高亮 | 无 toast（纯本地态，保存才落盘） | — | 切换不清空已填表单值 |
| 测试按钮 | 手动模式下点「测试」 | host/port 已填→请求中：按钮 loading+禁用；完成：行内结果「✓ 代理可用 · HTTP 200 · 128ms」或「✗ 错误原文」 | idle→testing→success/fail | 行内文案（图标+文字，不单靠颜色）；改任意输入或切模式 → 结果重置回 idle | 未填地址端口→行内提示且不发请求 | 10s 超时按失败展示 |
| 探测地址（可选） | 测试按钮同行小输入 | 填了则测该地址，空=内置探针（www.baidu.com） | 占位文案说明默认 | 结果文案含实际探测 URL 域名 | placeholder「默认 https://www.baidu.com」 | 非法 scheme 后端 400 原文回显 |
| 添加条目 | 点「+ 添加条目」 | 列表尾部出现新空输入行并聚焦 | 行=input+删除图标按钮(aria-label) | — | 空态：说明文案「命中条目的地址将不走代理，如 `*.example.com`、`10.*`」+添加按钮 | ≥50 条时按钮禁用并说明原因 |
| 删除条目 | 点行尾删除 | 该行即移除 | — | — | 删空回空态 | 删除只改待保存态，未保存可切走丢弃；生效需点保存（非即时不可逆→免二次确认） |
| 保存 | 点「保存」 | 校验（前端拦→行内红字常驻占位；后端 400→原文）→成功 toast「网络设置已保存，模型请求即刻生效」+服务端回显 | 按钮 loading | 同上一批 | — | 三模式均可保存；旁路在直连模式保存无副作用（永不命中） |

### 交互验收标准（并入 AC，逐条可测）

- 单测锁：模式切换渲染区正确、测试三态（拦截/loading/成败文案）、条目增删与保存 payload、结果重置；e2e 锁：手动+非法地址测试 → 红字出现且可见错误文本。
- CRAP 自查：三模式控件与既有开关同区同高（Repetition）；旁路区在 manual/system 显示、direct 隐藏（亲密：只展示生效的配置）；说明文字与 PageHead 对齐链不变（Alignment）；模式当前项 vs 非当前项对比明确（Contrast）。错误行沿用 h-4 常驻占位（防吞点击回归）。

## Acceptance Criteria

- [ ] AC-1 `manual` 且未命中旁路 → GetProxy 返回 `http://host:port`；命中旁路/回环 → null（单测矩阵 Verified）
- [ ] AC-2 `direct` 恒 null；`system` 委托系统代理对象（委托行为有单测；兜底 Warn 路径有单测）
- [ ] AC-3 旁路匹配规则五形态（`*.dom`/裸域名/IPv4/`10.*`/非法拒绝）单测全覆盖，含大小写与子域
- [ ] AC-4 GET/POST 契约含 proxyMode/proxyBypass、无 proxyEnabled；非法值 400 文案带原因；旁路违规带行号
- [ ] AC-5 test-proxy：不可达代理→reachable=false+错误原文；经可用代理（现场 10808）→reachable=true+statusCode+latency；配置无副作用、日志无明文密码
- [ ] AC-6 现场：`manual`+10808+旁路 `gpustack.luxvisions-inno.com` → provider test 走直连（上游 401/正常回包，不再 502）——输入6 场景闭环
- [ ] AC-7 前端 `pnpm check` 0 error、`pnpm run test` 全绿；后端 build 0 error、本域测试全绿无假绿（旧 proxyEnabled 断言同步改造）
- [ ] AC-8 e2e settings-network.spec 更新后全绿（模式切换/旁路增删/测试反馈），真实前后端零 mock
