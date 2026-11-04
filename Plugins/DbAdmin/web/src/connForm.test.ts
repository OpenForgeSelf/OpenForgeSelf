import { describe, expect, it } from 'vitest'
import { buildConnStr, emptyForm, formFromConnection, isTested, toSubmitBody, validateForm } from './connForm'

describe('连接表单', () => {
  it('SQLite 需要文件路径', () => {
    const f = { ...emptyForm('SQLite'), name: 'demo' }
    expect(validateForm(f, false)).toBe('请填写 SQLite 文件路径')
    expect(validateForm({ ...f, database: '/data/demo.db' }, false)).toBeNull()
  })
  it('名称必填；非 SQLite 需要主机/库名/合法端口', () => {
    expect(validateForm(emptyForm('MySql'), false)).toBe('请填写连接名称')
    const f = { ...emptyForm('MySql'), name: 'm' }
    expect(validateForm(f, false)).toBe('请填写主机')
    expect(validateForm({ ...f, host: 'h', port: 70000 }, false)).toBe('端口应为 1–65535')
    expect(validateForm({ ...f, host: 'h', database: 'd' }, false)).toBeNull()
  })
  it('连接串模式：新建必填，编辑可留空', () => {
    const f = { ...emptyForm('PostgreSQL'), name: 'p', mode: 'connstr' as const }
    expect(validateForm(f, false)).toBe('请填写连接串')
    expect(validateForm(f, true)).toBeNull()
  })
  it('仅 SQLite 实测', () => {
    expect(isTested({ id: 'SQLite' })).toBe(true)
    expect(isTested({ id: 'MySql' })).toBe(false)
    expect(isTested({ id: 'MySql', tested: true })).toBe(true)
  })
  it('编辑：密码不回填；空密码提交时不带 password（= 不改）', () => {
    const f = formFromConnection({ id: 'c1', name: 'm', provider: 'MySql', mode: 'form', host: 'h', database: 'd', userName: 'u', password: '****', readonly: false })
    expect(f.password).toBe('')
    expect('password' in toSubmitBody(f, true)).toBe(false)
    expect(toSubmitBody({ ...f, password: 'x' }, true).password).toBe('x')
  })
  it('表单 → 连接串', () => {
    expect(buildConnStr({ ...emptyForm('SQLite'), database: '/a.db' })).toBe('Data Source=/a.db')
    expect(buildConnStr({ ...emptyForm('MySql'), host: 'h', database: 'd', userName: 'u' })).toBe('Server=h;Port=3306;Database=d;User Id=u')
  })
})
