<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { devToolsApi } from '@/services/devToolsApi'
import type { ColorConvertResult, ColorPaletteResult, ColorContrastResult } from '@/types/devTools'

const hexInput = ref('#3b82f6')
const rgbR = ref(59)
const rgbG = ref(130)
const rgbB = ref(246)
const hslH = ref(217)
const hslS = ref(91)
const hslL = ref(60)
const alpha = ref(100)
const colorResult = ref<ColorConvertResult | null>(null)

const paletteBaseColor = ref('#3b82f6')
const paletteScheme = ref('analogous')
const paletteCount = ref(5)
const paletteResult = ref<ColorPaletteResult | null>(null)

const foregroundColor = ref('#ffffff')
const backgroundColor = ref('#3b82f6')
const contrastResult = ref<ColorContrastResult | null>(null)

const activeTab = ref<'convert' | 'palette' | 'contrast'>('convert')

const colorSchemes = [
  { value: 'analogous', label: '类似色', icon: 'fa-palette' },
  { value: 'complementary', label: '互补色', icon: 'fa-shuffle' },
  { value: 'triadic', label: '三角色', icon: 'fa-bullseye' },
  { value: 'split-complementary', label: '分裂互补', icon: 'fa-code-fork' },
  { value: 'monochromatic', label: '单色', icon: 'fa-circle-half-stroke' },
]

async function updateColor(from: 'hex' | 'rgb' | 'hsl'): Promise<void> {
  try {
    const request: Record<string, unknown> = {}

    if (from === 'hex') {
      request.hex = hexInput.value
    } else if (from === 'rgb') {
      request.r = rgbR.value
      request.g = rgbG.value
      request.b = rgbB.value
    } else if (from === 'hsl') {
      request.h = hslH.value
      request.s = hslS.value
      request.l = hslL.value
    }

    if (alpha.value < 100) {
      request.alpha = alpha.value / 100
    }

    const result = await devToolsApi.convertColor(request)
    colorResult.value = result

    hexInput.value = result.hex
    rgbR.value = result.r
    rgbG.value = result.g
    rgbB.value = result.b
    hslH.value = Math.round(result.h)
    hslS.value = Math.round(result.s)
    hslL.value = Math.round(result.l)
    alpha.value = Math.round(result.a * 100)
  } catch (e) {
    console.error('颜色转换失败', e)
  }
}

async function generatePalette(): Promise<void> {
  if (!paletteBaseColor.value) return
  try {
    paletteResult.value = await devToolsApi.generateColorPalette(
      paletteBaseColor.value,
      paletteCount.value,
      paletteScheme.value
    )
  } catch (e) {
    console.error('生成调色板失败', e)
  }
}

async function checkContrast(): Promise<void> {
  if (!foregroundColor.value || !backgroundColor.value) return
  try {
    contrastResult.value = await devToolsApi.checkColorContrast(
      foregroundColor.value,
      backgroundColor.value
    )
  } catch (e) {
    console.error('对比度检查失败', e)
  }
}

function copyToClipboard(text: string): void {
  navigator.clipboard.writeText(text).catch(() => {})
}

function useAsForeground(color: string): void {
  foregroundColor.value = color
  checkContrast()
}

function useAsBackground(color: string): void {
  backgroundColor.value = color
  checkContrast()
}

watch(hexInput, (val) => {
  if (val && /^#?[0-9a-fA-F]{6}$/.test(val)) {
    updateColor('hex')
  }
})

watch([rgbR, rgbG, rgbB], () => {
  updateColor('rgb')
}, { deep: false })

watch([hslH, hslS, hslL], () => {
  updateColor('hsl')
}, { deep: false })

watch(alpha, () => {
  updateColor('hex')
})

watch([paletteBaseColor, paletteScheme, paletteCount], () => {
  generatePalette()
})

watch([foregroundColor, backgroundColor], () => {
  checkContrast()
})

onMounted(() => {
  updateColor('hex')
  generatePalette()
  checkContrast()
})
</script>

<template>
  <div class="color-tool">
    <div class="color-tabs">
      <button
        class="color-tab"
        :class="{ active: activeTab === 'convert' }"
        @click="activeTab = 'convert'"
      >
        <i class="fa-solid fa-arrows-rotate" />
        颜色转换
      </button>
      <button
        class="color-tab"
        :class="{ active: activeTab === 'palette' }"
        @click="activeTab = 'palette'"
      >
        <i class="fa-solid fa-palette" />
        调色板
      </button>
      <button
        class="color-tab"
        :class="{ active: activeTab === 'contrast' }"
        @click="activeTab = 'contrast'"
      >
        <i class="fa-solid fa-circle-half-stroke" />
        对比度
      </button>
    </div>

    <div v-if="activeTab === 'convert'" class="convert-section">
      <div class="color-preview-row">
        <div
          class="color-preview"
          :style="{ background: colorResult?.hexWithAlpha || hexInput }"
        >
          <div class="color-preview-inner">
            <span class="preview-hex">{{ colorResult?.hex?.toUpperCase() || hexInput }}</span>
          </div>
        </div>
      </div>

      <div class="alpha-slider-section">
        <div class="slider-header">
          <label class="slider-label">透明度</label>
          <span class="slider-value">{{ alpha }}%</span>
        </div>
        <input
          v-model="alpha"
          type="range"
          min="0"
          max="100"
          class="alpha-slider"
        />
      </div>

      <div class="format-sections">
        <div class="format-section">
          <div class="section-header">
            <span class="section-title">HEX</span>
            <button class="copy-btn-sm" @click="copyToClipboard(colorResult?.hex || '')">
              <i class="fa-regular fa-copy" />
              复制
            </button>
          </div>
          <div class="hex-input-row">
            <span class="hash-symbol">#</span>
            <input
              v-model="hexInput"
              type="text"
              class="hex-input"
              maxlength="7"
              spellcheck="false"
            />
            <input
              type="color"
              :value="colorResult?.hex || hexInput"
              class="color-picker-input"
              @input="hexInput = ($event.target as HTMLInputElement).value"
            />
          </div>
        </div>

        <div class="format-section">
          <div class="section-header">
            <span class="section-title">RGB</span>
            <button class="copy-btn-sm" @click="copyToClipboard(colorResult?.rgbString || '')">
              <i class="fa-regular fa-copy" />
              复制
            </button>
          </div>
          <div class="rgb-inputs">
            <div class="rgb-input-group">
              <label class="rgb-label">R</label>
              <input
                v-model.number="rgbR"
                type="number"
                min="0"
                max="255"
                class="rgb-input"
              />
            </div>
            <div class="rgb-input-group">
              <label class="rgb-label">G</label>
              <input
                v-model.number="rgbG"
                type="number"
                min="0"
                max="255"
                class="rgb-input"
              />
            </div>
            <div class="rgb-input-group">
              <label class="rgb-label">B</label>
              <input
                v-model.number="rgbB"
                type="number"
                min="0"
                max="255"
                class="rgb-input"
              />
            </div>
          </div>
          <div class="format-string">{{ colorResult?.rgbString }}</div>
        </div>

        <div class="format-section">
          <div class="section-header">
            <span class="section-title">HSL</span>
            <button class="copy-btn-sm" @click="copyToClipboard(colorResult?.hslString || '')">
              <i class="fa-regular fa-copy" />
              复制
            </button>
          </div>
          <div class="hsl-inputs">
            <div class="hsl-input-group">
              <label class="hsl-label">H</label>
              <input
                v-model.number="hslH"
                type="number"
                min="0"
                max="360"
                class="hsl-input"
              />
              <span class="hsl-unit">°</span>
            </div>
            <div class="hsl-input-group">
              <label class="hsl-label">S</label>
              <input
                v-model.number="hslS"
                type="number"
                min="0"
                max="100"
                class="hsl-input"
              />
              <span class="hsl-unit">%</span>
            </div>
            <div class="hsl-input-group">
              <label class="hsl-label">L</label>
              <input
                v-model.number="hslL"
                type="number"
                min="0"
                max="100"
                class="hsl-input"
              />
              <span class="hsl-unit">%</span>
            </div>
          </div>
          <div class="format-string">{{ colorResult?.hslString }}</div>
        </div>
      </div>
    </div>

    <div v-if="activeTab === 'palette'" class="palette-section">
      <div class="palette-controls">
        <div class="control-group">
          <label class="control-label">基色</label>
          <div class="color-input-row">
            <input
              v-model="paletteBaseColor"
              type="text"
              class="color-text-input"
              spellcheck="false"
            />
            <input
              type="color"
              :value="paletteBaseColor"
              class="color-picker-input"
              @input="paletteBaseColor = ($event.target as HTMLInputElement).value"
            />
          </div>
        </div>

        <div class="control-group">
          <label class="control-label">配色方案</label>
          <div class="scheme-buttons">
            <button
              v-for="scheme in colorSchemes"
              :key="scheme.value"
              class="scheme-btn"
              :class="{ active: paletteScheme === scheme.value }"
              :title="scheme.label"
              @click="paletteScheme = scheme.value"
            >
              <i :class="['fa-solid', scheme.icon]" />
              <span>{{ scheme.label }}</span>
            </button>
          </div>
        </div>

        <div class="control-group">
          <label class="control-label">颜色数量: {{ paletteCount }}</label>
          <input
            v-model.number="paletteCount"
            type="range"
            min="3"
            max="12"
            class="count-slider"
          />
        </div>
      </div>

      <div v-if="paletteResult" class="palette-result">
        <div class="palette-colors">
          <div
            v-for="(color, idx) in paletteResult.colors"
            :key="idx"
            class="palette-color-card"
          >
            <div
              class="palette-color-preview"
              :style="{ background: color }"
            >
              <div class="palette-color-actions">
                <button class="action-btn" title="复制" @click="copyToClipboard(color)">
                  <i class="fa-regular fa-copy" />
                </button>
                <button class="action-btn" title="设为前景色" @click="useAsForeground(color)">
                  <i class="fa-solid fa-font" />
                </button>
                <button class="action-btn" title="设为背景色" @click="useAsBackground(color)">
                  <i class="fa-solid fa-paint-roller" />
                </button>
              </div>
            </div>
            <div class="palette-color-info">
              <span class="color-hex">{{ color.toUpperCase() }}</span>
              <span class="color-index">{{ idx + 1 }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div v-if="activeTab === 'contrast'" class="contrast-section">
      <div class="contrast-colors">
        <div class="contrast-color-group">
          <label class="contrast-label">前景色（文字）</label>
          <div class="color-input-row">
            <input
              v-model="foregroundColor"
              type="text"
              class="color-text-input"
              spellcheck="false"
            />
            <input
              type="color"
              :value="foregroundColor"
              class="color-picker-input"
              @input="foregroundColor = ($event.target as HTMLInputElement).value"
            />
          </div>
        </div>

        <div class="contrast-arrow">
          <i class="fa-solid fa-right-left" />
        </div>

        <div class="contrast-color-group">
          <label class="contrast-label">背景色</label>
          <div class="color-input-row">
            <input
              v-model="backgroundColor"
              type="text"
              class="color-text-input"
              spellcheck="false"
            />
            <input
              type="color"
              :value="backgroundColor"
              class="color-picker-input"
              @input="backgroundColor = ($event.target as HTMLInputElement).value"
            />
          </div>
        </div>
      </div>

      <div class="contrast-preview">
        <div
          class="preview-box"
          :style="{ background: backgroundColor, color: foregroundColor }"
        >
          <span class="preview-title">标题文字示例</span>
          <span class="preview-body">这是一段正文文字，用来预览颜色对比度效果。</span>
          <span class="preview-small">小号文字示例</span>
        </div>
      </div>

      <div v-if="contrastResult" class="contrast-result">
        <div class="contrast-ratio-card">
          <div class="ratio-label">对比度</div>
          <div class="ratio-value">{{ contrastResult.ratio.toFixed(2) }}:1</div>
          <div class="ratio-level" :class="contrastResult.level.toLowerCase()">
            {{ contrastResult.level }}
          </div>
        </div>

        <div class="wcag-standards">
          <div class="standard-item">
            <div class="standard-header">
              <span class="standard-name">AA (正常文本)</span>
              <span class="pass-status" :class="{ pass: contrastResult.aaNormal, fail: !contrastResult.aaNormal }">
                <i :class="contrastResult.aaNormal ? 'fa-solid fa-check' : 'fa-solid fa-xmark'" />
                {{ contrastResult.aaNormal ? '通过' : '未通过' }}
              </span>
            </div>
            <div class="standard-desc">最小对比度 4.5:1</div>
          </div>

          <div class="standard-item">
            <div class="standard-header">
              <span class="standard-name">AA (大文本)</span>
              <span class="pass-status" :class="{ pass: contrastResult.aaLarge, fail: !contrastResult.aaLarge }">
                <i :class="contrastResult.aaLarge ? 'fa-solid fa-check' : 'fa-solid fa-xmark'" />
                {{ contrastResult.aaLarge ? '通过' : '未通过' }}
              </span>
            </div>
            <div class="standard-desc">最小对比度 3:1</div>
          </div>

          <div class="standard-item">
            <div class="standard-header">
              <span class="standard-name">AAA (正常文本)</span>
              <span class="pass-status" :class="{ pass: contrastResult.aaaNormal, fail: !contrastResult.aaaNormal }">
                <i :class="contrastResult.aaaNormal ? 'fa-solid fa-check' : 'fa-solid fa-xmark'" />
                {{ contrastResult.aaaNormal ? '通过' : '未通过' }}
              </span>
            </div>
            <div class="standard-desc">最小对比度 7:1</div>
          </div>

          <div class="standard-item">
            <div class="standard-header">
              <span class="standard-name">AAA (大文本)</span>
              <span class="pass-status" :class="{ pass: contrastResult.aaaLarge, fail: !contrastResult.aaaLarge }">
                <i :class="contrastResult.aaaLarge ? 'fa-solid fa-check' : 'fa-solid fa-xmark'" />
                {{ contrastResult.aaaLarge ? '通过' : '未通过' }}
              </span>
            </div>
            <div class="standard-desc">最小对比度 4.5:1</div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.color-tool {
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: 100%;
  overflow-y: auto;
  padding: 4px;
}

.color-tabs {
  display: flex;
  gap: 4px;
  padding: 4px;
  background: #f8f9fa;
  border-radius: 8px;
  flex-shrink: 0;
}

.color-tab {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 10px;
  border: none;
  background: transparent;
  color: #6c757d;
  font-size: 13px;
  font-weight: 500;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
}

.color-tab:hover {
  background: #e9ecef;
  color: #495057;
}

.color-tab.active {
  background: white;
  color: #0d6efd;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.convert-section {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.color-preview-row {
  display: flex;
  justify-content: center;
}

.color-preview {
  width: 100%;
  height: 100px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  transition: background 0.3s;
}

.color-preview-inner {
  padding: 8px 16px;
  background: rgba(255, 255, 255, 0.9);
  border-radius: 8px;
  backdrop-filter: blur(4px);
}

.preview-hex {
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 18px;
  font-weight: 600;
  color: #212529;
}

.alpha-slider-section {
  padding: 0 4px;
}

.slider-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
}

.slider-label {
  font-size: 13px;
  font-weight: 500;
  color: #495057;
}

.slider-value {
  font-size: 13px;
  font-weight: 600;
  color: #0d6efd;
}

.alpha-slider {
  width: 100%;
  height: 6px;
  border-radius: 3px;
  background: #e9ecef;
  outline: none;
  cursor: pointer;
}

.format-sections {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.format-section {
  padding: 12px;
  background: #f8f9fa;
  border-radius: 10px;
  border: 1px solid #e9ecef;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
}

.section-title {
  font-size: 13px;
  font-weight: 600;
  color: #495057;
}

.copy-btn-sm {
  padding: 4px 10px;
  border: 1px solid #dee2e6;
  background: white;
  color: #6c757d;
  border-radius: 6px;
  cursor: pointer;
  font-size: 12px;
  display: flex;
  align-items: center;
  gap: 4px;
  transition: all 0.2s;
}

.copy-btn-sm:hover {
  background: #e9ecef;
  color: #0d6efd;
}

.hex-input-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.hash-symbol {
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 16px;
  font-weight: 600;
  color: #6c757d;
}

.hex-input {
  flex: 1;
  padding: 8px 10px;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 14px;
  color: #212529;
  background: white;
  outline: none;
  text-transform: uppercase;
  transition: border-color 0.2s;
}

.hex-input:focus {
  border-color: #0d6efd;
}

.color-picker-input {
  width: 40px;
  height: 36px;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  cursor: pointer;
  padding: 2px;
  background: white;
}

.rgb-inputs {
  display: flex;
  gap: 10px;
  margin-bottom: 8px;
}

.rgb-input-group {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.rgb-label {
  font-size: 11px;
  font-weight: 600;
  color: #ef4444;
  text-align: center;
}

.rgb-input-group:nth-child(2) .rgb-label {
  color: #22c55e;
}

.rgb-input-group:nth-child(3) .rgb-label {
  color: #3b82f6;
}

.rgb-input {
  padding: 6px 8px;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 13px;
  text-align: center;
  color: #212529;
  background: white;
  outline: none;
  transition: border-color 0.2s;
}

.rgb-input:focus {
  border-color: #0d6efd;
}

.format-string {
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 12px;
  color: #6c757d;
  text-align: center;
  padding: 6px;
  background: white;
  border-radius: 4px;
  border: 1px solid #e9ecef;
}

.hsl-inputs {
  display: flex;
  gap: 10px;
  margin-bottom: 8px;
}

.hsl-input-group {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}

.hsl-label {
  font-size: 11px;
  font-weight: 600;
  color: #8b5cf6;
}

.hsl-input-group:nth-child(2) .hsl-label {
  color: #06b6d4;
}

.hsl-input-group:nth-child(3) .hsl-label {
  color: #f59e0b;
}

.hsl-input {
  width: 100%;
  padding: 6px 8px;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 13px;
  text-align: center;
  color: #212529;
  background: white;
  outline: none;
  transition: border-color 0.2s;
}

.hsl-input:focus {
  border-color: #0d6efd;
}

.hsl-unit {
  font-size: 11px;
  color: #6c757d;
}

.palette-section {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.palette-controls {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 14px;
  background: #f8f9fa;
  border-radius: 10px;
}

.control-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.control-label {
  font-size: 12px;
  font-weight: 600;
  color: #495057;
}

.color-input-row {
  display: flex;
  gap: 8px;
}

.color-text-input {
  flex: 1;
  padding: 8px 10px;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 13px;
  color: #212529;
  background: white;
  outline: none;
  text-transform: uppercase;
  transition: border-color 0.2s;
}

.color-text-input:focus {
  border-color: #0d6efd;
}

.scheme-buttons {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.scheme-btn {
  flex: 1;
  min-width: 80px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  padding: 8px 6px;
  border: 1px solid #dee2e6;
  background: white;
  color: #6c757d;
  border-radius: 8px;
  cursor: pointer;
  font-size: 11px;
  transition: all 0.2s;
}

.scheme-btn:hover {
  border-color: #0d6efd;
  color: #0d6efd;
}

.scheme-btn.active {
  background: #0d6efd;
  color: white;
  border-color: #0d6efd;
}

.scheme-btn i {
  font-size: 16px;
}

.count-slider {
  width: 100%;
  height: 6px;
  border-radius: 3px;
  background: #e9ecef;
  outline: none;
  cursor: pointer;
}

.palette-result {
  flex: 1;
}

.palette-colors {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
  gap: 10px;
}

.palette-color-card {
  display: flex;
  flex-direction: column;
  border-radius: 10px;
  overflow: hidden;
  border: 1px solid #e9ecef;
  background: white;
}

.palette-color-preview {
  height: 80px;
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: transform 0.2s;
}

.palette-color-card:hover .palette-color-preview {
  transform: scale(1.02);
}

.palette-color-actions {
  display: flex;
  gap: 4px;
  opacity: 0;
  transition: opacity 0.2s;
  background: rgba(255, 255, 255, 0.9);
  padding: 4px;
  border-radius: 6px;
  backdrop-filter: blur(4px);
}

.palette-color-card:hover .palette-color-actions {
  opacity: 1;
}

.action-btn {
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  border: none;
  background: white;
  color: #495057;
  border-radius: 4px;
  cursor: pointer;
  font-size: 12px;
  transition: all 0.2s;
}

.action-btn:hover {
  background: #0d6efd;
  color: white;
}

.palette-color-info {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 10px;
  background: #f8f9fa;
}

.color-hex {
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 11px;
  font-weight: 600;
  color: #495057;
}

.color-index {
  font-size: 10px;
  color: #adb5bd;
  font-weight: 600;
}

.contrast-section {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.contrast-colors {
  display: flex;
  align-items: flex-end;
  gap: 12px;
}

.contrast-color-group {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.contrast-label {
  font-size: 12px;
  font-weight: 600;
  color: #495057;
}

.contrast-arrow {
  padding-bottom: 10px;
  color: #adb5bd;
  font-size: 16px;
}

.contrast-preview {
  border-radius: 10px;
  overflow: hidden;
  border: 1px solid #e9ecef;
}

.preview-box {
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  transition: all 0.3s;
}

.preview-title {
  font-size: 20px;
  font-weight: 700;
}

.preview-body {
  font-size: 14px;
  line-height: 1.5;
}

.preview-small {
  font-size: 12px;
}

.contrast-result {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.contrast-ratio-card {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  border-radius: 12px;
  color: white;
}

.ratio-label {
  font-size: 13px;
  opacity: 0.9;
}

.ratio-value {
  font-size: 28px;
  font-weight: 700;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
}

.ratio-level {
  padding: 4px 12px;
  background: rgba(255, 255, 255, 0.2);
  border-radius: 20px;
  font-size: 12px;
  font-weight: 600;
}

.ratio-level.excellent {
  background: rgba(34, 197, 94, 0.9);
}

.ratio-level.good {
  background: rgba(59, 130, 246, 0.9);
}

.ratio-level.fail {
  background: rgba(239, 68, 68, 0.9);
}

.wcag-standards {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 8px;
}

.standard-item {
  padding: 12px;
  background: #f8f9fa;
  border-radius: 8px;
  border: 1px solid #e9ecef;
}

.standard-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4px;
}

.standard-name {
  font-size: 12px;
  font-weight: 600;
  color: #495057;
}

.pass-status {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 10px;
}

.pass-status.pass {
  color: #16a34a;
  background: #dcfce7;
}

.pass-status.fail {
  color: #dc2626;
  background: #fee2e2;
}

.standard-desc {
  font-size: 11px;
  color: #6c757d;
}
</style>
