import { ref } from 'vue'
import { defineStore } from 'pinia'

export const APPEARANCE_BG_IMAGE_KEY = 'forgeself.appearance.backgroundImage'
export const APPEARANCE_BG_OPACITY_KEY = 'forgeself.appearance.backgroundOpacity'

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

export function clampOpacity(opacity: number): number {
  return Math.max(0, Math.min(100, Math.round(opacity)))
}

export const useAppearanceStore = defineStore('appearance', () => {
  const backgroundImage = ref('')
  const backgroundOpacity = ref(30)
  const isInitialized = ref(false)

  function persistBackgroundImage(url: string): void {
    const storage = getStorage()
    if (url) {
      storage?.setItem(APPEARANCE_BG_IMAGE_KEY, url)
    } else {
      storage?.removeItem(APPEARANCE_BG_IMAGE_KEY)
    }
  }

  function persistBackgroundOpacity(opacity: number): void {
    const storage = getStorage()
    storage?.setItem(APPEARANCE_BG_OPACITY_KEY, String(opacity))
  }

  function setBackgroundImage(url: string): void {
    backgroundImage.value = url
    persistBackgroundImage(url)
  }

  function setBackgroundOpacity(opacity: number): void {
    const clamped = clampOpacity(opacity)
    backgroundOpacity.value = clamped
    persistBackgroundOpacity(clamped)
  }

  function clearBackgroundImage(): void {
    backgroundImage.value = ''
    persistBackgroundImage('')
  }

  function initialize(): void {
    const storage = getStorage()
    try {
      const storedUrl = storage?.getItem(APPEARANCE_BG_IMAGE_KEY) ?? ''
      backgroundImage.value = storedUrl

      const storedOpacity = storage?.getItem(APPEARANCE_BG_OPACITY_KEY)
      if (storedOpacity !== null && storedOpacity !== undefined) {
        const parsed = parseInt(storedOpacity, 10)
        if (!isNaN(parsed)) {
          backgroundOpacity.value = clampOpacity(parsed)
        }
      }
    } catch {
      // localStorage read failure — use defaults
    }
    isInitialized.value = true
  }

  return {
    backgroundImage,
    backgroundOpacity,
    isInitialized,
    initialize,
    setBackgroundImage,
    setBackgroundOpacity,
    clearBackgroundImage,
  }
})