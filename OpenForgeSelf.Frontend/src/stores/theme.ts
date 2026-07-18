import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

export type ThemeMode = 'light' | 'dark' | 'system'
export type ResolvedTheme = 'light' | 'dark'

export const THEME_STORAGE_KEY = 'forgeself.theme.mode'
export const ROOT_THEME_ATTRIBUTE = 'data-theme'
export const ROOT_THEME_MODE_ATTRIBUTE = 'data-theme-mode'

const SYSTEM_THEME_MEDIA_QUERY = '(prefers-color-scheme: dark)'

type MediaQueryChangeListener = (event: MediaQueryListEvent) => void
type MediaQueryListLike = Pick<MediaQueryList, 'matches'> &
  Partial<Pick<MediaQueryList, 'addEventListener' | 'removeEventListener' | 'addListener' | 'removeListener'>>

function isThemeMode(value: string | null): value is ThemeMode {
  return value === 'light' || value === 'dark' || value === 'system'
}

function getStorage(): Storage | null {
  if (typeof window === 'undefined') {
    return null
  }

  try {
    return window.localStorage
  } catch {
    return null
  }
}

function getMediaQueryList(): MediaQueryListLike | null {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
    return null
  }

  return window.matchMedia(SYSTEM_THEME_MEDIA_QUERY)
}

function addMediaQueryListener(mediaQueryList: MediaQueryListLike, listener: MediaQueryChangeListener): void {
  if (typeof mediaQueryList.addEventListener === 'function') {
    mediaQueryList.addEventListener('change', listener)
    return
  }

  if (typeof mediaQueryList.addListener === 'function') {
    mediaQueryList.addListener(listener)
  }
}

function removeMediaQueryListener(mediaQueryList: MediaQueryListLike, listener: MediaQueryChangeListener): void {
  if (typeof mediaQueryList.removeEventListener === 'function') {
    mediaQueryList.removeEventListener('change', listener)
    return
  }

  if (typeof mediaQueryList.removeListener === 'function') {
    mediaQueryList.removeListener(listener)
  }
}

export function resolveThemeMode(mode: ThemeMode, prefersDark: boolean): ResolvedTheme {
  if (mode === 'system') {
    return prefersDark ? 'dark' : 'light'
  }

  return mode
}

export function readStoredThemeMode(storage: Storage | null = getStorage()): ThemeMode {
  const storedMode = storage?.getItem(THEME_STORAGE_KEY) ?? null
  return isThemeMode(storedMode) ? storedMode : 'system'
}

export function applyThemeToRoot(
  mode: ThemeMode,
  resolvedTheme: ResolvedTheme,
  root: HTMLElement = document.documentElement,
): void {
  root.setAttribute(ROOT_THEME_ATTRIBUTE, resolvedTheme)
  root.setAttribute(ROOT_THEME_MODE_ATTRIBUTE, mode)
  root.style.colorScheme = resolvedTheme
}

export const useThemeStore = defineStore('theme', () => {
  const mode = ref<ThemeMode>('system')
  const resolvedTheme = ref<ResolvedTheme>('light')
  const isInitialized = ref(false)

  let mediaQueryList: MediaQueryListLike | null = null
  let mediaQueryListener: MediaQueryChangeListener | null = null

  const isDark = computed(() => resolvedTheme.value === 'dark')

  function updateResolvedTheme(): void {
    mediaQueryList = mode.value === 'system' ? getMediaQueryList() : null
    resolvedTheme.value = resolveThemeMode(mode.value, mediaQueryList?.matches ?? false)
    applyThemeToRoot(mode.value, resolvedTheme.value)
  }

  function stopSystemSync(): void {
    if (mediaQueryList && mediaQueryListener) {
      removeMediaQueryListener(mediaQueryList, mediaQueryListener)
    }

    mediaQueryListener = null
  }

  function startSystemSync(): void {
    stopSystemSync()

    if (mode.value !== 'system') {
      mediaQueryList = null
      return
    }

    mediaQueryList = getMediaQueryList()
    if (!mediaQueryList) {
      return
    }

    mediaQueryListener = (event: MediaQueryListEvent) => {
      if (mode.value !== 'system') {
        return
      }

      resolvedTheme.value = event.matches ? 'dark' : 'light'
      applyThemeToRoot(mode.value, resolvedTheme.value)
    }

    addMediaQueryListener(mediaQueryList, mediaQueryListener)
  }

  function persistMode(nextMode: ThemeMode): void {
    const storage = getStorage()
    storage?.setItem(THEME_STORAGE_KEY, nextMode)
  }

  function setMode(nextMode: ThemeMode): void {
    mode.value = nextMode
    persistMode(nextMode)
    updateResolvedTheme()
    startSystemSync()
  }

  function initialize(): void {
    mode.value = readStoredThemeMode()
    updateResolvedTheme()
    startSystemSync()
    isInitialized.value = true
  }

  function dispose(): void {
    stopSystemSync()
    mediaQueryList = null
    isInitialized.value = false
  }

  return {
    mode,
    resolvedTheme,
    isDark,
    isInitialized,
    initialize,
    setMode,
    dispose,
  }
})
