/**
 * 联调契约测试：样本来自真实后端 DbAdminController（fdc6e84）——不是手写假设。
 * 生成方式见 __fixtures__/backend-samples.json `_meta` 与提交说明。
 */
import { describe, expect, it } from 'vitest'
import samples from './__fixtures__/backend-samples.json'
import {
  agentEchoesOf, listOf, normApproval, normAudit, normConnection, normHistory, normProvider, normQuery, normTable,
  normTableDetail, normTest, replyToState,
} from './normalize'
import { classifyQueryResponse, errorReason } from './approvalFlow'
import { isTested, providerLabel } from './connForm'
import { hasPlaintextSecret } from './redact'

type Sample = { status: number; body: unknown }
const S = samples as unknown as Record<string, Sample>
const body = (k: string) => S[k].body as Record<string, unknown>

describe('真实后端样本 → normalize', () => {
  it('providers：小写 id + displayName + defaultPort；仅 sqlite 实测', () => {
    const ps = listOf(S.providers.body, normProvider)
    expect(ps.map(p => p.id)).toEqual(['sqlite', 'mysql', 'postgresql', 'sqlserver', 'oracle'])
    expect(ps.find(p => p.id === 'mysql')).toMatchObject({ displayName: 'MySQL / MariaDB', driverAvailable: false, defaultPort: 3306 })
    // oracle：2026-11-06 新增类型；预检关闭（ProbeEnabled=false）后端真值 driverAvailable=true，未实测
    expect(ps.find(p => p.id === 'oracle')).toMatchObject({ displayName: 'Oracle', driverAvailable: true, tested: false, driverPackage: 'Oracle.ManagedDataAccess', defaultPort: 1521 })
    expect(ps.filter(p => isTested(p)).map(p => p.id)).toEqual(['sqlite'])
    expect(providerLabel('postgresql')).toBe('PostgreSQL')
  })
  it('connections：sys:<ConnName> 为系统只读；用户连接数字串 id；密码只有掩码', () => {
    const cs = listOf(S.connections.body, normConnection)
    const sys = cs.find(c => c.id.startsWith('sys:'))!
    expect(sys).toMatchObject({ system: true, readonly: true, provider: 'sqlite', mode: 'form' })
    const users = cs.filter(c => !c.system)
    expect(users.length).toBeGreaterThan(0)
    for (const u of users) expect(u.id).toMatch(/^\d+$/)
    const pg = normConnection(body('createConnection'))
    expect(pg).toMatchObject({ provider: 'postgresql', password: '****', tested: false })
    expect(normConnection(body('createConnStr')).connStr).toBe('Server=h;Uid=u;Pwd=****')
  })
  it('测连恒 200：ok → code null 也判 ok；driver_missing / sql_error', () => {
    for (const k of ['testOk', 'testDriverMissing', 'testSqlError']) expect(S[k].status).toBe(200)
    expect(normTest(body('testOk'))).toMatchObject({ status: 'ok', ok: true, code: 'ok', serverVersion: '3.50.4' })
    const dm = normTest(body('testDriverMissing'))
    expect(dm).toMatchObject({ status: 'driver_missing', ok: false, code: 'driver_missing' })
    expect(dm.message).toContain('MySqlConnector')
    expect(normTest(body('testSqlError'))).toMatchObject({ status: 'error', code: 'sql_error' })
  })
  it('tables / tableDetail：isView、rawType 优先、primaryKey', () => {
    expect(listOf(S.tables.body, normTable)[0]).toEqual({ name: 'items', type: 'table' })
    const d = normTableDetail(body('tableDetail'))
    expect(d.columns[0]).toEqual({ name: 'id', type: 'INTEGER', nullable: false, primaryKey: true })
  })
  it('query 200 ok：columns:string[] + columnTypes；服务端分页；pageSize 被后端 clamp 到 500', () => {
    const st = replyToState(S.queryRead.status, S.queryRead.body)
    expect(st.kind).toBe('result')
    const q = normQuery(body('queryRead'), 200)
    expect(q).toMatchObject({ status: 'ok', kind: 'read', destructive: false, total: 1200, page: 2, pageSize: 3 })
    expect(q.columns).toEqual([{ name: 'id', type: 'Int64' }, { name: 'name', type: 'String' }])
    expect(normQuery(body('queryReadBigPage')).pageSize).toBe(500)
  })
  it('query 202 awaiting_approval：数字 approvalId → 字符串；destructive 独立字段，kind 仍为 write', () => {
    expect(S.queryWrite202.status).toBe(202)
    const w = normQuery(body('queryWrite202'), 202)
    expect(w).toMatchObject({ status: 'awaiting_approval', kind: 'write', destructive: false, approvalId: '1', httpStatus: 202 })
    expect(classifyQueryResponse(w)).toBe('open_approval')
    const d = normQuery(body('queryDestructive202'), 202)
    expect(d).toMatchObject({ status: 'awaiting_approval', kind: 'write', destructive: true })
  })
  it('query 200 + rejected：硬拒 / 只读连接 → 红条，不弹审批', () => {
    for (const k of ['queryHardReject', 'queryReadonlyReject']) {
      expect(S[k].status).toBe(200)
      const st = replyToState(S[k].status, S[k].body)
      expect(st.kind).toBe('result')
      if (st.kind === 'result') expect(classifyQueryResponse(st.result)).toBe('red_bar')
    }
    expect(normQuery(body('queryReadonlyReject')).code).toBe('readonly_connection')
  })
  it('query 非 2xx：400 sql_error / 404 not_found / 424 driver_missing → 错误红条（body 为 QueryOutcome）', () => {
    const cases: [string, number, string][] = [
      ['querySqlError', 400, 'sql_error'], ['queryNotFound', 404, 'not_found'], ['queryDriverMissing424', 424, 'driver_missing'],
    ]
    for (const [k, http, code] of cases) {
      expect(S[k].status).toBe(http)
      const st = replyToState(S[k].status, S[k].body)
      expect(st).toMatchObject({ kind: 'error', error: { code, httpStatus: http } })
    }
    expect(errorReason('driver_missing')).toContain('驱动缺失')
  })
  it('approvals：数组、数字 id、kind read/write + destructive', () => {
    const as = listOf(S.approvalsPending.body, normApproval)
    expect(as.map(a => [a.id, a.kind, a.destructive])).toEqual([['2', 'write', true], ['1', 'write', false]])
    expect(listOf(S.approvalsAfter.body, normApproval)[0].source).toBe('agent')
  })
  it('resolve：409 confirmation_required；approve 200 ok；reject 200 rejected/approval_rejected；400/404', () => {
    expect(S.resolveDestructiveNoConfirm409.status).toBe(409)
    expect(replyToState(409, S.resolveDestructiveNoConfirm409.body)).toMatchObject({ kind: 'error', error: { code: 'confirmation_required' } })
    expect(normQuery(body('resolveApprove'), 200)).toMatchObject({ status: 'ok', affectedRows: 1, approvalId: '1' })
    const rj = normQuery(body('resolveReject'), 200)
    expect(rj).toMatchObject({ status: 'rejected', code: 'approval_rejected', destructive: true })
    expect(normQuery(body('resolveDestructiveConfirmed'), 200)).toMatchObject({ status: 'ok', destructive: true })
    expect(replyToState(400, S.resolveBadDecision400.body)).toMatchObject({ error: { code: 'invalid_request' } })
    expect(replyToState(400, S.resolveAgain400.body)).toMatchObject({ error: { code: 'invalid_request' } })
    expect(replyToState(404, S.resolveMissing404.body)).toMatchObject({ error: { code: 'not_found' } })
  })
  it('history：分页对象；数字 id；成功时 error="" 归一为 undefined', () => {
    const hs = listOf(S.history.body, normHistory)
    expect(hs[0].id).toMatch(/^\d+$/)
    expect(hs.every(h => h.kind === 'read' || h.kind === 'write')).toBe(true)
    expect(hs.find(h => h.success)?.error).toBeUndefined()
    expect(listOf(S.historyAgent.body, normHistory).every(h => h.source === 'agent')).toBe(true)
  })
  it('S8：/audit?kind=agent → AgentToolEcho（tool/status/rowCount/truncated/approvalId/sessionId）', () => {
    const es = agentEchoesOf(S.auditAgent.body)
    expect(es.map(e => [e.tool, e.status])).toEqual([
      ['db_query_readonly', 'rejected'], ['db_execute_write', 'awaiting_approval'], ['db_query_readonly', 'ok'],
    ])
    expect(es[2]).toMatchObject({ rowCount: 1000, truncated: true, sessionId: 'sess-1', sqlKind: 'read' })
    expect(es[1].approvalId).toMatch(/^\d+$/)
    expect(es[0].message).toContain('只接受')
    // 非 agent 审计不进回显
    const all = listOf(S.audit.body, normAudit)
    expect(agentEchoesOf(S.audit.body).length).toBe(all.filter(a => a.kind === 'agent').length)
    expect(agentEchoesOf(S.auditAgentSession.body).map(e => e.status)).toEqual(['awaiting_approval'])
  })
  it('audit / history 分页上限 500；permission-mode {mode, default}', () => {
    expect(body('auditBigPage').pageSize).toBe(500)
    expect(body('modeGet')).toEqual({ mode: 'AskWrite', default: 'AskWrite' })
    expect(S.modeBad.status).toBe(400)
    expect(S.deleteConn.status).toBe(204)
  })
  it('样本无明文密钥', () => {
    expect(hasPlaintextSecret(JSON.stringify(samples))).toBe(false)
  })
})
