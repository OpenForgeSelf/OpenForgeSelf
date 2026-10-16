import { ref, computed, onBeforeUnmount } from 'vue'
import { makeSessionId, makeCode, makeRemoteId, buildPairUrl, clampText, maskCode } from '../utils/pairing'

/**
 * 移动端遥控（v1.8 / Q-10）。
 *
 * 大屏挂在墙上没人敲键盘，这是「遥控」存在的唯一理由。三条取舍：
 *
 *   1. **组件不发明传输通道**。`remote.subscribe(onMessage)` 由宿主注入——
 *      SSE、WebSocket、长轮询、MQTT 都行，组件只要求一个「给我推送消息」的函数。
 *      否则组件就得绑定某一种后端，宿主的网关换一次就要改组件。
 *
 *   2. **传输与策略分离**。中继只搬运，配对码校验 / 限流 / 会话生命周期
 *      全在客户端。这样宿主换通道时，策略逻辑一行都不用改。
 *
 *   3. **遥控不是认证**。配对码只是「看得见屏幕的人才能输入」，不是身份认证。
 *      真要鉴权，宿主用 `remote.authorize(text, meta)` 钩子拒掉——返回 false
 *      或抛错即拒绝。把安全边界说清楚，比假装它安全更有用。
 */
export function useRemote(config, deps = {}) {
  const rc = config.remote || {}
  const audit = deps.audit
  const enabled = !!rc.enabled

  const state = ref(enabled ? 'waiting' : 'off') // off | waiting | paired | expired
  const session = ref(rc.session || '')
  const code = ref(rc.code || '')
  const count = ref(0)
  const expiresAt = ref(0)
  const qrSrc = ref('')

  let ttlTimer = null
  let unsubscribe = null
  let lastAt = 0
  let started = false

  function defaultBase() {
    try {
      if (typeof location !== 'undefined' && location.origin) return location.origin + '/remote'
    } catch (e) {
      /* 非浏览器环境 */
    }
    return '/remote'
  }

  const urlBase = computed(() => rc.urlBase || defaultBase())
  const pairUrl = computed(() => buildPairUrl(urlBase.value, session.value, code.value))
  const info = computed(() => ({
    session: session.value,
    code: code.value,
    url: pairUrl.value,
    expiresAt: expiresAt.value,
  }))

  /** 二维码由宿主生成（组件不背 QR 编码器的体积）：rc.qr 是 (info) => dataURL */
  function applyQr() {
    let v = ''
    try {
      v = typeof rc.qr === 'function' ? rc.qr(info.value) : rc.qrSrc
    } catch (e) {
      v = ''
    }
    qrSrc.value = v || ''
  }

  function armTtl() {
    if (ttlTimer) {
      clearTimeout(ttlTimer)
      ttlTimer = null
    }
    const ttl = Number(rc.ttl) || 0
    if (ttl <= 0) {
      expiresAt.value = 0
      return
    }
    expiresAt.value = Date.now() + ttl
    ttlTimer = setTimeout(() => {
      state.value = 'expired'
      audit?.emit('remote:expire', { session: session.value })
      try {
        rc.onExpire?.()
      } catch (e) {
        /* noop */
      }
    }, ttl)
  }

  function announce() {
    // 审计里只留脱敏后的码：能定位是哪一次配对，但拿不到可用凭证
    audit?.emit('remote:pair', { session: session.value, code: maskCode(code.value), ttl: Number(rc.ttl) || 0 })
  }

  function start() {
    if (!enabled || started) return
    started = true
    if (!session.value) session.value = makeSessionId()
    if (!code.value) code.value = makeCode(Number(rc.codeLength) || 6)
    armTtl()
    applyQr()
    announce()
    try {
      rc.onPair?.(info.value)
    } catch (e) {
      /* 宿主回调异常不影响遥控自身可用 */
    }
    if (typeof rc.subscribe === 'function') {
      try {
        const un = rc.subscribe(receive)
        if (typeof un === 'function') unsubscribe = un
      } catch (e) {
        if (config.debug) console.warn('[ai-assistant] remote.subscribe 注入失败：', e)
      }
    }
  }

  /**
   * 统一出口：每条远端消息都带 id，无论接收还是拒绝都给出裁决。
   * 手机端据此显示「已接收 / 被拒：原因」，而不是无条件显示「已送达」——
   * 旧版那个「已送达」统计的是「中继推给了几块屏」，完全不等于「屏接受了」。
   */
  function settle(id, accepted, reason, msg) {
    const v = { id, accepted, reason: accepted ? null : reason, from: (msg && msg.from) || null }
    audit?.emit('remote:verdict', v)
    try {
      rc.onVerdict?.(v, msg)
    } catch (e) {
      /* 宿主回执钩子异常不能反过来影响遥控准入 */
    }
    if (!accepted && config.debug) console.warn('[ai-assistant] 遥控消息被拒：' + reason)
    return accepted
  }

  /**
   * 接收一条远端输入。返回 true = 已入队。
   * 校验顺序刻意从「便宜且致命」到「昂贵且次要」：
   * 会话 → 状态 → 配对码 → 限流 → 清洗 → 忙 → 宿主授权。
   * 无论放行还是拒绝都走 settle()，给出一条带 id 的裁决。
   */
  function receive(payload) {
    if (!enabled) return false
    const msg = typeof payload === 'string' ? { text: payload } : payload && typeof payload === 'object' ? payload : {}
    const id = msg.id ? String(msg.id) : makeRemoteId()

    const deny = (reason) => settle(id, false, reason, msg)

    if (msg.session && String(msg.session) !== session.value) return deny('session')
    if (state.value === 'expired') return deny('expired')
    if (state.value === 'off') return deny('off')
    if (rc.requireCode !== false && String(msg.code || '') !== code.value) return deny('code')

    const now = Date.now()
    const gap = Number(rc.minIntervalMs) || 0
    if (gap > 0 && lastAt && now - lastAt < gap) return deny('rate')

    const text = clampText(msg.text, Number(rc.maxLength) || 2000)
    if (!text) return deny('empty')

    // 正在生成 / 续传时不接：否则用户话音未落又来一句，会互相打断
    if (typeof deps.isBusy === 'function' && deps.isBusy()) return deny('busy')

    if (typeof rc.authorize === 'function') {
      let ok = false
      try {
        ok = rc.authorize(text, { session: session.value, code: String(msg.code || ''), from: msg.from || null, id }) !== false
      } catch (e) {
        ok = false // 「抛错即拒绝」——安全钩子的失败模式必须更严
      }
      if (!ok) return deny('authorize')
    }

    lastAt = now
    count.value += 1
    state.value = 'paired'
    audit?.emit('remote:message', { id, length: text.length, from: msg.from || null })
    deps.send?.(text, { source: 'remote' })
    return settle(id, true, null, msg)
  }

  /** 轮换配对码：旧码立即失效。怀疑截屏外泄时用 */
  function rotate() {
    code.value = makeCode(Number(rc.codeLength) || 6)
    state.value = 'waiting'
    armTtl()
    applyQr()
    announce()
    try {
      rc.onPair?.(info.value)
    } catch (e) {
      /* noop */
    }
    return info.value
  }

  function unpair() {
    if (ttlTimer) {
      clearTimeout(ttlTimer)
      ttlTimer = null
    }
    if (unsubscribe) {
      try {
        unsubscribe()
      } catch (e) {
        /* noop */
      }
      unsubscribe = null
    }
    state.value = 'off'
    expiresAt.value = 0
  }

  onBeforeUnmount(() => unpair())

  return { enabled, state, session, code, count, expiresAt, pairUrl, info, qrSrc, receive, start, rotate, unpair }
}

export default { useRemote }
