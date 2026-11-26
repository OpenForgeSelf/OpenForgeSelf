/** 运行实况打开目录扫描：/file-tools?scanId= */
export function scanIdFromSearch(search: string): string {
  const raw = search.startsWith('?') ? search.slice(1) : search
  return new URLSearchParams(raw).get('scanId')?.trim() ?? ''
}
