import { describe, expect, it } from 'vitest'
import { historyLine, historyLines } from './history'

describe('historyLines', () => {
  it('reads command from BlockDto objects instead of [object Object]', () => {
    expect(historyLine({ id: 'b-1', seq: 2, kind: 'command', command: 'git status -sb' })).toBe('git status -sb')
    expect(historyLine({ CommandRedacted: 'sudo nginx -s reload' })).toBe('sudo nginx -s reload')
    expect(historyLine('df -h')).toBe('df -h')
    expect(historyLine({ id: 'b-x' })).toBeNull()
    expect(String({ command: 'git status -sb' })).toBe('[object Object]')
    expect(
      historyLines({
        items: [
          { command: 'git status -sb' },
          { command: 'npm test' },
          { kind: 'system', command: '' },
        ],
      }),
    ).toEqual(['git status -sb', 'npm test'])
  })
})
