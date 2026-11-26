/** F03：同一路径再次导入或登记时的提示与路径比对。 */

/** 斜杠统一、去掉末尾分隔符；Windows 盘符路径再忽略大小写。 */
export function sameProjectRoot(a: string, b: string): boolean {
  const left = normalizeRoot(a)
  const right = normalizeRoot(b)
  if (!left || !right) return false
  if (isWindowsPath(left) || isWindowsPath(right)) {
    return left.toLowerCase() === right.toLowerCase()
  }
  return left === right
}

export function alreadyRegisteredProjectMessage(name?: string): string {
  const label = name?.trim()
  return label ? `「${label}」已存在并可打开原项目` : '已存在并可打开原项目'
}

function normalizeRoot(path: string): string {
  const text = path.trim().replace(/\\/g, '/')
  if (!text) return ''
  if (/^[A-Za-z]:$/.test(text)) return text + '/'
  if (text === '/') return text
  return text.replace(/\/+$/, '')
}

function isWindowsPath(path: string): boolean {
  return /^[A-Za-z]:/.test(path)
}
