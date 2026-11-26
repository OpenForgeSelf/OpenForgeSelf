import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

export type ThemeMode = 'light' | 'dark'
export type ResolvedTheme = 'light' | 'dark'

export const THEME_STORAGE_KEY = 'forgeself.theme.mode'
export const THEME_FOLLOW_SYSTEM_KEY = 'forgeself.theme.followSystem'
export const ROOT_THEME_ATTRIBUTE = 'data-theme'
export const ROOT_THEME_MODE_ATTRIBUTE = 'data-theme-mode'

const SYSTEM_THEME_MEDIA_QUERY = '(prefers-color-scheme: dark)'

type MediaQueryChangeListener = (event: MediaQueryListEvent) => void
type MediaQueryListLike = Pick<MediaQueryList, 'matches'> &
  Partial<Pick<MediaQueryList, 'addEventListener' | 'removeEventListener' | 'addListener' | 'removeListener'>>

function isThemeMode(value: string | null): value is ThemeMode {
  return value === 'light' || value === 'dark'
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
  void prefersDark
  return mode
}

export function readStoredThemeMode(storage: Storage | null = getStorage()): ThemeMode {
  const storedMode = storage?.getItem(THEME_STORAGE_KEY) ?? null
  return isThemeMode(storedMode) ? storedMode : 'light'
}

export function readFollowSystem(storage: Storage | null = getStorage()): boolean {
  const storedPreference = storage?.getItem(THEME_FOLLOW_SYSTEM_KEY) ?? null
  if (storedPreference === 'true') {
    return true
  }
  if (storedPreference === 'false') {
    return false
  }

  const legacyMode = storage?.getItem(THEME_STORAGE_KEY) ?? null
  if (legacyMode === 'system') {
    return true
  }
  if (isThemeMode(legacyMode)) {
    return false
  }
  return true
}

export function applyThemeToRoot(
  resolvedTheme: ResolvedTheme,
  root: HTMLElement = document.documentElement,
): void {
  root.setAttribute(ROOT_THEME_ATTRIBUTE, resolvedTheme)
  root.setAttribute(ROOT_THEME_MODE_ATTRIBUTE, resolvedTheme)
  root.style.colorScheme = resolvedTheme

  // Element Plus 暗色模式通过 html.dark class 激活
  root.classList.toggle('dark', resolvedTheme === 'dark')
}

export const useThemeStore = defineStore('theme', () => {
  const mode = ref<ThemeMode>('light')
  const resolvedTheme = ref<ResolvedTheme>('light')
  const isFollowingSystem = ref(true)
  const isInitialized = ref(false)

  let mediaQueryList: MediaQueryListLike | null = null
  let mediaQueryListener: MediaQueryChangeListener | null = null

  const isDark = computed(() => resolvedTheme.value === 'dark')

  function updateResolvedTheme(): void {
    if (isFollowingSystem.value) {
      mediaQueryList = getMediaQueryList()
      resolvedTheme.value = mediaQueryList?.matches ? 'dark' : 'light'
    } else {
      mediaQueryList = null
      resolvedTheme.value = mode.value
    }
    mode.value = resolvedTheme.value
    applyThemeToRoot(resolvedTheme.value)
  }

  function stopSystemSync(): void {
    if (mediaQueryList && mediaQueryListener) {
      removeMediaQueryListener(mediaQueryList, mediaQueryListener)
    }

    mediaQueryListener = null
  }

  function startSystemSync(): void {
    stopSystemSync()

    if (!isFollowingSystem.value) {
      mediaQueryList = null
      return
    }

    mediaQueryList = getMediaQueryList()
    if (!mediaQueryList) {
      return
    }

    mediaQueryListener = (event: MediaQueryListEvent) => {
      if (!isFollowingSystem.value) {
        return
      }

      const nextResolvedTheme = event.matches ? 'dark' : 'light'
      mode.value = nextResolvedTheme
      resolvedTheme.value = nextResolvedTheme
      persistMode(nextResolvedTheme)
      applyThemeToRoot(nextResolvedTheme)
    }

    addMediaQueryListener(mediaQueryList, mediaQueryListener)
  }

  function persistMode(nextMode: ThemeMode): void {
    const storage = getStorage()
    storage?.setItem(THEME_STORAGE_KEY, nextMode)
  }

  function persistFollowSystem(nextValue: boolean): void {
    const storage = getStorage()
    storage?.setItem(THEME_FOLLOW_SYSTEM_KEY, String(nextValue))
  }

  function setMode(nextMode: ThemeMode): void {
    isFollowingSystem.value = false
    mode.value = nextMode
    resolvedTheme.value = nextMode
    persistFollowSystem(false)
    persistMode(nextMode)
    updateResolvedTheme()
    startSystemSync()
  }

  function setFollowSystem(nextValue: boolean): void {
    isFollowingSystem.value = nextValue
    persistFollowSystem(nextValue)

    if (nextValue) {
      updateResolvedTheme()
      persistMode(resolvedTheme.value)
      startSystemSync()
      return
    }

    stopSystemSync()
    mode.value = resolvedTheme.value
    persistMode(mode.value)
  }

  function initialize(): void {
    isFollowingSystem.value = readFollowSystem()
    persistFollowSystem(isFollowingSystem.value)

    if (isFollowingSystem.value) {
      updateResolvedTheme()
      persistMode(resolvedTheme.value)
    } else {
      mode.value = readStoredThemeMode()
      resolvedTheme.value = mode.value
      applyThemeToRoot(resolvedTheme.value)
    }
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
    isFollowingSystem,
    isInitialized,
    initialize,
    setMode,
    setFollowSystem,
    dispose,
  }
})
