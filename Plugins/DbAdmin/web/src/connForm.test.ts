import { describe, expect, it } from 'vitest'
import { buildConnStr, emptyForm, formFromConnection, isTested, toSubmitBody, validateForm } from './connForm'

describe('连接表单', () => {
  it('SQLite 需要文件路径', () => {
    const f = { ...emptyForm('sqlite'), name: 'demo' }
    expect(validateForm(f, false)).toBe('请填写 SQLite 文件路径')
    expect(validateForm({ ...f, database: '/data/demo.db' }, false)).toBeNull()
  })
  it('名称必填；非 SQLite 需要主机/库名/合法端口', () => {
    expect(validateForm(emptyForm('mysql'), false)).toBe('请填写连接名称')
    const f = { ...emptyForm('mysql'), name: 'm' }
    expect(validateForm(f, false)).toBe('请填写主机')
    expect(validateForm({ ...f, host: 'h', port: 70000 }, false)).toBe('端口应为 1–65535')
    expect(validateForm({ ...f, host: 'h', database: 'd' }, false)).toBeNull()
  })
  it('连接串模式：新建必填，编辑可留空', () => {
    const f = { ...emptyForm('postgresql'), name: 'p', mode: 'connstr' as const }
    expect(validateForm(f, false)).toBe('请填写连接串')
    expect(validateForm(f, true)).toBeNull()
  })
  it('仅 SQLite 实测', () => {
    expect(isTested({ id: 'sqlite' })).toBe(true)
    expect(isTested({ id: 'mysql' })).toBe(false)
    expect(isTested({ id: 'mysql', tested: true })).toBe(true)
  })
  it('编辑：密码不回填；空密码提交时不带 password（= 不改）', () => {
    const f = formFromConnection({ id: '12', name: 'm', provider: 'mysql', mode: 'form', host: 'h', database: 'd', userName: 'u', password: '****', readonly: false })
    expect(f.password).toBe('')
    expect('password' in toSubmitBody(f, true)).toBe(false)
    expect(toSubmitBody({ ...f, password: 'x' }, true).password).toBe('x')
  })
  it('表单 → 连接串', () => {
    expect(buildConnStr({ ...emptyForm('sqlite'), database: '/a.db' })).toBe('Data Source=/a.db')
    expect(buildConnStr({ ...emptyForm('mysql'), host: 'h', database: 'd', userName: 'u' })).toBe('Server=h;Port=3306;Database=d;User Id=u')
  })
  it('默认表是本机偏好：不进请求体（AC-12 机械守卫）', () => {
    const f = { ...emptyForm('sqlite'), name: 'demo', database: '/d.db', defaultTable: 'users' }
    expect('defaultTable' in toSubmitBody(f, false)).toBe(false)
    expect('defaultTable' in toSubmitBody(f, true)).toBe(false)
    expect(toSubmitBody(f, false)).toEqual({ name: 'demo', provider: 'sqlite', mode: 'form', database: '/d.db' })
    expect(formFromConnection({ id: '1', name: 'd', provider: 'sqlite', mode: 'form', readonly: false }, 'orders').defaultTable).toBe('orders')
  })
})

describe('provider 小写契约', () => {
  it('默认端口 / 展示名：后端 providers 优先，缺省走前端映射', async () => {
    const { defaultPort, providerLabel } = await import('./connForm')
    expect(defaultPort('postgresql')).toBe(5432)
    expect(defaultPort('MySQL')).toBe(3306)
    expect(defaultPort('mysql', [{ id: 'mysql', defaultPort: 3307 }])).toBe(3307)
    expect(providerLabel('sqlserver')).toBe('SQL Server')
    expect(providerLabel('sqlite', [{ id: 'sqlite', displayName: 'SQLite（实测）' }])).toBe('SQLite（实测）')
    expect(emptyForm('SQLite').provider).toBe('sqlite')
  })
})
