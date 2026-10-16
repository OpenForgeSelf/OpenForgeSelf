/**
 * 权限分级（v1.6）—— 按角色决定「哪些内容/操作对这个用户可见」。
 *
 * 三条设计原则：
 *   1. **默认不启用**。role 不传时一切照旧可见，否则升级组件会突然把老用户的
 *      内容全锁掉——那是事故，不是特性。
 *   2. **拒绝时不能泄数据**。被拦下的块只渲染占位，数据绝不进 DOM。
 *      计算属性只有走到对应分支才会求值，所以门禁判断必须是模板的第一个分支。
 *   3. **未知角色名 = 拒绝**。配错角色名时宁可挡住，也不能默默放行——
 *      权限系统的失败模式必须是「更严」，不能是「更松」。
 */

/** 内置三级。宿主可用 config.roles 覆盖或扩展 */
const DEFAULT_LEVELS = { viewer: 1, operator: 2, admin: 3 }

/**
 * 解析角色等级表。
 * 支持 `{ viewer: 1, admin: 3 }` 与 `{ viewer: { level: 1 } }` 两种写法。
 */
export function resolveRoleLevels(config = {}) {
  const custom = config.roles
  if (custom && typeof custom === 'object' && Object.keys(custom).length) {
    const out = {}
    for (const [name, v] of Object.entries(custom)) {
      const lv = typeof v === 'number' ? v : Number(v && v.level)
      out[name] = Number.isFinite(lv) ? lv : 0
    }
    return out
  }
  return { ...DEFAULT_LEVELS }
}

function createAuth(config = {}) {
  const levels = resolveRoleLevels(config)
  // 只有显式给了 role 才启用门禁；没给 = 不设防（向后兼容）
  const gated = typeof config.role === 'string' && config.role.length > 0
  const myLevel = gated ? (levels[config.role] !== undefined ? levels[config.role] : 0) : Infinity

  const levelOf = (name) => (levels[name] !== undefined ? levels[name] : Infinity)

  /** 块要求的最低等级；0 = 无要求 */
  function requiredLevel(block) {
    if (!block || typeof block !== 'object') return 0
    if (typeof block.minRole === 'string' && block.minRole) return levelOf(block.minRole)
    if (Array.isArray(block.roles) && block.roles.length) {
      return Math.min(...block.roles.map(levelOf))
    }
    return 0
  }

  function allowed(block) {
    if (!gated) return true
    const need = requiredLevel(block)
    if (need === 0) return true
    if (need === Infinity) return false // 未知角色名 → 拒绝
    return myLevel >= need
  }

  /** 命名能力检查：宿主可用来决定按钮显隐等（如 can('operator')） */
  function can(ability) {
    if (!gated) return true
    return myLevel >= levelOf(ability)
  }

  /** 块被拦下时，界面要告诉用户「需要什么角色」——但要说出角色名，不说数据 */
  function requiredLabel(block) {
    if (!block || typeof block !== 'object') return ''
    if (Array.isArray(block.roles) && block.roles.length) return block.roles.join(' / ')
    if (typeof block.minRole === 'string' && block.minRole) return block.minRole
    return ''
  }

  return { gated, role: config.role || null, level: myLevel, levels, allowed, requiredLevel, requiredLabel, can }
}

/** 同一个实例的所有块共用一份判断，用 config 对象做缓存键 */
const cache = typeof WeakMap === 'function' ? new WeakMap() : null

export function getAuth(config) {
  if (!config || typeof config !== 'object' || !cache) return createAuth(config || {})
  let a = cache.get(config)
  if (!a) {
    a = createAuth(config)
    cache.set(config, a)
  }
  return a
}

export default { getAuth, resolveRoleLevels, DEFAULT_LEVELS }
