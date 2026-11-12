import { describe, expect, it } from 'vitest'
import { SqlSplitError, splitExecutable, splitStatements } from './sqlSplit'

describe('多语句切分（AC-20 / BR-13~14）', () => {
  it('顶层分号切分，去掉空片段', () => {
    expect(splitExecutable('select 1; select 2;')).toEqual(['select 1', 'select 2'])
    expect(splitExecutable('  ;  ; ')).toEqual([])
  })

  it('字符串里的分号不切（含双写引号转义）', () => {
    expect(splitExecutable("select 'a;b'; select 2")).toEqual(["select 'a;b'", 'select 2'])
    expect(splitExecutable("select 'it''s;ok' from t; select 2")).toEqual(["select 'it''s;ok' from t", 'select 2'])
  })

  it('E 前缀字符串的反斜杠转义不切', () => {
    expect(splitExecutable("select E'a\\';b' ; select 2")).toEqual(["select E'a\\';b'", 'select 2'])
  })

  it('引号标识符内的分号不切（"…"、`…`、[…]）', () => {
    expect(splitExecutable('select "a;b" from t; select 2')).toEqual(['select "a;b" from t', 'select 2'])
    expect(splitExecutable('select `a;b` from t; select 2')).toEqual(['select `a;b` from t', 'select 2'])
    expect(splitExecutable('select [a;b] from t; select 2')).toEqual(['select [a;b] from t', 'select 2'])
  })

  it('行注释 / 块注释中的分号不切', () => {
    expect(splitExecutable('select 1 -- x;y\n; select 2')).toEqual(['select 1 -- x;y', 'select 2'])
    expect(splitExecutable('select 1 /* x;y */; select 2')).toEqual(['select 1 /* x;y */', 'select 2'])
  })

  it('美元引号内的分号不切', () => {
    expect(splitExecutable('select $tag$a;b$tag$; select 2')).toEqual(['select $tag$a;b$tag$', 'select 2'])
  })

  it('CREATE TRIGGER 的 BEGIN…END 块内分号不切', () => {
    const sql = 'CREATE TRIGGER trg AFTER INSERT ON t BEGIN INSERT INTO u VALUES (1); INSERT INTO v VALUES (2); END; select 1'
    expect(splitExecutable(sql)).toHaveLength(2)
    expect(splitExecutable(sql)[0]).toContain('INSERT INTO v VALUES (2); END')
  })

  it('触发器里的 CASE…END 正确配对，不误判块结束', () => {
    const sql = 'CREATE TEMP TRIGGER trg BEGIN UPDATE t SET a = CASE WHEN b THEN 1 ELSE 2 END; DELETE FROM t; END; select 1'
    expect(splitExecutable(sql)).toHaveLength(2)
  })

  it('非触发器语句（含 BEGIN TRANSACTION）照常按分号切', () => {
    expect(splitExecutable('BEGIN TRANSACTION; select 1')).toEqual(['BEGIN TRANSACTION', 'select 1'])
  })

  it('纯注释片段被丢弃', () => {
    const r = splitStatements('-- only comment\n; select 1; /* c */')
    expect(r).toHaveLength(1)
    expect(r[0].hasCode).toBe(true)
    expect(splitExecutable('-- only comment\n; select 1; /* c */')).toEqual(['select 1'])
  })

  it('未闭合引号 / 块注释 / 美元引号抛错（调用方不发请求）', () => {
    expect(() => splitStatements("select 'abc")).toThrow(SqlSplitError)
    expect(() => splitStatements('select 1 /* abc')).toThrow(SqlSplitError)
    expect(() => splitStatements('select $tag$abc')).toThrow(SqlSplitError)
    expect(() => splitStatements('select "abc')).toThrow(SqlSplitError)
  })

  it('原文保留用户写的引号与注释', () => {
    const r = splitStatements("select 'a;b' -- note\n; select 2")
    expect(r[0].text).toContain("'a;b'")
    expect(r[0].text).toContain('-- note')
  })
})
