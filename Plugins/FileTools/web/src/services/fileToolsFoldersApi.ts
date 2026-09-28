import { authFetch } from './authFetch'
import type {
  FolderCompareRow,
  FolderScanAccepted,
  FolderScanRequest,
  FolderScanView,
  FolderSnapshotDetail,
  FolderSnapshotSummary
} from '../types/fileTools'

/**
 * 目录大小排行（批次C）真实接口客户端 —— 走 `api/filetools/folders/*`。
 *
 * ⚠ 为什么单独成文而不进 `fileToolsApi.ts`：那个文件整体是 mock（不 import 任何 HTTP 客户端，
 * `getDirectoryStats` 直接 `setTimeout` 后返回硬编码 727MB），13 个真实端点零消费者。
 * 把新功能写进去 = 让新界面长在假数据层上。mock 债已单独记 `TODO.md`，还债时再合并。
 */

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'
const BASE = `${API_BASE_URL}/filetools/folders`

/** 宿主统一信封 `{code,message,success,data}` */
interface ApiEnvelope {
  code?: number
  message?: string
  success?: boolean
  data?: unknown
}

/**
 * 解析宿主统一信封：**已解包 data**，调用方直接按 T 收，
 * 禁止再取一次 `.data`（会得到 undefined 且页面无错、功能静默失效）。
 */
async function parseResponse<T>(response: Response): Promise<T> {
  if (response.status === 204) return undefined as unknown as T

  const text = await response.text()
  let json: ApiEnvelope | null
  try {
    json = text ? (JSON.parse(text) as ApiEnvelope) : null
  } catch {
    json = null
  }

  if (!response.ok || json?.success === false) {
    const detail = json?.message || text || response.statusText || '未知错误'
    throw new Error(`HTTP ${response.status}: ${detail}`)
  }

  return (json && json.data !== undefined ? json.data : json) as T
}

async function getJson<T>(url: string): Promise<T> {
  return parseResponse<T>(await authFetch(url))
}

async function postJson<T>(url: string, body?: unknown): Promise<T> {
  return parseResponse<T>(
    await authFetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body ?? {})
    })
  )
}

async function deleteJson<T>(url: string): Promise<T> {
  return parseResponse<T>(await authFetch(url, { method: 'DELETE' }))
}

export const folderScanApi = {
  /** 受理一次后台扫描（不阻塞）；返回 scanId 供轮询 */
  startScan(request: FolderScanRequest): Promise<FolderScanAccepted> {
    return postJson<FolderScanAccepted>(`${BASE}/scan`, request)
  },

  /** 查询状态与排行；Running 期间返回部分结果 */
  getScan(scanId: string): Promise<FolderScanView> {
    return getJson<FolderScanView>(`${BASE}/scan/${encodeURIComponent(scanId)}`)
  },

  cancelScan(scanId: string): Promise<boolean> {
    return postJson<boolean>(`${BASE}/scan/${encodeURIComponent(scanId)}/cancel`)
  },

  /** 释放内存中的任务（快照不受影响） */
  removeScan(scanId: string): Promise<boolean> {
    return deleteJson<boolean>(`${BASE}/scan/${encodeURIComponent(scanId)}`)
  },

  saveSnapshot(scanId: string, note = ''): Promise<FolderSnapshotSummary> {
    return postJson<FolderSnapshotSummary>(`${BASE}/snapshots`, { scanId, note })
  },

  listSnapshots(take = 50): Promise<FolderSnapshotSummary[]> {
    return getJson<FolderSnapshotSummary[]>(`${BASE}/snapshots?take=${take}`)
  },

  getSnapshot(snapshotId: number): Promise<FolderSnapshotDetail> {
    return getJson<FolderSnapshotDetail>(`${BASE}/snapshots/${snapshotId}`)
  },

  deleteSnapshot(snapshotId: number): Promise<boolean> {
    return deleteJson<boolean>(`${BASE}/snapshots/${snapshotId}`)
  },

  compare(fromSnapshotId: number, toSnapshotId: number): Promise<FolderCompareRow[]> {
    return getJson<FolderCompareRow[]>(
      `${BASE}/compare?from=${fromSnapshotId}&to=${toSnapshotId}`
    )
  }
}
