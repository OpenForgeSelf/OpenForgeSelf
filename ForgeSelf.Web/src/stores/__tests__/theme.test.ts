import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import {
  ROOT_THEME_ATTRIBUTE,
  ROOT_THEME_MODE_ATTRIBUTE,
  THEME_STORAGE_KEY,
  useThemeStore,
} from '../theme'

interface MatchMediaMock {
  matches: boolean
  media: string
  addEventListener: ReturnType<typeof vi.fn>
  removeEventListener: ReturnType<typeof vi.fn>
  dispatch: (matches: boolean) => void
}

function createMatchMediaMock(initialMatches = false): MatchMediaMock {
  const listeners = new Set<(event: MediaQueryListEvent) => void>()

  const mock: MatchMediaMock = {
    matches: initialMatches,
    media: '(prefers-color-scheme: dark)',
    addEventListener: vi.fn((type: string, listener: EventListenerOrEventListenerObject) => {
      if (type === 'change' && typeof listener === 'function') {
        listeners.add(listener as (event: MediaQueryListEvent) => void)
      }
    }),
    removeEventListener: vi.fn((type: string, listener: EventListenerOrEventListenerObject) => {
      if (type === 'change' && typeof listener === 'function') {
        listeners.delete(listener as (event: MediaQueryListEvent) => void)
      }
    }),
    dispatch(matches: boolean) {
      mock.matches = matches
      const event = { matches, media: mock.media } as MediaQueryListEvent
      listeners.forEach((listener) => listener(event))
    },
  }

  return mock
}

describe('Theme Store', () => {
  let matchMediaMock: MatchMediaMock

  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.documentElement.removeAttribute(ROOT_THEME_ATTRIBUTE)
    document.documentElement.removeAttribute(ROOT_THEME_MODE_ATTRIBUTE)
    document.documentElement.style.colorScheme = ''

    matchMediaMock = createMatchMediaMock(false)
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn(() => matchMediaMock),
    })
  })

  afterEach(() => {
    const store = useThemeStore()
    store.dispose()
  })

  it('initialize 应将根节点主题标记设置为系统解析结果', () => {
    const store = useThemeStore()

    store.initialize()

    expect(store.mode).toBe('system')
    expect(store.resolvedTheme).toBe('light')
    expect(document.documentElement.getAttribute(ROOT_THEME_ATTRIBUTE)).toBe('light')
    expect(document.documentElement.getAttribute(ROOT_THEME_MODE_ATTRIBUTE)).toBe('system')
  })

  it('setMode 应同步更新主题标记和本地持久化值', () => {
    const store = useThemeStore()

    store.initialize()
    store.setMode('dark')

    expect(store.mode).toBe('dark')
    expect(store.resolvedTheme).toBe('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')
    expect(document.documentElement.getAttribute(ROOT_THEME_ATTRIBUTE)).toBe('dark')
    expect(document.documentElement.getAttribute(ROOT_THEME_MODE_ATTRIBUTE)).toBe('dark')
    expect(document.documentElement.style.colorScheme).toBe('dark')
  })

  it('initialize 应恢复上次持久化的主题模式', () => {
    const firstStore = useThemeStore()

    firstStore.initialize()
    firstStore.setMode('dark')
    firstStore.dispose()

    setActivePinia(createPinia())
    const secondStore = useThemeStore()
    secondStore.initialize()

    expect(secondStore.mode).toBe('dark')
    expect(secondStore.resolvedTheme).toBe('dark')
    expect(document.documentElement.getAttribute(ROOT_THEME_ATTRIBUTE)).toBe('dark')
    expect(document.documentElement.getAttribute(ROOT_THEME_MODE_ATTRIBUTE)).toBe('dark')
  })

  it('system 模式下应在系统主题变化时同步更新', () => {
    const store = useThemeStore()

    store.initialize()
    store.setMode('system')
    matchMediaMock.dispatch(true)

    expect(store.mode).toBe('system')
    expect(store.resolvedTheme).toBe('dark')
    expect(document.documentElement.getAttribute(ROOT_THEME_ATTRIBUTE)).toBe('dark')
    expect(document.documentElement.getAttribute(ROOT_THEME_MODE_ATTRIBUTE)).toBe('system')

    matchMediaMock.dispatch(false)

    expect(store.resolvedTheme).toBe('light')
    expect(document.documentElement.getAttribute(ROOT_THEME_ATTRIBUTE)).toBe('light')
  })
})
