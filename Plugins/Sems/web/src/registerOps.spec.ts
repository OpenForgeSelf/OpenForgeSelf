import { describe, expect, it } from 'vitest'
import { alreadyRegisteredProjectMessage, sameProjectRoot } from './registerOps'

describe('F03 同一路径提示', () => {
  it('文案明确写「已存在并可打开原项目」，不说已导入', () => {
    expect(alreadyRegisteredProjectMessage('演示')).toBe('「演示」已存在并可打开原项目')
    expect(alreadyRegisteredProjectMessage('  ')).toBe('已存在并可打开原项目')
    expect(alreadyRegisteredProjectMessage()).toBe('已存在并可打开原项目')
    expect(alreadyRegisteredProjectMessage('演示')).not.toMatch(/已导入/)
  })

  it('Linux 路径按大小写区分，去掉末尾斜杠后仍视为同一目录', () => {
    expect(sameProjectRoot('/tmp/App', '/tmp/App/')).toBe(true)
    expect(sameProjectRoot('/tmp/App', '/tmp/app')).toBe(false)
  })

  it('Windows 路径统一斜杠且忽略大小写', () => {
    expect(sameProjectRoot('D:\\verify', 'D:/verify/')).toBe(true)
    expect(sameProjectRoot('D:/Verify', 'd:/verify')).toBe(true)
  })
})
