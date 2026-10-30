/**
 * 规格 HTTP。成功体没有 data 字段，request() 会原样返回 { success, spec|specs|... }。
 * 写请求走队列，避免后发先至把界面盖回旧快照。
 */
import { apiGet, apiPost, withQuery } from './http'
import { createWriteQueue } from './specOps'
import type { SpecCase, SpecCoverage, SpecLink, SpecVersion } from './types'
import type { CaseSpecBody, CreateSpecBody, LinkSpecBody, VersionSpecBody } from './specOps'

const enqueue = createWriteQueue()

interface SpecsResp { success?: boolean; specs?: SpecVersion[] }
interface SpecResp { success?: boolean; spec?: SpecVersion }
interface LinksResp { success?: boolean; links?: SpecLink[] }
interface LinkResp { success?: boolean; link?: SpecLink }
interface CaseResp { success?: boolean; case?: SpecCase }
interface CoverageResp { success?: boolean; coverage?: SpecCoverage }

function specsPath(projectId: number, specId?: string): string {
  const base = `/api/projects/${projectId}/specs`
  return specId ? `${base}/${encodeURIComponent(specId)}` : base
}

export async function listSpecs(projectId: number): Promise<SpecVersion[]> {
  const resp = await apiGet<SpecsResp>(specsPath(projectId))
  return resp?.specs ?? []
}

export async function getSpec(projectId: number, specId: string, version?: number): Promise<SpecVersion> {
  const resp = await apiGet<SpecResp>(withQuery(specsPath(projectId, specId), { version }))
  if (!resp?.spec) throw new Error('读取规格失败：后端未返回 spec')
  return resp.spec
}

export function createSpec(projectId: number, body: CreateSpecBody): Promise<SpecVersion> {
  return enqueue(async () => {
    const resp = await apiPost<SpecResp>(specsPath(projectId), body)
    if (!resp?.spec) throw new Error('创建规格失败：后端未返回 spec')
    return resp.spec
  })
}

export function addSpecVersion(projectId: number, specId: string, body: VersionSpecBody): Promise<SpecVersion> {
  return enqueue(async () => {
    const resp = await apiPost<SpecResp>(`${specsPath(projectId, specId)}/versions`, body)
    if (!resp?.spec) throw new Error('追加版本失败：后端未返回 spec')
    return resp.spec
  })
}

export function linkSpec(projectId: number, specId: string, body: LinkSpecBody): Promise<SpecLink> {
  return enqueue(async () => {
    const resp = await apiPost<LinkResp>(`${specsPath(projectId, specId)}/links`, body)
    if (!resp?.link) throw new Error('关联失败：后端未返回 link')
    return resp.link
  })
}

export async function listSpecLinks(projectId: number, specId: string): Promise<SpecLink[]> {
  const resp = await apiGet<LinksResp>(`${specsPath(projectId, specId)}/links`)
  return resp?.links ?? []
}

export async function findSpecsByRef(projectId: number, kind: string, ref: string): Promise<SpecLink[]> {
  const resp = await apiGet<LinksResp>(withQuery(`${specsPath(projectId)}/by-ref`, { kind, ref }))
  return resp?.links ?? []
}

export function linkSpecCase(projectId: number, specId: string, body: CaseSpecBody): Promise<SpecCase> {
  return enqueue(async () => {
    const resp = await apiPost<CaseResp>(`${specsPath(projectId, specId)}/cases`, body)
    if (!resp?.case) throw new Error('登记用例失败：后端未返回 case')
    return resp.case
  })
}

export async function getSpecCoverage(projectId: number, specId: string, version?: number): Promise<SpecCoverage> {
  const resp = await apiGet<CoverageResp>(withQuery(`${specsPath(projectId, specId)}/coverage`, { version }))
  if (!resp?.coverage) throw new Error('读取覆盖失败：后端未返回 coverage')
  return resp.coverage
}