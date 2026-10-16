/* ------------------------------------------------------------------
   构建期注入的环境配置。

   由 vite.config.js 的 `define: { __AI_ENV__: ... }` 在打包时整体替换，
   值来自项目根目录的 .env.* 文件（VITE_ 前缀的变量）。
   这样切换环境只需要改 .env 文件，不用动任何业务代码。

   未注入时（例如直接跑源码、单元测试）退回下面的默认值。
   ------------------------------------------------------------------ */
const fallback = {
  mode: 'mock', // 'mock' = 前端本地模拟；'server' = 真实 HTTP
  baseUrl: '',
  apiPath: '/api/chat',
  apiKey: '',
  model: 'gpt-4o-mini',
}

export const env = typeof __AI_ENV__ !== 'undefined' && __AI_ENV__ ? __AI_ENV__ : fallback

export default env
