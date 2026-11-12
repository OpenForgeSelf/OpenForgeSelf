import type { Decision, PermissionMode, Risk } from './types'

const DENY = [
  /^\s*rm\s+(-[a-zA-Z]*r[a-zA-Z]*f|-rf|--no-preserve-root).*\s+(\/|\/\*|~|\$HOME)\s*$/i,
  /^\s*mkfs\b/i,
  /^\s*dd\s+.*of=\/dev\//i,
  /^\s*:\(\)\s*\{\s*:\s*\|\s*:\s*&\s*\}\s*;\s*:/,
  /^\s*(shutdown|reboot|poweroff|halt)\b/i,
  /^\s*drop\s+(database|table)\b/i,
  /^\s*format\s+[a-z]:/i,
]

const HIGH = [
  /\brm\s+(-[a-zA-Z]*r[a-zA-Z]*f|-rf)\b/i,
  /\bchmod\s+(-R\s+)?777\b/i,
  /\bkill\s+-9\b/i,
  /\bmkfs\b/i,
  /\bdd\b/i,
  /\bcurl\b.*\|\s*(sh|bash|zsh)\b/i,
  /\bwget\b.*\|\s*(sh|bash|zsh)\b/i,
  /\b>\s*\/etc\//,
]

const WRITE = [
  /\b(sudo|doas)\b/i,
  /\b(rm|mv|cp|mkdir|rmdir|touch|chmod|chown|chgrp|ln|install)\b/i,
  /\b(apt|yum|dnf|pacman|brew|pip|npm|pnpm|yarn)\s+(install|remove|uninstall|upgrade|add)\b/i,
  /\bgit\s+(commit|push|reset|rebase|checkout|merge|clean)\b/i,
  /\bnginx\s+-s\b/i,
  /\bsystemctl\b/i,
  /\b(vi|vim|nano|sed|tee)\b/i,
  />{1,2}/,
]

const READ = [
  /^\s*(ls|ll|pwd|whoami|id|date|uname|uptime|df|du|ps|top|htop|free|env|printenv|which|whereis|type|file|stat|cat|head|tail|less|more|wc|echo|hostname|ip|ss|netstat|git\s+status|git\s+log|git\s+diff|git\s+branch)\b/i,
]

export function classifyRisk(command: string): Risk {
  const cmd = command.trim()
  if (!cmd) return 'read'
  if (DENY.some((r) => r.test(cmd))) return 'deny'
  if (HIGH.some((r) => r.test(cmd))) return 'high'
  if (WRITE.some((r) => r.test(cmd))) return 'write'
  if (READ.some((r) => r.test(cmd))) return 'read'
  // unknown conservative: write
  return 'write'
}

export function decide(risk: Risk, mode: PermissionMode): Decision {
  if (risk === 'deny') return 'deny'
  if (mode === 'Auto') return 'allow'
  if (mode === 'AskWrite') return risk === 'read' ? 'allow' : 'ask'
  // AskHighRisk
  return risk === 'high' ? 'ask' : 'allow'
}

export function hardRejectReason(command: string): string {
  return `命令被硬拒绝，未执行：${command.trim()}`
}
