/** 运行实况打开数据库待批。主参数 id，旧名 approvalId 仍接受。 */
export function approvalIdFromSearch(search: string): string {
  const raw = search.startsWith('?') ? search.slice(1) : search
  const params = new URLSearchParams(raw)
  return (params.get('id') || params.get('approvalId') || '').trim()
}
