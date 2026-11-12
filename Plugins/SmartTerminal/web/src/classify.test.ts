import { describe, expect, it } from 'vitest'
import { classifyRisk, decide } from './classify'

describe('permission gate (AskWrite default)', () => {
  it('allows read, asks write and high-risk, hard-rejects destructive roots', () => {
    expect(classifyRisk('git status -sb')).toBe('read')
    expect(decide('read', 'AskWrite')).toBe('allow')
    expect(classifyRisk('sudo nginx -s reload')).toBe('write')
    expect(decide('write', 'AskWrite')).toBe('ask')
    expect(classifyRisk('rm -rf /tmp/st-demo-orphan')).toBe('high')
    expect(decide('high', 'AskWrite')).toBe('ask')
    expect(classifyRisk('rm -rf /')).toBe('deny')
    expect(decide('deny', 'AskWrite')).toBe('deny')
  })

  it('Auto allows write but still hard-rejects; AskHighRisk only asks for high', () => {
    expect(decide('write', 'Auto')).toBe('allow')
    expect(decide('high', 'Auto')).toBe('allow')
    expect(decide('deny', 'Auto')).toBe('deny')
    expect(decide('write', 'AskHighRisk')).toBe('allow')
    expect(decide('high', 'AskHighRisk')).toBe('ask')
  })
})
