import { describe, expect, it } from 'vitest'
import { buildAliasSuggestion } from '@/utils/modelAlias'

describe('buildAliasSuggestion', () => {
  it('normalizes separators and truncates to the public alias limit', () => {
    expect(buildAliasSuggestion('Qwen/Qwen2.5-72B')).toBe('qwen-qwen2.5-72b')
    expect(buildAliasSuggestion('--model///name--')).toBe('model-name')
    expect(buildAliasSuggestion('x'.repeat(70))).toHaveLength(64)
  })
})
