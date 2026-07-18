import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type {
  TextToolTab,
  FormatterType,
  EncodingType,
  HashType,
  TextStats
} from '@/types/textTools'
import { textToolsApi } from '@/services/textToolsApi'

export const useTextToolsStore = defineStore('textTools', () => {
  const currentTab = ref<TextToolTab>('formatter')
  const inputText = ref('')
  const outputText = ref('')
  const isProcessing = ref(false)
  const error = ref<string | null>(null)
  const formatterType = ref<FormatterType>('json')
  const encodingType = ref<EncodingType>('base64')
  const hashType = ref<HashType>('sha256')
  const indentSize = ref(2)
  const stats = ref<TextStats>({
    charCount: 0,
    charCountNoSpace: 0,
    wordCount: 0,
    lineCount: 0,
    byteCount: 0
  })

  const hasInput = computed(() => inputText.value.length > 0)
  const hasOutput = computed(() => outputText.value.length > 0)

  function setTab(tab: TextToolTab): void {
    currentTab.value = tab
    error.value = null
  }

  function setInput(text: string): void {
    inputText.value = text
    if (currentTab.value === 'stats') {
      computeStats()
    }
  }

  function setFormatterType(type: FormatterType): void {
    formatterType.value = type
  }

  function setEncodingType(type: EncodingType): void {
    encodingType.value = type
  }

  function setHashType(type: HashType): void {
    hashType.value = type
  }

  function setIndentSize(size: number): void {
    indentSize.value = size
  }

  async function processFormat(format: boolean = true): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入要处理的文本'
      return
    }

    try {
      isProcessing.value = true
      error.value = null

      let result = ''
      const type = formatterType.value

      if (format) {
        switch (type) {
          case 'json':
            result = textToolsApi.formatJson(inputText.value, indentSize.value)
            break
          case 'xml':
            result = textToolsApi.formatXml(inputText.value, indentSize.value)
            break
          case 'html':
            result = textToolsApi.formatHtml(inputText.value, indentSize.value)
            break
        }
      } else {
        switch (type) {
          case 'json':
            result = textToolsApi.minifyJson(inputText.value)
            break
          case 'xml':
            result = textToolsApi.minifyXml(inputText.value)
            break
          case 'html':
            result = textToolsApi.minifyHtml(inputText.value)
            break
        }
      }

      outputText.value = result
    } catch (e) {
      console.error('格式化处理失败:', e)
      error.value = e instanceof Error ? e.message : '格式化处理失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function processEncoding(encode: boolean = true): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入要处理的文本'
      return
    }

    try {
      isProcessing.value = true
      error.value = null

      let result = ''
      const type = encodingType.value

      if (encode) {
        switch (type) {
          case 'base64':
            result = textToolsApi.encodeBase64(inputText.value)
            break
          case 'url':
            result = textToolsApi.encodeUrl(inputText.value)
            break
          case 'unicode':
            result = textToolsApi.encodeUnicode(inputText.value)
            break
        }
      } else {
        switch (type) {
          case 'base64':
            result = textToolsApi.decodeBase64(inputText.value)
            break
          case 'url':
            result = textToolsApi.decodeUrl(inputText.value)
            break
          case 'unicode':
            result = textToolsApi.decodeUnicode(inputText.value)
            break
        }
      }

      outputText.value = result
    } catch (e) {
      console.error('编解码处理失败:', e)
      error.value = e instanceof Error ? e.message : '编解码处理失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function processHash(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入要计算哈希的文本'
      return
    }

    try {
      isProcessing.value = true
      error.value = null

      const result = await textToolsApi.computeHash(inputText.value, hashType.value)
      outputText.value = result
    } catch (e) {
      console.error('哈希计算失败:', e)
      error.value = e instanceof Error ? e.message : '哈希计算失败'
    } finally {
      isProcessing.value = false
    }
  }

  function computeStats(): void {
    stats.value = textToolsApi.getStats(inputText.value)
  }

  function clearOutput(): void {
    outputText.value = ''
    error.value = null
  }

  function clearInput(): void {
    inputText.value = ''
    outputText.value = ''
    error.value = null
    stats.value = {
      charCount: 0,
      charCountNoSpace: 0,
      wordCount: 0,
      lineCount: 0,
      byteCount: 0
    }
  }

  function swapInputOutput(): void {
    const temp = inputText.value
    inputText.value = outputText.value
    outputText.value = temp
    error.value = null

    if (currentTab.value === 'stats') {
      computeStats()
    }
  }

  function clearError(): void {
    error.value = null
  }

  return {
    currentTab,
    inputText,
    outputText,
    isProcessing,
    error,
    formatterType,
    encodingType,
    hashType,
    indentSize,
    stats,
    hasInput,
    hasOutput,
    setTab,
    setInput,
    setFormatterType,
    setEncodingType,
    setHashType,
    setIndentSize,
    processFormat,
    processEncoding,
    processHash,
    computeStats,
    clearOutput,
    clearInput,
    swapInputOutput,
    clearError
  }
})
