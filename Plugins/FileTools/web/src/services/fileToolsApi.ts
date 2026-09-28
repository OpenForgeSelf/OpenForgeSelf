import type {
  RenameRule,
  RenamePreviewItem,
  CleanupRule,
  CleanupPreviewItem,
  FileStats,
  ArchiveInfo,
  ArchiveFormat,
  LargeFileItem,
  FileTypeBreakdown
} from '../types/fileTools'

function generateId(): string {
  return Math.random().toString(36).substring(2, 11)
}

function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

function getFileName(path: string): string {
  const parts = path.replace(/\\/g, '/').split('/')
  return parts[parts.length - 1] || path
}

function getFileExtension(path: string): string {
  const name = getFileName(path)
  const dotIndex = name.lastIndexOf('.')
  return dotIndex > 0 ? name.substring(dotIndex + 1) : ''
}

function applyRenameRules(fileName: string, rules: RenameRule[]): string {
  let result = fileName
  const ext = getFileExtension(result)
  const baseName = ext ? result.substring(0, result.length - ext.length - 1) : result

  for (const rule of rules) {
    if (!rule.enabled) continue

    switch (rule.ruleType) {
      case 'sequence': {
        const params = rule.params as {
          startValue: number
          step: number
          digits: number
          position: 'prefix' | 'suffix' | 'replace'
          separator: string
        }
        const seq = String(params.startValue).padStart(params.digits, '0')
        if (params.position === 'prefix') {
          result = seq + params.separator + result
        } else if (params.position === 'suffix') {
          if (ext) {
            result = baseName + params.separator + seq + '.' + ext
          } else {
            result = result + params.separator + seq
          }
        } else {
          result = seq + (ext ? '.' + ext : '')
        }
        break
      }
      case 'date': {
        const params = rule.params as {
          format: string
          position: 'prefix' | 'suffix'
          separator: string
        }
        const now = new Date()
        let dateStr = params.format
        dateStr = dateStr.replace('YYYY', String(now.getFullYear()))
        dateStr = dateStr.replace('MM', String(now.getMonth() + 1).padStart(2, '0'))
        dateStr = dateStr.replace('DD', String(now.getDate()).padStart(2, '0'))
        dateStr = dateStr.replace('HH', String(now.getHours()).padStart(2, '0'))
        dateStr = dateStr.replace('mm', String(now.getMinutes()).padStart(2, '0'))
        dateStr = dateStr.replace('ss', String(now.getSeconds()).padStart(2, '0'))

        if (params.position === 'prefix') {
          result = dateStr + params.separator + result
        } else {
          if (ext) {
            result = baseName + params.separator + dateStr + '.' + ext
          } else {
            result = result + params.separator + dateStr
          }
        }
        break
      }
      case 'replace': {
        const params = rule.params as {
          find: string
          replace: string
          caseSensitive: boolean
        }
        if (params.caseSensitive) {
          result = result.split(params.find).join(params.replace)
        } else {
          const regex = new RegExp(params.find.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'gi')
          result = result.replace(regex, params.replace)
        }
        break
      }
      case 'regex': {
        const params = rule.params as {
          pattern: string
          replace: string
          flags: string
        }
        try {
          const regex = new RegExp(params.pattern, params.flags)
          result = result.replace(regex, params.replace)
        } catch {
          // 忽略无效的正则表达式
        }
        break
      }
      case 'prefix': {
        const params = rule.params as { text: string }
        result = params.text + result
        break
      }
      case 'suffix': {
        const params = rule.params as { text: string }
        if (ext) {
          result = baseName + params.text + '.' + ext
        } else {
          result = result + params.text
        }
        break
      }
      case 'extension': {
        const params = rule.params as { newExtension: string }
        const newExt = params.newExtension.replace(/^\./, '')
        if (ext) {
          result = baseName + '.' + newExt
        } else {
          result = result + '.' + newExt
        }
        break
      }
    }
  }

  return result
}

async function previewRename(
  files: string[],
  rules: RenameRule[]
): Promise<RenamePreviewItem[]> {
  await new Promise(resolve => setTimeout(resolve, 300))

  return files.map(filePath => {
    const originalName = getFileName(filePath)
    const newName = applyRenameRules(originalName, rules)
    const dirPath = filePath.substring(0, filePath.length - originalName.length)
    const newPath = dirPath + newName

    let isValid = true
    let errorMessage: string | null = null

    if (!newName || newName === originalName) {
      isValid = false
      errorMessage = '文件名未改变'
    }
    if (newName.includes('/') || newName.includes('\\')) {
      isValid = false
      errorMessage = '文件名不能包含路径分隔符'
    }

    return {
      originalPath: filePath,
      originalName,
      newPath,
      newName,
      isValid,
      errorMessage
    }
  })
}

async function executeRename(
  files: string[],
  rules: RenameRule[]
): Promise<{ success: number; failed: number; results: RenamePreviewItem[] }> {
  await new Promise(resolve => setTimeout(resolve, 800))

  const preview = await previewRename(files, rules)
  let success = 0
  let failed = 0

  const results = preview.map(item => {
    if (item.isValid) {
      success++
      return { ...item, isValid: true }
    } else {
      failed++
      return item
    }
  })

  return { success, failed, results }
}

async function previewCleanup(
  directory: string,
  _rules: CleanupRule[]
): Promise<CleanupPreviewItem[]> {
  await new Promise(resolve => setTimeout(resolve, 500))

  const mockFiles: CleanupPreviewItem[] = [
    {
      filePath: directory + '/temp/cache1.tmp',
      fileName: 'cache1.tmp',
      size: 1024 * 1024 * 5,
      reason: '临时文件',
      canUndo: true,
      selected: true
    },
    {
      filePath: directory + '/temp/cache2.tmp',
      fileName: 'cache2.tmp',
      size: 1024 * 1024 * 3,
      reason: '临时文件',
      canUndo: true,
      selected: true
    },
    {
      filePath: directory + '/logs/error.log',
      fileName: 'error.log',
      size: 1024 * 500,
      reason: '日志文件',
      canUndo: true,
      selected: false
    },
    {
      filePath: directory + '/old/backup.bak',
      fileName: 'backup.bak',
      size: 1024 * 1024 * 50,
      reason: '备份文件',
      canUndo: true,
      selected: true
    },
    {
      filePath: directory + '/downloads/setup.exe',
      fileName: 'setup.exe',
      size: 1024 * 1024 * 100,
      reason: '安装包',
      canUndo: true,
      selected: false
    }
  ]

  return mockFiles
}

async function executeCleanup(
  _directory: string,
  _rules: CleanupRule[],
  _deletePermanently: boolean = false
): Promise<{ success: number; failed: number; totalSize: number }> {
  await new Promise(resolve => setTimeout(resolve, 1000))

  return {
    success: 3,
    failed: 0,
    totalSize: 1024 * 1024 * 58
  }
}

async function findEmptyFolders(directory: string): Promise<string[]> {
  await new Promise(resolve => setTimeout(resolve, 300))
  return [
    directory + '/empty_folder_1',
    directory + '/temp/empty_sub',
    directory + '/logs/archive/empty'
  ]
}

async function findDuplicates(directory: string): Promise<Record<string, string[]>> {
  await new Promise(resolve => setTimeout(resolve, 800))
  return {
    'hash_abc123': [
      directory + '/photos/IMG_001.jpg',
      directory + '/backup/photos/IMG_001.jpg'
    ],
    'hash_def456': [
      directory + '/docs/report.pdf',
      directory + '/archive/report_v1.pdf',
      directory + '/backup/report.pdf'
    ]
  }
}

async function compressFiles(
  files: string[],
  outputPath: string,
  _format: ArchiveFormat = 'zip',
  _password?: string,
  _volumeSize?: number
): Promise<{ success: boolean; outputPath: string; size: number }> {
  await new Promise(resolve => setTimeout(resolve, 1500))

  const totalSize = files.reduce((sum) => sum + 1024 * 1024 * 10, 0)

  return {
    success: true,
    outputPath,
    size: totalSize * 0.7
  }
}

async function extractFiles(
  _archivePath: string,
  _outputPath: string,
  _password?: string
): Promise<{ success: boolean; fileCount: number; totalSize: number }> {
  await new Promise(resolve => setTimeout(resolve, 1200))

  return {
    success: true,
    fileCount: 25,
    totalSize: 1024 * 1024 * 50
  }
}

async function getArchiveInfo(archivePath: string): Promise<ArchiveInfo> {
  await new Promise(resolve => setTimeout(resolve, 400))

  const fileName = getFileName(archivePath)
  const ext = getFileExtension(archivePath).toLowerCase() as ArchiveFormat

  return {
    fileName,
    filePath: archivePath,
    size: 1024 * 1024 * 35,
    fileCount: 25,
    compressedSize: 1024 * 1024 * 35,
    compressionRatio: 0.7,
    format: ext || 'zip',
    hasPassword: false
  }
}

async function getDirectoryStats(directory: string): Promise<FileStats> {
  await new Promise(resolve => setTimeout(resolve, 600))

  const typeBreakdown: FileTypeBreakdown[] = [
    { extension: 'jpg', count: 156, size: 1024 * 1024 * 256, percentage: 35.2 },
    { extension: 'png', count: 89, size: 1024 * 1024 * 128, percentage: 17.6 },
    { extension: 'pdf', count: 45, size: 1024 * 1024 * 96, percentage: 13.2 },
    { extension: 'docx', count: 32, size: 1024 * 1024 * 48, percentage: 6.6 },
    { extension: 'mp4', count: 12, size: 1024 * 1024 * 180, percentage: 24.8 },
    { extension: '其他', count: 78, size: 1024 * 1024 * 19, percentage: 2.6 }
  ]

  const largeFiles: LargeFileItem[] = [
    { filePath: directory + '/videos/project_demo.mp4', fileName: 'project_demo.mp4', size: 1024 * 1024 * 150 },
    { filePath: directory + '/backup/full_backup.zip', fileName: 'full_backup.zip', size: 1024 * 1024 * 120 },
    { filePath: directory + '/videos/tutorial.mp4', fileName: 'tutorial.mp4', size: 1024 * 1024 * 85 },
    { filePath: directory + '/photos/raw/DSC_001.raw', fileName: 'DSC_001.raw', size: 1024 * 1024 * 45 },
    { filePath: directory + '/docs/large_report.pdf', fileName: 'large_report.pdf', size: 1024 * 1024 * 32 }
  ]

  return {
    fileCount: 412,
    folderCount: 56,
    totalSize: 1024 * 1024 * 727,
    typeBreakdown,
    largeFiles
  }
}

async function getLargeFiles(directory: string, limit: number = 20): Promise<LargeFileItem[]> {
  const stats = await getDirectoryStats(directory)
  return stats.largeFiles.slice(0, limit)
}

async function getFileTypeBreakdown(directory: string): Promise<FileTypeBreakdown[]> {
  const stats = await getDirectoryStats(directory)
  return stats.typeBreakdown
}

export const fileToolsApi = {
  previewRename,
  executeRename,
  previewCleanup,
  executeCleanup,
  findEmptyFolders,
  findDuplicates,
  compressFiles,
  extractFiles,
  getArchiveInfo,
  getDirectoryStats,
  getLargeFiles,
  getFileTypeBreakdown,
  formatFileSize,
  getFileName,
  getFileExtension,
  generateId
}
