export type FileToolTab = 'rename' | 'cleanup' | 'archive' | 'stats' | 'folders'

/* ===== 批次C · 目录大小排行（api/filetools/folders/*）=====
 * 与上面既有的 FileStats 系类型刻意分开：那边服务「单根总量 / 类型分布 / 大文件榜」，
 * 这边服务「按子目录聚合的占用排行 + 快照 + 趋势」。字段名逐字对齐后端 FolderScanModels.cs。
 * 字节大小的展示字符串一律由后端给（*Formatted），前端不再实现第二套格式化口径。
 */

// 后端 ScanState 是枚举，按仓内既有裁决序列化为 int（not-taken-decisions §007：保持 int，前端做映射）
export type FolderScanState = 0 | 1 | 2 | 3 | 4

export const SCAN_STATE_NAME: Record<number, string> = {
  0: 'Queued',
  1: 'Running',
  2: 'Completed',
  3: 'Failed',
  4: 'Cancelled'
}

export interface FolderScanRequest {
  directory: string
  top?: number
}

export interface FolderScanAccepted {
  scanId: string
  rootPath: string
  state: FolderScanState
}

export interface FolderSizeRow {
  relativePath: string
  name: string
  totalBytes: number
  totalFormatted: string
  directBytes: number
  directFormatted: string
  fileCount: number
  dirCount: number
  percentage: number
  isOther: boolean
}

export interface FolderScanView {
  scanId: string
  state: FolderScanState
  rootPath: string
  rootTotalBytes: number
  rootTotalFormatted: string
  rootOwnBytes: number
  rootOwnFormatted: string
  directoryCount: number
  fileCount: number
  durationMs: number
  inaccessibleCount: number
  skippedReparseCount: number
  truncated: boolean
  capNote: string
  partial: boolean
  top: number
  childCount: number
  items: FolderSizeRow[]
  otherRow: FolderSizeRow | null
  rootOwnRow: FolderSizeRow | null
  error: string
}

export interface FolderSnapshotSummary {
  id: number
  rootPath: string
  scannedAt: string
  durationMs: number
  rootTotalBytes: number
  rootTotalFormatted: string
  rootOwnBytes: number
  directoryCount: number
  fileCount: number
  top: number
  truncated: boolean
  inaccessibleCount: number
  skippedReparseCount: number
  note: string
  rootPathMissing: boolean
}

export interface FolderSnapshotDetail {
  snapshot: FolderSnapshotSummary
  items: FolderSizeRow[]
  otherRow: FolderSizeRow | null
  rootOwnRow: FolderSizeRow | null
}

export interface FolderCompareRow {
  relativePath: string
  name: string
  fromBytes: number
  toBytes: number
  deltaBytes: number
  deltaPercent: number
  missing: boolean
  added: boolean
}

export type RenameRuleType =
  | 'sequence'
  | 'date'
  | 'replace'
  | 'regex'
  | 'prefix'
  | 'suffix'
  | 'extension'

export type CleanupFilterType =
  | 'extension'
  | 'size'
  | 'dateCreated'
  | 'dateModified'

export type ArchiveFormat = 'zip' | '7z' | 'tar' | 'tar.gz'

export type CompressionLevel = 'store' | 'fastest' | 'standard' | 'optimal'

export interface RenameRule {
  id: string
  ruleType: RenameRuleType
  params: Record<string, unknown>
  enabled: boolean
}

export interface RenamePreviewItem {
  originalPath: string
  originalName: string
  newPath: string
  newName: string
  isValid: boolean
  errorMessage: string | null
}

export interface CleanupRule {
  id: string
  filterType: CleanupFilterType
  condition: string
  value: string
  enabled: boolean
}

export interface CleanupPreviewItem {
  filePath: string
  fileName: string
  size: number
  reason: string
  canUndo: boolean
  selected: boolean
}

export interface FileTypeBreakdown {
  extension: string
  count: number
  size: number
  percentage: number
}

export interface LargeFileItem {
  filePath: string
  fileName: string
  size: number
}

export interface FileStats {
  fileCount: number
  folderCount: number
  totalSize: number
  typeBreakdown: FileTypeBreakdown[]
  largeFiles: LargeFileItem[]
}

export interface ArchiveInfo {
  fileName: string
  filePath: string
  size: number
  fileCount: number
  compressedSize: number
  compressionRatio: number
  format: ArchiveFormat
  hasPassword: boolean
}

export interface SelectedFile {
  path: string
  name: string
  size: number
  isDirectory: boolean
}

export interface SequenceRuleParams {
  startValue: number
  step: number
  digits: number
  position: 'prefix' | 'suffix' | 'replace'
  separator: string
}

export interface DateRuleParams {
  format: string
  position: 'prefix' | 'suffix'
  separator: string
}

export interface ReplaceRuleParams {
  find: string
  replace: string
  caseSensitive: boolean
}

export interface RegexRuleParams {
  pattern: string
  replace: string
  flags: string
}

export interface PrefixRuleParams {
  text: string
}

export interface SuffixRuleParams {
  text: string
}

export interface ExtensionRuleParams {
  newExtension: string
}
