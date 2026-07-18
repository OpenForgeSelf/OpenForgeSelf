import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type {
  DevToolTab,
  EncodingType,
  HashType,
  EncryptMode,
  ValidateResult,
  HashAllResult,
} from '@/types/devTools'
import { devToolsApi } from '@/services/devToolsApi'

export const useDevToolsStore = defineStore('devTools', () => {
  const currentTab = ref<DevToolTab>('json')
  const inputText = ref('')
  const outputText = ref('')
  const isProcessing = ref(false)
  const error = ref<string | null>(null)
  const indentSize = ref(2)
  const encodingType = ref<EncodingType>('base64')
  const hashType = ref<HashType>('sha256')
  const jsonPathExpression = ref('$.')
  const validateResult = ref<ValidateResult | null>(null)
  const hashAllResult = ref<HashAllResult | null>(null)
  const hmacKey = ref('')
  const aesKey = ref('')
  const aesIv = ref('')
  const converterDirection = ref<'json-to-yaml' | 'yaml-to-json'>('json-to-yaml')
  const encryptMode = ref<EncryptMode>('hmac')
  const hmacAlgorithm = ref<string>('sha256')
  const encryptKey = ref('')
  const encryptIv = ref('')

  const hasInput = computed(() => inputText.value.length > 0)
  const hasOutput = computed(() => outputText.value.length > 0)

  function setTab(tab: DevToolTab): void {
    currentTab.value = tab
    error.value = null
    validateResult.value = null
    hashAllResult.value = null
  }

  function setInput(text: string): void {
    inputText.value = text
    error.value = null
    validateResult.value = null
  }

  function setOutput(text: string): void {
    outputText.value = text
  }

  function setIndentSize(size: number): void {
    indentSize.value = size
  }

  function setEncodingType(type: EncodingType): void {
    encodingType.value = type
    error.value = null
  }

  function setHashType(type: HashType): void {
    hashType.value = type
    error.value = null
  }

  function setJsonPathExpression(expr: string): void {
    jsonPathExpression.value = expr
  }

  function setConverterDirection(dir: 'json-to-yaml' | 'yaml-to-json'): void {
    converterDirection.value = dir
  }

  function setEncryptMode(mode: EncryptMode): void {
    encryptMode.value = mode
    error.value = null
  }

  function setHmacAlgorithm(algo: string): void {
    hmacAlgorithm.value = algo
  }

  async function encrypt(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入文本'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      if (encryptMode.value === 'hmac') {
        outputText.value = await devToolsApi.computeHmac(inputText.value, encryptKey.value || hmacKey.value, hmacAlgorithm.value)
      } else {
        outputText.value = await devToolsApi.aesEncrypt(inputText.value, encryptKey.value || aesKey.value, encryptIv.value || aesIv.value || undefined)
      }
    } catch (e) {
      error.value = e instanceof Error ? e.message : '加密失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function decrypt(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入文本'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      outputText.value = await devToolsApi.aesDecrypt(inputText.value, encryptKey.value || aesKey.value, encryptIv.value || aesIv.value || undefined)
    } catch (e) {
      error.value = e instanceof Error ? e.message : '解密失败'
    } finally {
      isProcessing.value = false
    }
  }

  function clearError(): void {
    error.value = null
  }

  function clearInput(): void {
    inputText.value = ''
    outputText.value = ''
    error.value = null
    validateResult.value = null
    hashAllResult.value = null
  }

  function clearOutput(): void {
    outputText.value = ''
    error.value = null
    validateResult.value = null
    hashAllResult.value = null
  }

  function swapInputOutput(): void {
    const temp = inputText.value
    inputText.value = outputText.value
    outputText.value = temp
    error.value = null
    validateResult.value = null
  }

  async function formatJson(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入JSON文本'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      outputText.value = await devToolsApi.formatJson(inputText.value, indentSize.value)
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'JSON格式化失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function minifyJson(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入JSON文本'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      outputText.value = await devToolsApi.minifyJson(inputText.value)
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'JSON压缩失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function validateJson(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入JSON文本'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      validateResult.value = await devToolsApi.validateJson(inputText.value)
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'JSON校验失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function queryJsonPath(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入JSON文本'
      return
    }
    if (!jsonPathExpression.value.trim()) {
      error.value = '请输入JSONPath表达式'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      outputText.value = await devToolsApi.jsonPathQuery(inputText.value, jsonPathExpression.value)
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'JSONPath查询失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function convertJsonToYaml(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入文本'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      if (converterDirection.value === 'json-to-yaml') {
        outputText.value = await devToolsApi.jsonToYaml(inputText.value)
      } else {
        outputText.value = await devToolsApi.yamlToJson(inputText.value)
      }
    } catch (e) {
      error.value = e instanceof Error ? e.message : '格式转换失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function formatYaml(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入YAML文本'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      outputText.value = await devToolsApi.formatYaml(inputText.value)
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'YAML格式化失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function validateYaml(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入YAML文本'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      validateResult.value = await devToolsApi.validateYaml(inputText.value)
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'YAML校验失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function formatXml(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入XML文本'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      outputText.value = await devToolsApi.formatXml(inputText.value, indentSize.value)
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'XML格式化失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function minifyXml(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入XML文本'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      outputText.value = await devToolsApi.minifyXml(inputText.value)
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'XML压缩失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function validateXml(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入XML文本'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      validateResult.value = await devToolsApi.validateXml(inputText.value)
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'XML校验失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function encodeText(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入文本'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      const type = encodingType.value
      let result = ''
      switch (type) {
        case 'base64':
          result = await devToolsApi.base64Encode(inputText.value)
          break
        case 'url':
          result = await devToolsApi.urlEncode(inputText.value)
          break
        case 'unicode':
          result = await devToolsApi.unicodeEncode(inputText.value)
          break
        case 'html':
          result = await devToolsApi.htmlEncode(inputText.value)
          break
        case 'hex':
          result = await devToolsApi.hexEncode(inputText.value)
          break
      }
      outputText.value = result
    } catch (e) {
      error.value = e instanceof Error ? e.message : '编码失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function decodeText(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入编码文本'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      const type = encodingType.value
      let result = ''
      switch (type) {
        case 'base64':
          result = await devToolsApi.base64Decode(inputText.value)
          break
        case 'url':
          result = await devToolsApi.urlDecode(inputText.value)
          break
        case 'unicode':
          result = await devToolsApi.unicodeDecode(inputText.value)
          break
        case 'html':
          result = await devToolsApi.htmlDecode(inputText.value)
          break
        case 'hex':
          result = await devToolsApi.hexDecode(inputText.value)
          break
      }
      outputText.value = result
    } catch (e) {
      error.value = e instanceof Error ? e.message : '解码失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function computeHash(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入文本'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      const type = hashType.value
      let result = ''
      switch (type) {
        case 'md5':
          result = await devToolsApi.computeMd5(inputText.value)
          break
        case 'sha1':
          result = await devToolsApi.computeSha1(inputText.value)
          break
        case 'sha256':
          result = await devToolsApi.computeSha256(inputText.value)
          break
        case 'sha512':
          result = await devToolsApi.computeSha512(inputText.value)
          break
      }
      outputText.value = result
    } catch (e) {
      error.value = e instanceof Error ? e.message : '哈希计算失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function computeAllHashes(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入文本'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      hashAllResult.value = await devToolsApi.computeAllHashes(inputText.value)
    } catch (e) {
      error.value = e instanceof Error ? e.message : '哈希计算失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function computeHmac(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入文本'
      return
    }
    if (!hmacKey.value.trim()) {
      error.value = '请输入密钥'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      outputText.value = await devToolsApi.computeHmac(
        inputText.value,
        hmacKey.value,
        hashType.value
      )
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'HMAC计算失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function encryptAes(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入明文'
      return
    }
    if (!aesKey.value.trim()) {
      error.value = '请输入密钥'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      outputText.value = await devToolsApi.aesEncrypt(
        inputText.value,
        aesKey.value,
        aesIv.value || undefined
      )
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'AES加密失败'
    } finally {
      isProcessing.value = false
    }
  }

  async function decryptAes(): Promise<void> {
    if (!inputText.value.trim()) {
      error.value = '请输入密文'
      return
    }
    if (!aesKey.value.trim()) {
      error.value = '请输入密钥'
      return
    }
    try {
      isProcessing.value = true
      error.value = null
      outputText.value = await devToolsApi.aesDecrypt(
        inputText.value,
        aesKey.value,
        aesIv.value || undefined
      )
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'AES解密失败'
    } finally {
      isProcessing.value = false
    }
  }

  return {
    currentTab,
    inputText,
    outputText,
    isProcessing,
    error,
    indentSize,
    encodingType,
    hashType,
    jsonPathExpression,
    validateResult,
    hashAllResult,
    hmacKey,
    aesKey,
    aesIv,
    converterDirection,
    encryptMode,
    hmacAlgorithm,
    encryptKey,
    encryptIv,
    hasInput,
    hasOutput,
    setTab,
    setInput,
    setOutput,
    setIndentSize,
    setEncodingType,
    setHashType,
    setJsonPathExpression,
    setConverterDirection,
    setEncryptMode,
    setHmacAlgorithm,
    encrypt,
    decrypt,
    clearError,
    clearInput,
    clearOutput,
    swapInputOutput,
    formatJson,
    minifyJson,
    validateJson,
    queryJsonPath,
    convertJsonToYaml,
    formatYaml,
    validateYaml,
    formatXml,
    minifyXml,
    validateXml,
    encodeText,
    decodeText,
    computeHash,
    computeAllHashes,
    computeHmac,
    encryptAes,
    decryptAes,
  }
})
