/** 运行实况打开目录扫描。主参数 id，旧名 scanId 仍接受。 */
export function scanIdFromSearch(search: string): string {
  const raw = search.startsWith('?') ? search.slice(1) : search
  const params = new URLSearchParams(raw)
  return (params.get('id') || params.get('scanId') || '').trim()
}
