import { describe, expect, it } from 'vitest'
import { projectListFacts } from './projectFacts'

describe('F01 项目列表字段', () => {
  it('空档案也写出类型、标签、描述、来源、非 git、目录不可达', () => {
    expect(projectListFacts({})).toEqual({
      type: '未分类',
      tags: '无',
      description: '无',
      root: '未记录',
      source: '未记录',
      git: '非 git 仓库',
      path: '目录不可达',
    })
  })

  it('有值时原样展示，git 与路径可达分开写', () => {
    expect(projectListFacts({
      type: 'frontend',
      tags: 'vue',
      description: '说明',
      path: '/tmp/p',
      source: 'ai-agent',
      isGitRepo: true,
      pathExists: true,
    })).toEqual({
      type: 'frontend',
      tags: 'vue',
      description: '说明',
      root: '/tmp/p',
      source: 'ai-agent',
      git: 'git 仓库',
      path: '路径可达',
    })
  })
})
