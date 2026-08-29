export type FileToolTab = 'rename' | 'cleanup' | 'archive' | 'stats'

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
