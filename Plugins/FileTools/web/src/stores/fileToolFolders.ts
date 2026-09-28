import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { folderScanApi } from '../services/fileToolsFoldersApi'
import { shouldApplyProgress } from '../components/foldersModel'
import type {
  FolderCompareRow,
  FolderScanView,
  FolderSizeRow,
  FolderSnapshotDetail,
  FolderSnapshotSummary
} from '../types/fileTools'

/**
 * 目录大小排行（批次C）的状态层。
 * 与 `stores/fileTools.ts` 刻意分开：那个 store 的 loadStats 等动作仍指向 mock 服务层
 * （mock 债已记 TODO），本 store 只走 `api/filetools/folders/*` 真接口，两者只共用 currentTab。
 */
export const useFolderScanStore = defineStore('fileToolFolders', () => {
  const directory = ref('')
  const top = ref(50)
  const scanId = ref<string | null>(null)
  const view = ref<FolderScanView | null>(null)
  const polling = ref(false)
  const busy = ref(false)
  const error = ref<string | null>(null)

  const snapshots = ref<FolderSnapshotSummary[]>([])
  const detail = ref<FolderSnapshotDetail | null>(null)
  const compareRows = ref<FolderCompareRow[]>([])
  const compareNote = ref<string | null>(null)

  let timer: ReturnType<typeof setInterval> | undefined

  // state 是 int 枚举（0 Queued / 1 Running / 2 Completed / 3 Failed / 4 Cancelled）
  const isScanning = computed(
    () => !!view.value && (view.value.state === 0 || view.value.state === 1)
  )
  const hasResult = computed(() => !!view.value && view.value.state !== 0)
  const percentDone = computed(() => {
    const v = view.value
    if (!v) return 0
    if (v.state === 2 || v.state === 3 || v.state === 4) return 100 // Completed/Failed/Cancelled
    // 没有「总量」分母可用（大小未知是这类扫描的本质），用已计入字节的对数量程给个进度感
    const grown = Math.log10(Math.max(1, v.fileCount))
    return Math.min(95, Math.round(grown * 12))
  })

  function clearError(): void {
    error.value = null
  }

  /** Windows 宿主：把后端给的 / 分隔相对路径接回根路径下，供钻取再扫一层 */
  function pathUnderRoot(root: string, relativePath: string): string {
    const base = root.replace(/[\\/]+$/, '')
    const rel = relativePath.replace(/[\\/]/g, '\\')
    return `${base}\\${rel}`
  }

  function stopPolling(): void {
    if (timer !== undefined) {
      clearInterval(timer)
      timer = undefined
    }
    polling.value = false
  }

  function startPolling(): void {
    stopPolling()
    polling.value = true
    timer = setInterval(() => {
      void pollOnce()
    }, 1000)
  }

  async function pollOnce(): Promise<void> {
    const id = scanId.value
    if (!id) {
      stopPolling()
      return
    }
    try {
      const next = await folderScanApi.getScan(id)
      // 同值不赋值：轮询每 1s 一次，整块重赋值会让表格闪动
      if (shouldApplyProgress(view.value, next)) view.value = next
      if (next.state !== 0 && next.state !== 1) stopPolling()
    } catch (err) {
      error.value = toMessage(err)
      stopPolling()
    }
  }

  async function startScan(target?: string): Promise<void> {
    clearError()
    const dir = (target ?? directory.value).trim()
    if (!dir) {
      error.value = '请先填写要统计的目录路径'
      return
    }
    directory.value = dir
    busy.value = true
    try {
      const accepted = await folderScanApi.startScan({ directory: dir, top: top.value })
      scanId.value = accepted.scanId
      view.value = null
      startPolling()
      await pollOnce()
    } catch (err) {
      error.value = toMessage(err)
    } finally {
      busy.value = false
    }
  }

  /** 钻取：以某个子目录为新根重扫（每次都是独立任务，不复用旧结果，避免陈旧冒充新） */
  async function drillInto(row: FolderSizeRow): Promise<void> {
    const root = view.value?.rootPath
    if (!root || row.isOther || !row.relativePath) return
    await startScan(pathUnderRoot(root, row.relativePath))
  }

  async function confirmCancelScan(ask: () => Promise<boolean>): Promise<boolean> {
    const id = scanId.value
    if (!id) return false
    return runConfirmed(ask, async () => {
      await folderScanApi.cancelScan(id)
      await pollOnce()
    })
  }

  async function releaseScan(): Promise<void> {
    const id = scanId.value
    if (!id) return
    try {
      await folderScanApi.removeScan(id)
    } catch (err) {
      error.value = toMessage(err)
    }
  }

  async function saveSnapshot(note: string, ask?: () => Promise<boolean>): Promise<void> {
    const id = scanId.value
    if (!id) {
      error.value = '没有可保存的扫描任务'
      return
    }
    clearError()
    try {
      const run = async (): Promise<void> => {
        await folderScanApi.saveSnapshot(id, note)
        await loadSnapshots()
      }
      // 保存会改变用户可见状态（快照区多一条）→ 有确认器就走二次确认；取消则一个请求都不发
      if (ask) await runConfirmed(ask, run)
      else await run()
    } catch (err) {
      error.value = toMessage(err)
    }
  }

  async function loadSnapshots(): Promise<void> {
    clearError()
    try {
      snapshots.value = await folderScanApi.listSnapshots()
    } catch (err) {
      error.value = toMessage(err)
    }
  }

  async function openSnapshot(snapshotId: number): Promise<void> {
    clearError()
    try {
      detail.value = await folderScanApi.getSnapshot(snapshotId)
    } catch (err) {
      error.value = toMessage(err)
      detail.value = null
    }
  }

  async function confirmDeleteSnapshot(snapshotId: number, ask: () => Promise<boolean>): Promise<boolean> {
    clearError()
    return runConfirmed(ask, async () => {
      await folderScanApi.deleteSnapshot(snapshotId)
      if (detail.value?.snapshot.id === snapshotId) detail.value = null
      await loadSnapshots()
    })
  }

  async function runCompare(from: number, to: number): Promise<void> {
    clearError()
    compareNote.value = null
    try {
      compareRows.value = await folderScanApi.compare(from, to)
      if (compareRows.value.length === 0) compareNote.value = '两个快照之间没有可比的目录差异'
    } catch (err) {
      error.value = toMessage(err)
      compareRows.value = []
    }
  }

  async function runConfirmed(
    ask: () => Promise<boolean>,
    action: () => Promise<void>
  ): Promise<boolean> {
    if (!(await ask())) return false
    try {
      await action()
      return true
    } catch (err) {
      error.value = toMessage(err)
      return false
    }
  }

  function reset(): void {
    stopPolling()
    scanId.value = null
    view.value = null
    error.value = null
  }

  function toMessage(err: unknown): string {
    return err instanceof Error ? err.message : String(err)
  }

  return {
    directory,
    top,
    scanId,
    view,
    polling,
    busy,
    error,
    snapshots,
    detail,
    compareRows,
    compareNote,
    isScanning,
    hasResult,
    percentDone,
    clearError,
    startScan,
    drillInto,
    pollOnce,
    confirmCancelScan,
    releaseScan,
    saveSnapshot,
    loadSnapshots,
    openSnapshot,
    confirmDeleteSnapshot,
    runCompare,
    stopPolling,
    reset
  }
})
