/**
 * 审计日志（v1.6）—— 谁、何时、问了什么、调了哪些工具、批了哪次危险操作。
 *
 * 合规场景（金融 / 政企）要的是「事后能复盘」，所以四条约束：
 *   1. **默认关闭**。开着必带性能与隐私成本，必须由宿主显式决定。
 *   2. **只留内存 + 回调，不落 localStorage**。审计数据属于服务端；
 *      留在浏览器里既可能被本地篡改，也会在大屏换班次后把上一个人的
 *      操作记录暴露给下一个人（与 ROADMAP 里「持久化策略分离」同一条理由）。
 *   3. **提供 redact 钩子**。审计要留内容，但内容里可能有手机号、密钥；
 *      脱敏规则必须由最懂业务的宿主来定，组件不擅自猜——猜错等于把审计变成泄密渠道。
 *   4. **审批结论单独记一条** approval:decide。这是合规最看重的事件：
 *      谁批了哪次危险操作、批还是拒、什么时候。
 */

export const AUDIT_TYPES = [
  'session:init',      // 实例挂载
  'message:user',      // 用户提问
  'message:assistant', // 助手回复
  'tool:call',         // 工具调用（含危险写操作）
  'approval:request',  // 危险操作请求批准
  'approval:decide',   // 审批结论（★ 合规关键事件）
  'approval:blocked',  // 权限不足，拒绝放行（★ 与门禁同源）
  'approval:expire',   // 到点自动作废
  'refresh',           // 大屏自动刷新一拍
  'refresh:skip',      // FR-1208：本拍被跳过（正在生成 / 续传）
  'error',             // 请求失败
  'fallback',          // 降级为本地模拟
  'remote:pair',       // 遥控配对建立 / 配对码轮换（码已脱敏）
  'remote:message',    // 远端送来的输入
  'remote:verdict',    // 每条远端输入的裁决（accepted + reason；放行与拒绝都记）
  'remote:expire',     // 配对码到期
]

export function createAudit(config = {}) {
  const cfg = config.audit
  const enabled = !!cfg && cfg !== false
  const opts = cfg && typeof cfg === 'object' ? cfg : {}
  const max = Math.max(1, Number(opts.max) || 500)
  const redact = typeof opts.redact === 'function' ? opts.redact : null
  const events = []
  let seq = 0

  function actor() {
    const a = config.actor
    if (!a) return { id: null, name: null, role: config.role || null }
    if (typeof a === 'string') return { id: a, name: a, role: config.role || null }
    return { id: a.id ?? null, name: a.name ?? null, role: config.role || null }
  }

  function emit(type, payload = {}) {
    if (!enabled) return null
    let body = payload
    if (redact) {
      try {
        body = redact(type, payload) ?? payload
      } catch (e) {
        // 脱敏钩子自己抛错时，用占位替换整条 payload——宁可少记，也不把原文漏出去
        body = { _redactError: String((e && e.message) || e) }
      }
    }
    const ev = {
      seq: ++seq,
      at: Date.now(),
      type,
      instance: config.instanceKey || 'default',
      actor: actor(),
      ...body,
    }
    events.push(ev)
    if (events.length > max) events.splice(0, events.length - max)
    try {
      config.onAudit?.(ev, events.length)
    } catch (e) {
      /* 宿主审计回调异常不能反过来影响对话主流程 */
    }
    return ev
  }

  return {
    enabled,
    emit,
    list: () => events.slice(),
    count: () => events.length,
    clear: () => {
      events.length = 0
      return true
    },
    export: () => JSON.stringify(events, null, 2),
  }
}

/** 递归扫描块，把工具调用与审批请求记进审计 */
export function auditBlocks(audit, blocks, depth = 0) {
  if (!audit || !audit.enabled || depth > 8) return
  for (const b of blocks || []) {
    if (!b || typeof b !== 'object') continue
    if (b.type === 'tool') {
      audit.emit('tool:call', {
        tool: b.name || null,
        status: b.status || 'ok',
        dangerous: !!b.dangerous,
        durationMs: Number.isFinite(b.durationMs) ? b.durationMs : null,
      })
    } else if (b.type === 'approval') {
      audit.emit('approval:request', {
        id: b.id ?? null,
        action: (b.action && b.action.name) || null,
        risk: b.risk || 'medium',
        status: b.status || 'pending',
        requireConfirmText: b.requireConfirmText ? true : false, // 只记「有没有门禁」，不记门禁词本身
      })
    } else if (b.type === 'group' && Array.isArray(b.blocks)) {
      auditBlocks(audit, b.blocks, depth + 1)
    }
  }
}

export default { createAudit, auditBlocks, AUDIT_TYPES }
