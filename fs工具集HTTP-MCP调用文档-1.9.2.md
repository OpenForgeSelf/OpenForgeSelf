================================================================================
fs 工具集 HTTP/MCP 调用文档
端点: http://127.0.0.1:18890/mcp
版本: 2026-10-19 16:05 (Asia/Shanghai)   对应工具实现: AIAgent 1.9.2（宿主 2.4.13）
标注说明: 【实测】=真请求抓包原文；【schema】=服务端 tools/list 返回的定义原文；
       【待验】=新参数（end_line 相关）成功路径，需服务端更新到 AIAgent 1.9.2
       并重绑项目目录后补测；契约描述以 1.9.2 实现源为准
================================================================================

第 0 节 · 调用链总览（只有两层，没有第三层）
  HTTP POST /mcp
    └─ JSON-RPC 层: method=tools/call, params.name 只能是 "universal_tool"
         └─ 工具层: arguments.tool = read_file / write_file / run_terminal_command / ...
              └─ arguments.parameters = 具体工具的参数对象

--------------------------------------------------------------------------------
第 1 节 · 通用约定【实测】
  方法      : POST（GET 请求无需探测，全部调用走 POST）
  必带头    : Content-Type: application/json
  建议头    : Accept: application/json, text/event-stream
              （实测只发 Content-Type、或两个都发，服务端都正常响应）
  免握手    : 无需 initialize、无需会话；响应不含 mcp-session-id（无状态）【实测】
  响应格式  : 恒为 SSE 帧 text/event-stream（即使只返回一条）【实测】
  SSE 解析  : 逐行找 "data:" 前缀行，取其后文本 = JSON-RPC 应答；
              本服务单请求单帧，无需处理多帧拼接【实测】
  id 规则   : 请求 id（数字）原样回显在应答，调用方自行递增即可

--------------------------------------------------------------------------------
第 2 节 · 完整请求示例（原样可复制，一层不多一层不少）

—— 2.1 读文件 read_file ——（end_line 选填，缺省读到文末）
POST http://127.0.0.1:18890/mcp
Content-Type: application/json
Accept: application/json, text/event-stream

{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"universal_tool","arguments":{"tool":"read_file","parameters":{"path":"README.md","start_line":1}}}}

区间读取（第 12~40 行）：
{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"universal_tool","arguments":{"tool":"read_file","parameters":{"path":"src/main.py","start_line":12,"end_line":40}}}}

—— 2.2 写文件 write_file ——（start_line 与 end_line 均必填，区间替换）
POST http://127.0.0.1:18890/mcp
Content-Type: application/json
Accept: application/json, text/event-stream

{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"universal_tool","arguments":{"tool":"write_file","parameters":{"path":".temp/demo.txt","content":"第一行\n第二行\n","start_line":1,"end_line":1}}}}
（本例为新建文件：start_line=1、end_line=1）

区间替换示例（用 content 替换第 10~12 行，其余行原样保留）：
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"universal_tool","arguments":{"tool":"write_file","parameters":{"path":"src/main.py","content":"...","start_line":10,"end_line":12}}}}

注意 JSON 转义两处：\n 是内容换行符（两个字符：反斜杠+n）；
字符串里的双引号写成 \"。

—— 2.3 执行命令 run_terminal_command ——
POST http://127.0.0.1:18890/mcp
Content-Type: application/json
Accept: application/json, text/event-stream

{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"universal_tool","arguments":{"tool":"run_terminal_command","parameters":{"command":"pwsh -NoProfile -Command \"(Get-Item README.md).Length\""}}}}

命令里的双引号也要 JSON 转义成 \"。

—— 2.4 工具发现（对接前先跑这个对参数） ——
{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"universal_tool","arguments":{"tool":"list_tools","parameters":{"keyword":"read_file"}}}}
MCP 层清单（只暴露 universal_tool 一个）：
{"jsonrpc":"2.0","id":5,"method":"tools/list","params":{}}

--------------------------------------------------------------------------------
第 3 节 · 响应解析（全部三种形态，逐字实测）

—— 3.1 成功【实测原文】——
外层（JSON-RPC result，写入业务正确时 isError 恒为 false）：
{"result":{"content":[{"type":"text","text":"<这里是字符串>"}],"isError":false},"id":1,"jsonrpc":"2.0"}

关键：真正的业务结果是 content[0].text —— 一个 JSON 字符串，必须二次解析：
  JSON.parse(response.result.content[0].text)

—— 3.2 内层业务失败（HTTP 200 + isError:false + 内层 success:false）【实测原文】——
外层同 3.1；二次解析后得到：
{"success":false,"error":"尚未选择项目目录，请在左侧选择工作目录",
 "hint":"set directory via POST api/project/directory 或设置 FORGESELF_DEFAULT_PROJECT_ROOT",
 "usedRoot":null}
路径越界同形：{"success":false,"error":"拒绝访问项目目录之外的路径",
 "hint":"路径受当前项目根约束：PROJECT_ROOT=D:\\src\\proj\\MindHub", ...}

—— 3.3 参数校验失败【实测原文，2026-10-19 新增形态】——
外层同 3.1（isError:false）；二次解析后得到（注意大写开头的信封，与 3.2 并存）：
{"Success":false,"Data":null,
 "Error":{"Code":"INVALID_PARAMETERS",
          "Message":"参数验证失败: 缺少必填参数: start_line",
          "Suggestion":"请检查参数是否符合要求"},
 "DurationMs":0}
AIAgent 1.9.2 起 write_file 的 start_line 与 end_line 同在 schema required 里，
缺任何一个都会以同形报「缺少必填参数: start_line / end_line」【待验】。
行号语义校验（非正整数、end<start、start 越界）走的是 3.2 那套小写业务信封，
工具内层还有一道，不要只依赖 schema 校验。

—— 3.4 MCP 层错误（外层就是 error）【实测原文】——
裸传 read_file 当 MCP 工具名时：
{"error":{"code":-32602,"message":"Unknown tool: 'read_file'"},"id":2,"jsonrpc":"2.0"}
判别顺序：先看有无 result，再看有无 error，最后才二次解析 content[0].text。

--------------------------------------------------------------------------------
第 4 节 · read_file 详细契约（AIAgent 1.9.2）

参数（parameters 对象内）：
  path        string  必填  相对项目根的文件路径（如 "src/main.py"）
  start_line  int     必填  起始行号，从 1 起计；缺失或非正整数直接报错【实测】
  end_line    int     选填  结束行号（含该行）；缺省读到文末；
                            不得小于 start_line（小于则报错）；
                            超出总行数自动钳到文末（不报错）【schema】

行为【schema】：
  返回 start_line..end_line 区间的行内容，同时返回整个文件的总行数 totalLines。
  行号口径：文本按 CRLF/CR 归一为 LF 后切分；文件末尾若带换行，
  末尾会多出一个空段（计入 totalLines，content 尾部会因此多一个 \n）。
  start_line 超出 totalLines → content 为空串（仍返回 totalLines，便于自纠）。
  示例：{"path":"src/main.py","start_line":12,"end_line":40} → 读第 12~40 行。

成功返回（内层 text 解析后；字段以 1.9.2 实现源为准，本实例未更新故【待验】）：
  {"success":true,"path":"...","startLine":12,"endLine":40,
   "content":"<startLine..生效endLine>",
   "totalLines":<int>,"usedRoot":"..."}
  新增/变化字段：startLine（回显）、endLine（钳后生效结束行）、totalLines。

注意：当前服务端项目目录未绑定（usedRoot:null）时所有文件调用返回 3.2 的
「尚未选择项目目录」借误；绑定后恢复。

--------------------------------------------------------------------------------
第 5 节 · write_file 详细契约（AIAgent 1.9.2 · 先读后写必填）

参数：
  path        string  必填  相对项目根；父目录不存在自动创建【schema】
  content     string  必填  新内容（UTF-8 文本）
  start_line  int     必填  起始行号（从 1 起）；缺失/非正整数直接报错【实测】
  end_line    int     必填  结束行号（含该行）；不得小于 start_line；
                            超出已有行数自动钳到文末【schema】

行为【schema】：
  用 content 整段替换 start_line..end_line 区间的行；区间外一律原样保留
  （前 start_line-1 行保留，end_line 之后的尾部行保留）。
  · 整文件覆盖 = start_line=1 且 end_line≥文末（推荐传 read 返回的 totalLines）
  · 新建文件（不存在）：start_line 必须为 1（否则报错）；end_line 钳后无实际作用
  · start_line > 已有行数+1 报错；start_line = 已有行数+1 = 追加到文末
  · end_line < start_line 报错
  示例：{"path":"src/main.py","start_line":10,"end_line":12,"content":"..."}
  → 只替换第 10~12 行；content 行数可多于/少于原区间，总行数随之增减。

风险提示（对接方复核义务）：
  先读后写是契约——必须先 read_file 拿到真实行号（content+totalLines）再写，
  不可凭记忆填行号。end_line 被钳到文末时区间会变小，超出区间的旧尾部行
  不会被删除——需要全量重写时把 end_line 传成 totalLines。

成功返回（内层 text 解析后；字段以 1.9.2 实现源为准，【待验】）：
  {"success":true,"path":"...","startLine":10,"endLine":12,
   "writtenLength":<写入后整个文件的字符数>,
   "totalLines":<写入后新文件总行数>,"usedRoot":"..."}
  字段变化：旧 contentLength（所传 content 大小）→ writtenLength（整个落盘文件）；
  新增 endLine、totalLines。

--------------------------------------------------------------------------------
第 6 节 · run_terminal_command（本次未变更，更新前全流程实测）

参数：command（必填）、cwd（可选，默认项目根）、timeoutSeconds（可选）
限制：首 token ∈ {dotnet,pnpm,node,git,ssh,pwsh}；管道与链式（| ; && > 换行）拒绝；
      cwd 必须在项目根内；exitCode≠0 是正常回传（stdout/stderr 照常返回），
      不要当协议错误处理【实测】。
成功返回（内层解析后）：
  {"success":true,"command":"<原样>","cwd":"D:\\src\\proj\\MindHub","exitCode":0,
   "stdout":"15\r\n","stderr":"","stdoutTruncated":false,"stderrTruncated":false,
   "durationMs":1141}

--------------------------------------------------------------------------------
第 7 节 · 调用方代码样例

curl（Windows cmd / macOS Linux 通用）：
  curl -X POST http://127.0.0.1:18890/mcp ^
       -H "Content-Type: application/json" ^
       -H "Accept: application/json, text/event-stream" ^
       -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"universal_tool\",\"arguments\":{\"tool\":\"write_file\",\"parameters\":{\"path\":\".temp/demo.txt\",\"content\":\"第一行\\n第二行\",\"start_line\":1,\"end_line\":1}}}}"

C# (HttpClient)：
  var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:18890") };
  var body = new {
      jsonrpc = "2.0", id = 1, method = "tools/call",
      @params = new {
          name = "universal_tool",
          arguments = new {
              tool = "write_file",
              parameters = new { path = ".temp/demo.txt", content = "第一行\n第二行", start_line = 1, end_line = 1 }
          }
      }
  };
  var resp = await http.PostAsync("/mcp",
      new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
  // 1) 解析 SSE：取以 "data:" 开头行的余文
  // 2) 反序列化为 JsonRpcResult，取 result.content[0].text
  // 3) 再次 JSON.parse(text) 得业务对象；先看 success/Success 字段判成败

--------------------------------------------------------------------------------
第 8 节 · 对接方 Checklist
  [ ] 解析 SSE data: 帧（不要把整个响应体当 JSON）
  [ ] 双层解析：JSON-RPC result → content[0].text → JSON.parse
  [ ] 不传 initialize，直接 tools/call
  [ ] MCP 层只认识 universal_tool，其他名字通吃 -32602
  [ ] 两套失败信封并存：Success/INVALID_PARAMETERS（校验）与 success/error（业务）
  [ ] read_file：start_line 必填（从 1 起）；end_line 选填（缺省=读到文末）
  [ ] write_file：start_line 与 end_line 均必填（1.9.2），缺则 INVALID_PARAMETERS
  [ ] 先读后写：read_file 拿 content+totalLines 后再定 start_line/end_line
  [ ] 整文件重写：start_line=1、end_line=totalLines
  [ ] write 返回字段为 writtenLength/totalLines（不再是旧 contentLength），
      解析层在服务端更新后切换
  [ ] 处理「尚未选择项目目录」（usedRoot:null）为可重试环境型错误
================================================================================
