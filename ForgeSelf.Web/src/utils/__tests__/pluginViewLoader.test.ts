/**
 * 插件界面远程加载器单元测试（specs/010-plugin-frontend-runtime T010）。
 *
 * 覆盖：资源 URL 拼装、按「插件 id + 版本」缓存、版本变化触发重新加载、缓存清理。
 * 说明：异步组件对远程 URL 的真实解析依赖浏览器 import map 与后端资源服务，
 * 属于端到端范畴（由 E2E 覆盖），此处只验证不依赖网络的纯逻辑部分。
 * 文件名用 .test.ts 而非 .spec.ts：vitest 配置排除了 spec.ts 后缀（那是 Playwright e2e 的约定）。
 */

import { describe, expect, it, beforeEach } from 'vitest'
import {
  buildPluginAssetUrl,
  buildPluginStyleUrl,
  clearPluginViewCache,
  getPluginViewCacheSize,
  loadPluginView,
} from '@/utils/pluginViewLoader'

describe('buildPluginAssetUrl', () => {
  it('按 /plugins/{插件id}/{入口} 拼装资源地址', () => {
    expect(buildPluginAssetUrl('ai-agent', 'frontend/index.js')).toBe(
      '/plugins/ai-agent/frontend/index.js'
    )
  })

  it('带版本时附加 ?v={version} 缓存标识', () => {
    expect(buildPluginAssetUrl('ai-agent', 'frontend/index.js', '1.2.0')).toBe(
      '/plugins/ai-agent/frontend/index.js?v=1.2.0'
    )
  })

  it('入口路径的前导斜杠被归一化，避免出现双斜杠', () => {
    expect(buildPluginAssetUrl('ai-agent', '/frontend/index.js', '2.0.0')).toBe(
      '/plugins/ai-agent/frontend/index.js?v=2.0.0'
    )
  })

  it('对版本号做编码，避免非法字符破坏 URL', () => {
    expect(buildPluginAssetUrl('ai-agent', 'frontend/index.js', '1.0.0+build 1')).toBe(
      '/plugins/ai-agent/frontend/index.js?v=1.0.0%2Bbuild%201'
    )
  })
})

describe('buildPluginStyleUrl (N+5 缓存击穿修复)', () => {
  it('从入口 URL 同目录推导 style.css', () => {
    expect(buildPluginStyleUrl('/plugins/ai-agent/web/dist/index.js?v=1.4.3')).toBe(
      '/plugins/ai-agent/web/dist/style.css'
    )
  })

  it('带版本时样式 URL 同样附加 ?v= 缓存标识（修复旧实现丢弃查询串）', () => {
    expect(buildPluginStyleUrl('/plugins/ai-agent/web/dist/index.js?v=1.4.3', '1.4.3')).toBe(
      '/plugins/ai-agent/web/dist/style.css?v=1.4.3'
    )
  })

  it('无版本时样式 URL 不带查询串', () => {
    expect(buildPluginStyleUrl('/plugins/ai-agent/web/dist/index.js')).toBe(
      '/plugins/ai-agent/web/dist/style.css'
    )
  })

  it('内容指纹变化时样式 URL 随之变化，确保刷新取到新 CSS', () => {
    const v1 = buildPluginStyleUrl('/plugins/ai-agent/web/dist/index.js', 'abc123')
    const v2 = buildPluginStyleUrl('/plugins/ai-agent/web/dist/index.js', 'def456')
    expect(v1).toBe('/plugins/ai-agent/web/dist/style.css?v=abc123')
    expect(v2).toBe('/plugins/ai-agent/web/dist/style.css?v=def456')
    expect(v1).not.toBe(v2)
  })
})

describe('loadPluginView 缓存行为', () => {
  beforeEach(() => {
    clearPluginViewCache()
  })

  it('同一插件同一版本复用同一个组件实例（不重复加载）', () => {
    const options = {
      pluginId: 'ai-agent',
      pluginName: 'AI代理插件',
      version: '1.0.0',
      entryUrl: '/plugins/ai-agent/frontend/index.js?v=1.0.0',
      exportName: 'AiAgentView',
    }

    const first = loadPluginView(options)
    const second = loadPluginView(options)

    expect(first).toBe(second)
    expect(getPluginViewCacheSize()).toBe(1)
  })

  it('版本变化时缓存键变化，返回新的组件实例（FR-008）', () => {
    const base = {
      pluginId: 'ai-agent',
      pluginName: 'AI代理插件',
      entryUrl: '/plugins/ai-agent/frontend/index.js',
      exportName: 'AiAgentView',
    }

    const v1 = loadPluginView({ ...base, version: '1.0.0' })
    const v2 = loadPluginView({ ...base, version: '1.1.0' })

    expect(v1).not.toBe(v2)
    expect(getPluginViewCacheSize()).toBe(2)
  })

  it('不同插件互不干扰', () => {
    const base = { version: '1.0.0', exportName: 'XView', entryUrl: '/plugins/x/frontend/index.js' }

    loadPluginView({ ...base, pluginId: 'plugin-a' })
    loadPluginView({ ...base, pluginId: 'plugin-b' })

    expect(getPluginViewCacheSize()).toBe(2)
  })

  it('clearPluginViewCache 清空全部缓存', () => {
    loadPluginView({
      pluginId: 'ai-agent',
      version: '1.0.0',
      entryUrl: '/plugins/ai-agent/frontend/index.js',
      exportName: 'AiAgentView',
    })
    expect(getPluginViewCacheSize()).toBe(1)

    clearPluginViewCache()
    expect(getPluginViewCacheSize()).toBe(0)
  })
})
