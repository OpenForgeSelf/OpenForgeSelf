import { describe, expect, it } from 'vitest'
import {
  classifyEngineeringPlugin,
  filterEngineeringPlugins,
  parseEngineeringPluginsPayload,
} from './engineeringPlugins'

describe('classifyEngineeringPlugin', () => {
  it('recognizes frontend tooling and drops eslint-config packages', () => {
    expect(classifyEngineeringPlugin('@vitejs/plugin-vue')).toBe('构建')
    expect(classifyEngineeringPlugin('vite-plugin-dts')).toBe('构建')
    expect(classifyEngineeringPlugin('foo-vite-plugin')).toBe('构建')
    expect(classifyEngineeringPlugin('rollup-plugin-visualizer')).toBe('构建')
    expect(classifyEngineeringPlugin('unplugin-auto-import')).toBe('构建')
    expect(classifyEngineeringPlugin('html-webpack-plugin')).toBe('构建')
    expect(classifyEngineeringPlugin('webpack-plugin-serve')).toBe('构建')
    expect(classifyEngineeringPlugin('@babel/plugin-proposal-class-properties')).toBe('转译')
    expect(classifyEngineeringPlugin('babel-plugin-import')).toBe('转译')
    expect(classifyEngineeringPlugin('eslint-plugin-vue')).toBe('代码质量')
    expect(classifyEngineeringPlugin('@typescript-eslint/eslint-plugin')).toBe('代码质量')
    expect(classifyEngineeringPlugin('prettier-plugin-tailwindcss')).toBe('代码质量')
    expect(classifyEngineeringPlugin('@vue/cli-plugin-babel')).toBe('框架')
    expect(classifyEngineeringPlugin('vue-cli-plugin-stylelint')).toBe('框架')
    expect(classifyEngineeringPlugin('eslint-config-prettier')).toBe(null)
    expect(classifyEngineeringPlugin('@vue/eslint-config-typescript')).toBe(null)
    expect(classifyEngineeringPlugin('vue')).toBe(null)
    expect(classifyEngineeringPlugin('sems_list_projects')).toBe(null)
  })
})

describe('parseEngineeringPluginsPayload', () => {
  it('maps API rows, fills group, and never keeps sems_* tools', () => {
    const { plugins, error } = parseEngineeringPluginsPayload({
      plugins: [
        { name: '@vitejs/plugin-vue', version: '^5.2.1' },
        { name: 'sems_list_projects', version: '1.3.0', group: '项目档案' },
        { name: 'eslint-plugin-vue', declaredVersion: '9.0.0' },
      ],
    })
    expect(error).toBe('')
    expect(plugins.map((p) => p.name)).toEqual(['@vitejs/plugin-vue', 'eslint-plugin-vue'])
    expect(plugins[0].group).toBe('构建')
    expect(plugins[0].source).toBe('package.json')
    expect(plugins[1].version).toBe('9.0.0')
  })

  it('keeps scan failure reason when the list is empty', () => {
    const { plugins, error } = parseEngineeringPluginsPayload({
      plugins: [],
      error: 'package.json 不是合法 JSON',
    })
    expect(plugins).toEqual([])
    expect(error).toBe('package.json 不是合法 JSON')
  })
})

describe('filterEngineeringPlugins', () => {
  const items = [
    { name: '@vitejs/plugin-vue', version: '5.2.1', group: '构建' as const, source: 'package.json' },
    { name: 'eslint-plugin-vue', version: '9.0.0', group: '代码质量' as const, source: 'package.json' },
  ]

  it('filters by name or group', () => {
    expect(filterEngineeringPlugins(items, 'vite').map((p) => p.name)).toEqual(['@vitejs/plugin-vue'])
    expect(filterEngineeringPlugins(items, '代码').map((p) => p.name)).toEqual(['eslint-plugin-vue'])
  })

  it('returns empty when nothing matches, so the page can show 没有匹配的插件', () => {
    expect(filterEngineeringPlugins(items, 'sems_')).toEqual([])
  })
})
