#!/usr/bin/env node
// mock-mcp-server.js — 测试用最小 MCP 服务器（三传输：stdio / streamable-http / http-sse）
// 用法：
//   node mock-mcp-server.js --mode stdio
//   node mock-mcp-server.js --mode stdio-ndjson       # 换行分隔 JSON 帧（官方 TS/Python SDK 实况，PC-009 仿真）
//   node mock-mcp-server.js --mode http --port 0        # 随机端口，打印 "LISTENING <port>"
//   node mock-mcp-server.js --mode sse --port 0         # 旧版双端点（GET /sse 发现 endpoint → POST /mcp）
// 支持工具：echo(text) / add(a,b)；initialize/ping/tools/list/tools/call。
// --with-slow 额外暴露 slow(ms) 工具（延时后才应答，v2.5.2 执行超时可配置 e2e 专用）；
// 默认不开启，保持既有工具数断言（toolCount=2）不变。

const http = require('http');

const PROTOCOL_VERSIONS = ['2025-11-25', '2025-06-18', '2025-03-26', '2024-11-05'];

const TOOLS = [
  {
    name: 'echo',
    description: '回显传入的 text 字段',
    inputSchema: { type: 'object', properties: { text: { type: 'string' } }, required: ['text'] }
  },
  {
    name: 'add',
    description: '两个数字相加',
    inputSchema: { type: 'object', properties: { a: { type: 'number' }, b: { type: 'number' } }, required: ['a', 'b'] }
  }
];

// --with-slow：v2.5.2 执行超时 e2e 的慢工具（按参数 ms 延时后才回响应）
const WITH_SLOW = process.argv.includes('--with-slow');
if (WITH_SLOW) {
  TOOLS.push({
    name: 'slow',
    description: '延时 ms 毫秒后返回（测试执行超时用）',
    inputSchema: { type: 'object', properties: { ms: { type: 'number' } }, required: ['ms'] }
  });
}

/** 本次请求需延迟应答的毫秒数（仅 slow 工具；上限 120s 防挂死）。 */
function callDelayMs(req) {
  return req.method === 'tools/call' && req.params && req.params.name === 'slow'
    ? Math.min(Number(req.params.arguments && req.params.arguments.ms) || 0, 120000) : 0;
}

function handleRequest(req, json) {
  const id = req.id !== undefined ? req.id : null;
  const method = req.method;
  try {
    switch (method) {
      case 'initialize':
        return { jsonrpc: '2.0', id, result: {
          protocolVersion: PROTOCOL_VERSIONS.includes(req.params && req.params.protocolVersion)
            ? req.params.protocolVersion : PROTOCOL_VERSIONS[0],
          capabilities: { tools: { listChanged: false } },
          serverInfo: { name: 'MockMcpServer', version: '1.0.0' }
        }};
      case 'ping':
        return { jsonrpc: '2.0', id, result: {} };
      case 'tools/list':
        return { jsonrpc: '2.0', id, result: { tools: TOOLS } };
      case 'tools/call': {
        const name = req.params && req.params.name;
        const args = (req.params && req.params.arguments) || {};
        if (name === 'echo') {
          return { jsonrpc: '2.0', id, result: { content: [{ type: 'text', text: JSON.stringify({ echo: args.text || '' }) }], isError: false } };
        }
        if (name === 'add') {
          const sum = Number(args.a) + Number(args.b);
          return { jsonrpc: '2.0', id, result: { content: [{ type: 'text', text: JSON.stringify({ sum }) }], isError: false } };
        }
        if (name === 'slow') {
          return { jsonrpc: '2.0', id, result: { content: [{ type: 'text', text: JSON.stringify({ slept: Number(args.ms) || 0, ok: true }) }], isError: false } };
        }
        return { jsonrpc: '2.0', id, error: { code: -32602, message: `unknown tool: ${name}` } };
      }
      default:
        return { jsonrpc: '2.0', id, error: { code: -32601, message: `Method not found: ${method}` } };
    }
  } catch (e) {
    return { jsonrpc: '2.0', id, error: { code: -32603, message: String(e && e.message || e) } };
  }
}

// ---------- stdio ----------
function runStdio() {
  let buf = '';
  process.stdin.setEncoding('utf8');
  process.stdin.on('data', (chunk) => {
    buf += chunk;
    while (true) {
      const idx = buf.indexOf('\r\n\r\n');
      if (idx < 0) return;
      const header = buf.slice(0, idx);
      const m = /Content-Length:\s*(\d+)/i.exec(header);
      if (!m) { buf = buf.slice(idx + 4); continue; }
      const len = parseInt(m[1], 10);
      if (buf.length < idx + 4 + len) return;
      const body = buf.slice(idx + 4, idx + 4 + len);
      buf = buf.slice(idx + 4 + len);
      let req;
      try { req = JSON.parse(body); } catch { continue; }
      const resp = handleRequest(req, body);
      if (req.id !== undefined) {
        const d = callDelayMs(req);
        if (d) setTimeout(() => writeFrame(resp), d); else writeFrame(resp);
      }
    }
  });
  function writeFrame(obj) {
    const json = JSON.stringify(obj);
    const bytes = Buffer.byteLength(json, 'utf8');
    process.stdout.write(`Content-Length: ${bytes}\r\n\r\n${json}`);
  }
}

// ---------- stdio（NDJSON：换行分隔，仿官方 TS/Python SDK） ----------
function runStdioNdjson() {
  let buf = '';
  process.stdin.setEncoding('utf8');
  process.stdin.on('data', (chunk) => {
    buf += chunk;
    let i;
    while ((i = buf.indexOf('\n')) >= 0) {
      const line = buf.slice(0, i).trim();
      buf = buf.slice(i + 1);
      if (!line) continue;
      let req;
      try { req = JSON.parse(line); } catch { continue; }
      const resp = handleRequest(req, line);
      if (req.id !== undefined) {
        const d = callDelayMs(req);
        const w = () => process.stdout.write(JSON.stringify(resp) + '\n');
        if (d) setTimeout(w, d); else w();
      }
    }
  });
}

// ---------- HTTP helpers ----------
function sseFrame(event, data) {
  return `event: ${event}\ndata: ${data}\n\n`;
}

function readBody(req, cb) {
  let data = '';
  req.on('data', (c) => { data += c; });
  req.on('end', () => cb(data));
}

// ---------- streamable-http / http-sse ----------
function runHttp(mode) {
  let sseClients = new Set();
  const server = http.createServer((req, res) => {
    if (mode === 'sse' && req.url === '/sse' && req.method === 'GET') {
      // 旧版：GET /sse → endpoint 事件 + 保持连接；POST /mcp 的响应经此流回传
      res.writeHead(200, { 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-cache' });
      res.write(sseFrame('endpoint', 'http://127.0.0.1:' + server.address().port + '/mcp'));
      sseClients.add(res);
      req.on('close', () => sseClients.delete(res));
      return;
    }
    if (req.url !== '/mcp') { res.writeHead(404); res.end('not found'); return; }

    if (req.method === 'GET') {
      // Streamable HTTP 的 GET 流（保持连接；收到请求即回心跳）
      res.writeHead(200, { 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-cache' });
      res.write(sseFrame('message', JSON.stringify({ jsonrpc: '2.0', id: null, result: {} })));
      const keep = setInterval(() => res.write(': keepalive\n\n'), 15000);
      req.on('close', () => clearInterval(keep));
      return;
    }

    if (req.method === 'POST') {
      readBody(req, (body) => {
        let reqObj;
        try { reqObj = JSON.parse(body); } catch {
          res.writeHead(400); res.end('bad json'); return;
        }
        const resp = handleRequest(reqObj);
        const deliver = () => {
        if (mode === 'sse') {
          // 旧版：POST 202 空体；响应经已建立的 SSE 流按 id 回传
          res.writeHead(202, { 'Content-Type': 'text/plain' });
          res.end();
          if (reqObj.id !== undefined) {
            for (const client of sseClients) {
              client.write(sseFrame('message', JSON.stringify(resp)));
            }
          }
        } else {
          // streamable-http：JSON 响应（支持 application/json；带 Accept SSE 也回 JSON 可被客户端解析）
          const accept = req.headers.accept || '';
          if (accept.includes('text/event-stream')) {
            res.writeHead(200, { 'Content-Type': 'text/event-stream' });
            res.end(sseFrame('message', JSON.stringify(resp)));
          } else {
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify(resp));
          }
        }
        };
        const d = callDelayMs(reqObj);
        if (d) setTimeout(deliver, d); else deliver();
      });
      return;
    }
    res.writeHead(405); res.end();
  });

  server.listen(0, '127.0.0.1', () => {
    console.log(`LISTENING ${server.address().port}`);
  });
}

const args = process.argv.slice(2);
const mode = args.includes('--mode') ? args[args.indexOf('--mode') + 1] : 'stdio';
if (mode === 'stdio') runStdio();
else if (mode === 'stdio-ndjson') runStdioNdjson();
else runHttp(mode);
