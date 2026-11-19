/** 项目列表必须露出的档案字段（F01）。空值用明确占位，不靠「有才显示」。 */

export interface ProjectFactInput {
  type?: string
  tags?: string
  description?: string
  path?: string
  source?: string
  isGitRepo?: boolean
  pathExists?: boolean
}

export interface ProjectListFacts {
  type: string
  tags: string
  description: string
  root: string
  source: string
  git: string
  path: string
}

export function projectListFacts(project: ProjectFactInput): ProjectListFacts {
  return {
    type: textOr(project.type, '未分类'),
    tags: textOr(project.tags, '无'),
    description: textOr(project.description, '无'),
    root: textOr(project.path, '未记录'),
    source: textOr(project.source, '未记录'),
    git: project.isGitRepo ? 'git 仓库' : '非 git 仓库',
    path: project.pathExists ? '路径可达' : '目录不可达',
  }
}

function textOr(value: string | undefined, fallback: string): string {
  const text = value?.trim()
  return text ? text : fallback
}
