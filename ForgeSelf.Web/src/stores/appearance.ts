import { ref } from 'vue'
import { defineStore } from 'pinia'

export const APPEARANCE_BG_IMAGE_KEY = 'forgeself.appearance.backgroundImage'
export const APPEARANCE_BG_OPACITY_KEY = 'forgeself.appearance.backgroundOpacity'
/** 设计稿持久化键：localStorage['forgeself:bg']（DESIGN.md「背景系统」节） */
export const FORGESELF_BG_KEY = 'forgeself:bg'

/** 背景模式：solid=纯色预设 / image=图片预设或自定义图 */
export type BackgroundMode = 'solid' | 'image'

/** 设计稿 12 档纯色预设（DESIGN.md「背景系统」：App.jsx:58-71） */
export const BG_SOLID_PRESETS: { key: string; label: string; value: string }[] = [
  { key: 'ink', label: '墨黑', value: '#0B0908' },
  { key: 'iron', label: '铁灰', value: '#1B1815' },
  { key: 'navy', label: '深蓝', value: '#0F172A' },
  { key: 'forest', label: '深绿', value: '#0F1F1A' },
  { key: 'violet', label: '深紫', value: '#1A0F1F' },
  { key: 'cinnabar', label: '朱砂', value: '#3A1512' },
  { key: 'bronze', label: '青铜', value: '#1F2A1F' },
  { key: 'paper', label: '宣纸', value: '#F4EEDF' },
  { key: 'porcelain', label: '瓷白', value: '#FBF7EE' },
  { key: 'celadon', label: '天青', value: '#1E3A4C' },
  { key: 'iron-dark', label: '玄铁', value: '#141518' },
  { key: 'black', label: '曜黑', value: '#000000' },
]

/** 设计稿 6 张图片预设（已本地化到 ForgeSelf.Web/public/backgrounds/，含熔炉补图） */
export const BG_IMAGE_PRESETS: { key: string; label: string; url: string }[] = [
  { key: 'forge', label: '熔炉', url: '/backgrounds/forge.jpg' },
  { key: 'mountain', label: '山雾', url: '/backgrounds/mountain-mist.jpg' },
  { key: 'ink', label: '墨色', url: '/backgrounds/ink-dark.jpg' },
  { key: 'starry', label: '星野', url: '/backgrounds/starry-night.jpg' },
  { key: 'coast', label: '海岸', url: '/backgrounds/coast.jpg' },
  { key: 'bamboo', label: '竹林', url: '/backgrounds/bamboo.jpg' },
]

/** 默认纯色（墨黑）与默认遮罩浓度（设计稿默认 55%） */
export const DEFAULT_BG_SOLID = '#0B0908'
export const DEFAULT_OVERLAY_OPACITY = 55

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

interface ForgeBgState {
  mode: BackgroundMode
  solid: string
  image: string
  overlay: number
}

function parseForgeBg(raw: string | null | undefined): Partial<ForgeBgState> | null {
  if (!raw) return null
  try {
    const parsed = JSON.parse(raw) as Partial<ForgeBgState>
    if (typeof parsed !== 'object' || parsed === null) return null
    return parsed
  } catch {
    return null
  }
}

export const useAppearanceStore = defineStore('appearance', () => {
  const backgroundImage = ref('')
  const backgroundOpacity = ref(30)
  const isInitialized = ref(false)

  // —— 设计稿背景系统（DESIGN.md）：模式 / 纯色 / 遮罩 ——
  const backgroundMode = ref<BackgroundMode>('solid')
  const solidColor = ref(DEFAULT_BG_SOLID)
  const overlayOpacity = ref(DEFAULT_OVERLAY_OPACITY)

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

  /** 按设计稿持久化整个背景状态到 localStorage['forgeself:bg'] */
  function persistForgeBg(): void {
    const storage = getStorage()
    if (!storage) return
    const state: ForgeBgState = {
      mode: backgroundMode.value,
      solid: solidColor.value,
      image: backgroundImage.value,
      overlay: overlayOpacity.value,
    }
    storage.setItem(FORGESELF_BG_KEY, JSON.stringify(state))
  }

  function setBackgroundImage(url: string): void {
    backgroundImage.value = url
    persistBackgroundImage(url)
    if (url) {
      backgroundMode.value = 'image'
    }
    persistForgeBg()
  }

  function setBackgroundOpacity(opacity: number): void {
    const clamped = clampOpacity(opacity)
    backgroundOpacity.value = clamped
    persistBackgroundOpacity(clamped)
  }

  function clearBackgroundImage(): void {
    backgroundImage.value = ''
    persistBackgroundImage('')
    backgroundMode.value = 'solid'
    persistForgeBg()
  }

  /** 选择纯色预设（墨黑/铁灰/宣纸…） */
  function selectSolidColor(color: string): void {
    solidColor.value = color
    backgroundMode.value = 'solid'
    backgroundImage.value = ''
    persistForgeBg()
  }

  /** 选择图片预设（熔炉/山雾/墨色…）或自定义图 URL */
  function selectImageBackground(url: string): void {
    setBackgroundImage(url)
  }

  /** 遮罩浓度 0-100（设计稿滑竿 0-90%，默认 55） */
  function setOverlayOpacity(opacity: number): void {
    overlayOpacity.value = clampOpacity(opacity)
    persistForgeBg()
  }

  function initialize(): void {
    const storage = getStorage()
    try {
      // 设计稿键优先；无则回退旧键并迁移
      const forgeBg = parseForgeBg(storage?.getItem(FORGESELF_BG_KEY))
      if (forgeBg) {
        if (forgeBg.mode === 'image' && forgeBg.image) {
          backgroundMode.value = 'image'
          backgroundImage.value = forgeBg.image
        } else {
          backgroundMode.value = 'solid'
          backgroundImage.value = ''
        }
        if (forgeBg.solid) solidColor.value = forgeBg.solid
        if (typeof forgeBg.overlay === 'number') {
          overlayOpacity.value = clampOpacity(forgeBg.overlay)
        }
      } else {
        const storedUrl = storage?.getItem(APPEARANCE_BG_IMAGE_KEY) ?? ''
        backgroundImage.value = storedUrl
        backgroundMode.value = storedUrl ? 'image' : 'solid'
      }

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
    backgroundMode,
    solidColor,
    overlayOpacity,
    isInitialized,
    initialize,
    setBackgroundImage,
    setBackgroundOpacity,
    clearBackgroundImage,
    selectSolidColor,
    selectImageBackground,
    setOverlayOpacity,
  }
})
