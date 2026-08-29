import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type {
  FileToolTab,
  RenameRule,
  RenamePreviewItem,
  CleanupRule,
  CleanupPreviewItem,
  FileStats,
  ArchiveInfo,
  ArchiveFormat,
  CompressionLevel,
  RenameRuleType,
  CleanupFilterType
} from '@/types/fileTools'
import { fileToolsApi } from '@/services/fileToolsApi'

export const useFileToolsStore = defineStore('fileTools', () => {
  const currentTab = ref<FileToolTab>('rename')
  const selectedFiles = ref<string[]>([])
  const selectedDirectory = ref('')
  const renameRules = ref<RenameRule[]>([])
  const renamePreview = ref<RenamePreviewItem[]>([])
  const cleanupRules = ref<CleanupRule[]>([])
  const cleanupPreview = ref<CleanupPreviewItem[]>([])
  const statsData = ref<FileStats | null>(null)
  const isProcessing = ref(false)
  const error = ref<string | null>(null)
  const progress = ref(0)

  const archiveMode = ref<'compress' | 'extract'>('compress')
  const archiveFormat = ref<ArchiveFormat>('zip')
  const compressionLevel = ref<CompressionLevel>('standard')
  const archivePassword = ref('')
  const archiveOutputPath = ref('')
  const archiveInputPath = ref('')
  const archiveInfo = ref<ArchiveInfo | null>(null)

  const validPreviewCount = computed(() => {
    return renamePreview.value.filter(item => item.isValid).length
  })

  const totalCleanupSize = computed(() => {
    return cleanupPreview.value
      .filter(item => item.selected)
      .reduce((sum, item) => sum + item.size, 0)
  })

  const selectedFileCount = computed(() => selectedFiles.value.length)

  function setTab(tab: FileToolTab): void {
    currentTab.value = tab
    error.value = null
  }

  function addFiles(files: string[]): void {
    const newFiles = files.filter(f => !selectedFiles.value.includes(f))
    selectedFiles.value.push(...newFiles)
  }

  function removeFile(filePath: string): void {
    const index = selectedFiles.value.indexOf(filePath)
    if (index > -1) {
      selectedFiles.value.splice(index, 1)
    }
  }

  function clearFiles(): void {
    selectedFiles.value = []
    renamePreview.value = []
  }

  function setSelectedDirectory(directory: string): void {
    selectedDirectory.value = directory
  }

  function addRenameRule(ruleType: RenameRuleType): void {
    const rule: RenameRule = {
      id: fileToolsApi.generateId(),
      ruleType,
      enabled: true,
      params: getDefaultParams(ruleType)
    }
    renameRules.value.push(rule)
  }

  function getDefaultParams(ruleType: RenameRuleType): Record<string, unknown> {
    switch (ruleType) {
      case 'sequence':
        return {
          startValue: 1,
          step: 1,
          digits: 3,
          position: 'prefix',
          separator: '_'
        }
      case 'date':
        return {
          format: 'YYYYMMDD',
          position: 'prefix',
          separator: '_'
        }
      case 'replace':
        return {
          find: '',
          replace: '',
          caseSensitive: false
        }
      case 'regex':
        return {
          pattern: '',
          replace: '',
          flags: 'g'
        }
      case 'prefix':
        return { text: '' }
      case 'suffix':
        return { text: '' }
      case 'extension':
        return { newExtension: '' }
      default:
        return {}
    }
  }

  function removeRenameRule(ruleId: string): void {
    const index = renameRules.value.findIndex(r => r.id === ruleId)
    if (index > -1) {
      renameRules.value.splice(index, 1)
    }
  }

  function updateRenameRule(ruleId: string, params: Record<string, unknown>): void {
    const rule = renameRules.value.find(r => r.id === ruleId)
    if (rule) {
      rule.params = { ...rule.params, ...params }
    }
  }

  function toggleRenameRule(ruleId: string): void {
    const rule = renameRules.value.find(r => r.id === ruleId)
    if (rule) {
      rule.enabled = !rule.enabled
    }
  }

  function moveRenameRule(ruleId: string, direction: 'up' | 'down'): void {
    const index = renameRules.value.findIndex(r => r.id === ruleId)
    if (index === -1) return

    const newIndex = direction === 'up' ? index - 1 : index + 1
    if (newIndex < 0 || newIndex >= renameRules.value.length) return

    const temp = renameRules.value[index]
    renameRules.value[index] = renameRules.value[newIndex]
    renameRules.value[newIndex] = temp
  }

  async function previewRename(): Promise<void> {
    if (selectedFiles.value.length === 0) {
      error.value = '请先选择文件'
      return
    }

    try {
      isProcessing.value = true
      error.value = null
      progress.value = 0

      renamePreview.value = await fileToolsApi.previewRename(
        selectedFiles.value,
        renameRules.value
      )
      progress.value = 100
    } catch (e) {
      console.error('重命名预览失败:', e)
      error.value = e instanceof Error ? e.message : '重命名预览失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function executeRename(): Promise<{ success: number; failed: number }> {
    if (selectedFiles.value.length === 0) {
      error.value = '请先选择文件'
      return { success: 0, failed: 0 }
    }

    try {
      isProcessing.value = true
      error.value = null
      progress.value = 0

      const result = await fileToolsApi.executeRename(
        selectedFiles.value,
        renameRules.value
      )
      progress.value = 100

      return { success: result.success, failed: result.failed }
    } catch (e) {
      console.error('重命名执行失败:', e)
      error.value = e instanceof Error ? e.message : '重命名执行失败'
      return { success: 0, failed: 0 }
    } finally {
      isProcessing.value = false
    }
  }

  function addCleanupRule(filterType: CleanupFilterType): void {
    const rule: CleanupRule = {
      id: fileToolsApi.generateId(),
      filterType,
      condition: getDefaultCondition(filterType),
      value: '',
      enabled: true
    }
    cleanupRules.value.push(rule)
  }

  function getDefaultCondition(filterType: CleanupFilterType): string {
    switch (filterType) {
      case 'extension':
        return 'include'
      case 'size':
        return 'greater'
      case 'dateCreated':
      case 'dateModified':
        return 'before'
      default:
        return ''
    }
  }

  function removeCleanupRule(ruleId: string): void {
    const index = cleanupRules.value.findIndex(r => r.id === ruleId)
    if (index > -1) {
      cleanupRules.value.splice(index, 1)
    }
  }

  function updateCleanupRule(ruleId: string, updates: Partial<CleanupRule>): void {
    const rule = cleanupRules.value.find(r => r.id === ruleId)
    if (rule) {
      Object.assign(rule, updates)
    }
  }

  function toggleCleanupItem(index: number): void {
    if (cleanupPreview.value[index]) {
      cleanupPreview.value[index].selected = !cleanupPreview.value[index].selected
    }
  }

  function selectAllCleanupItems(selected: boolean): void {
    cleanupPreview.value.forEach(item => {
      item.selected = selected
    })
  }

  async function previewCleanup(): Promise<void> {
    if (!selectedDirectory.value) {
      error.value = '请先选择目录'
      return
    }

    try {
      isProcessing.value = true
      error.value = null
      progress.value = 0

      cleanupPreview.value = await fileToolsApi.previewCleanup(
        selectedDirectory.value,
        cleanupRules.value
      )
      progress.value = 100
    } catch (e) {
      console.error('清理预览失败:', e)
      error.value = e instanceof Error ? e.message : '清理预览失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function executeCleanup(deletePermanently: boolean = false): Promise<{ success: number; failed: number }> {
    if (!selectedDirectory.value) {
      error.value = '请先选择目录'
      return { success: 0, failed: 0 }
    }

    try {
      isProcessing.value = true
      error.value = null
      progress.value = 0

      const result = await fileToolsApi.executeCleanup(
        selectedDirectory.value,
        cleanupRules.value,
        deletePermanently
      )
      progress.value = 100

      return { success: result.success, failed: result.failed }
    } catch (e) {
      console.error('清理执行失败:', e)
      error.value = e instanceof Error ? e.message : '清理执行失败'
      return { success: 0, failed: 0 }
    } finally {
      isProcessing.value = false
    }
  }

  async function loadStats(): Promise<void> {
    if (!selectedDirectory.value) {
      error.value = '请先选择目录'
      return
    }

    try {
      isProcessing.value = true
      error.value = null
      progress.value = 0

      statsData.value = await fileToolsApi.getDirectoryStats(selectedDirectory.value)
      progress.value = 100
    } catch (e) {
      console.error('加载统计数据失败:', e)
      error.value = e instanceof Error ? e.message : '加载统计数据失败'
    } finally {
      isProcessing.value = false
    }
  }

  function setArchiveMode(mode: 'compress' | 'extract'): void {
    archiveMode.value = mode
    archiveInfo.value = null
  }

  function setArchiveFormat(format: ArchiveFormat): void {
    archiveFormat.value = format
  }

  function setCompressionLevel(level: CompressionLevel): void {
    compressionLevel.value = level
  }

  function setArchivePassword(password: string): void {
    archivePassword.value = password
  }

  function setArchiveOutputPath(path: string): void {
    archiveOutputPath.value = path
  }

  function setArchiveInputPath(path: string): void {
    archiveInputPath.value = path
  }

  async function loadArchiveInfo(): Promise<void> {
    if (!archiveInputPath.value) {
      error.value = '请先选择压缩包'
      return
    }

    try {
      isProcessing.value = true
      error.value = null

      archiveInfo.value = await fileToolsApi.getArchiveInfo(archiveInputPath.value)
    } catch (e) {
      console.error('获取压缩包信息失败:', e)
      error.value = e instanceof Error ? e.message : '获取压缩包信息失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function compress(): Promise<boolean> {
    if (selectedFiles.value.length === 0) {
      error.value = '请先选择要压缩的文件'
      return false
    }

    try {
      isProcessing.value = true
      error.value = null
      progress.value = 0

      const result = await fileToolsApi.compressFiles(
        selectedFiles.value,
        archiveOutputPath.value,
        archiveFormat.value,
        archivePassword.value || undefined
      )
      progress.value = 100

      return result.success
    } catch (e) {
      console.error('压缩失败:', e)
      error.value = e instanceof Error ? e.message : '压缩失败'
      return false
    } finally {
      isProcessing.value = false
    }
  }

  async function extract(): Promise<boolean> {
    if (!archiveInputPath.value || !archiveOutputPath.value) {
      error.value = '请选择压缩包和输出路径'
      return false
    }

    try {
      isProcessing.value = true
      error.value = null
      progress.value = 0

      const result = await fileToolsApi.extractFiles(
        archiveInputPath.value,
        archiveOutputPath.value,
        archivePassword.value || undefined
      )
      progress.value = 100

      return result.success
    } catch (e) {
      console.error('解压失败:', e)
      error.value = e instanceof Error ? e.message : '解压失败'
      return false
    } finally {
      isProcessing.value = false
    }
  }

  function clearError(): void {
    error.value = null
  }

  function resetProgress(): void {
    progress.value = 0
  }

  return {
    currentTab,
    selectedFiles,
    selectedDirectory,
    renameRules,
    renamePreview,
    cleanupRules,
    cleanupPreview,
    statsData,
    isProcessing,
    error,
    progress,
    archiveMode,
    archiveFormat,
    compressionLevel,
    archivePassword,
    archiveOutputPath,
    archiveInputPath,
    archiveInfo,
    validPreviewCount,
    totalCleanupSize,
    selectedFileCount,
    setTab,
    addFiles,
    removeFile,
    clearFiles,
    setSelectedDirectory,
    addRenameRule,
    removeRenameRule,
    updateRenameRule,
    toggleRenameRule,
    moveRenameRule,
    previewRename,
    executeRename,
    addCleanupRule,
    removeCleanupRule,
    updateCleanupRule,
    toggleCleanupItem,
    selectAllCleanupItems,
    previewCleanup,
    executeCleanup,
    loadStats,
    setArchiveMode,
    setArchiveFormat,
    setCompressionLevel,
    setArchivePassword,
    setArchiveOutputPath,
    setArchiveInputPath,
    loadArchiveInfo,
    compress,
    extract,
    clearError,
    resetProgress
  }
})
