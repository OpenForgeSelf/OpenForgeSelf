import http from 'node:http'

/**
 * Mock 上游 AI Provider —— Playwright E2E 测试专用。
 *
 * 设计目标（对齐 Playwright 官方 API Testing 最佳实践）：
 * - 用 Node 内置 http 模块起一个最小 mock server，模拟 OpenAI 兼容上游
 * - 记录最后一次收到的请求（方法/URL/headers/body），供断言网关行为
 * - 支持非流式 + 流式（SSE）两种响应模式
 * - 监听固定端口 18080，便于后端预配置 provider endpoint 指向它
 *
 * 前置条件（测试组 2 需要）：
 *   后端需预配置一个 provider，其 Endpoint 指向 `http://localhost:18080/v1/chat/completions`。
 *   provider 名通过环境变量 `OPENFORGE_E2E_MOCK_PROVIDER` 传入（如 `e2e-mock`）。
 *
 * 不使用 page.route() 的原因：
 *   page.route() 只能拦截浏览器发出的请求，而 API 网关 → 上游的请求在后端进程内发起，
 *   浏览器拦截不到。官方推荐用本地 mock HTTP server 作为上游（见 Playwright API Testing 文档）。
 */

export interface MockUpstreamRequest {
  method: string
  url: string
  headers: http.IncomingHttpHeaders
  body: any
}

export interface MockUpstream {
  /** 上游 chat completions 完整 URL，如 http://localhost:18080/v1/chat/completions */
  url: string
  port: number
  /** 关闭 server */
  close: () => Promise<void>
  /** 读取最后一次收到的请求；未收到请求返回 null */
  lastRequest: () => MockUpstreamRequest | null
  /** 清空记录的请求，便于 beforeEach 隔离 */
  reset: () => void
}

/**
 * 启动 mock 上游 server。
 * @param port 监听端口，默认 18080（与后端预配置 endpoint 约定一致）
 */
export function startMockUpstream(port = 18080): Promise<MockUpstream> {
  let lastReq: MockUpstreamRequest | null = null

  const server = http.createServer((req, res) => {
    const chunks: Buffer[] = []
    req.on('data', (c: Buffer) => chunks.push(c))
    req.on('end', () => {
      const bodyStr = Buffer.concat(chunks).toString('utf8')
      let body: any = null
      if (bodyStr) {
        try {
          body = JSON.parse(bodyStr)
        } catch {
          body = bodyStr
        }
      }

      lastReq = {
        method: req.method ?? '',
        url: req.url ?? '',
        headers: req.headers,
        body,
      }

      const url = req.url ?? ''
      if (url.includes('/chat/completions')) {
        const isStream = body?.stream === true
        if (isStream) {
          res.writeHead(200, {
            'Content-Type': 'text/event-stream',
            'Cache-Control': 'no-cache',
            Connection: 'keep-alive',
          })
          const model = body?.model ?? 'mock-model'
          const first = {
            id: 'chatcmpl-mock',
            object: 'chat.completion.chunk',
            created: 1,
            model,
            choices: [{ index: 0, delta: { role: 'assistant', content: 'hi' }, finish_reason: null }],
          }
          const stop = {
            id: 'chatcmpl-mock',
            object: 'chat.completion.chunk',
            created: 1,
            model,
            choices: [{ index: 0, delta: {}, finish_reason: 'stop' }],
          }
          res.write(`data: ${JSON.stringify(first)}\n\n`)
          res.write(`data: ${JSON.stringify(stop)}\n\n`)
          // 若客户端要求 include_usage，追加 usage chunk（OpenAI 标准）
          if (body?.stream_options?.include_usage === true) {
            const usage = {
              id: 'chatcmpl-mock',
              object: 'chat.completion.chunk',
              created: 1,
              model,
              choices: [],
              usage: { prompt_tokens: 5, completion_tokens: 2, total_tokens: 7 },
            }
            res.write(`data: ${JSON.stringify(usage)}\n\n`)
          }
          res.write('data: [DONE]\n\n')
          res.end()
        } else {
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end(
            JSON.stringify({
              id: 'chatcmpl-mock',
              object: 'chat.completion',
              created: 1,
              model: body?.model ?? 'mock-model',
              choices: [
                {
                  index: 0,
                  message: { role: 'assistant', content: 'hi' },
                  finish_reason: 'stop',
                },
              ],
              usage: { prompt_tokens: 5, completion_tokens: 2, total_tokens: 7 },
            }),
          )
        }
      } else if (url.includes('/models')) {
        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(
          JSON.stringify({
            object: 'list',
            data: [{ id: 'mock-model', object: 'model', created: 1, owned_by: 'mock' }],
          }),
        )
      } else {
        res.writeHead(404, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({ error: { message: 'Not found', type: 'invalid_request_error' } }))
      }
    })
  })

  return new Promise<MockUpstream>((resolve, reject) => {
    server.on('error', reject)
    server.listen(port, () => {
      resolve({
        url: `http://localhost:${port}/v1/chat/completions`,
        port,
        close: () =>
          new Promise<void>((res, rej) => {
            server.close(err => (err ? rej(err) : res()))
          }),
        lastRequest: () => lastReq,
        reset: () => {
          lastReq = null
        },
      })
    })
  })
}
