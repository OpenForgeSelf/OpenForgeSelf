/** 运行实况打开数据库待批：/db-admin?approvalId= */
export function approvalIdFromSearch(search: string): string {
  const raw = search.startsWith('?') ? search.slice(1) : search
  return new URLSearchParams(raw).get('approvalId')?.trim() ?? ''
}
