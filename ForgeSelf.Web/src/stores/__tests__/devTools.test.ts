import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useDevToolsStore } from '../devTools'

vi.mock('@/services/devToolsApi', () => ({
  devToolsApi: {
    formatJson: vi.fn((text: string, indent: number) => JSON.stringify(JSON.parse(text), null, indent)),
    minifyJson: vi.fn((text: string) => JSON.stringify(JSON.parse(text))),
    validateJson: vi.fn((text: string) => {
      try { JSON.parse(text); return { valid: true, error: null } }
      catch (e: any) { return { valid: false, error: e.message } }
    }),
    base64Encode: vi.fn((text: string) => btoa(text)),
    base64Decode: vi.fn((text: string) => atob(text)),
    computeMd5: vi.fn(() => 'd41d8cd98f00b204e9800998ecf8427e'),
    computeSha256: vi.fn(() => 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855'),
  }
}))

describe('DevTools Store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('should initialize with default state', () => {
    const store = useDevToolsStore()
    
    expect(store.currentTab).toBe('json')
    expect(store.inputText).toBe('')
    expect(store.outputText).toBe('')
    expect(store.isProcessing).toBe(false)
    expect(store.error).toBeNull()
    expect(store.indentSize).toBe(2)
    expect(store.encodingType).toBe('base64')
    expect(store.hashType).toBe('sha256')
    expect(store.validateResult).toBeNull()
  })

  it('hasInput should return true when inputText is not empty', () => {
    const store = useDevToolsStore()
    
    expect(store.hasInput).toBe(false)
    store.inputText = 'hello'
    expect(store.hasInput).toBe(true)
  })

  it('hasOutput should return true when outputText is not empty', () => {
    const store = useDevToolsStore()
    
    expect(store.hasOutput).toBe(false)
    store.outputText = 'world'
    expect(store.hasOutput).toBe(true)
  })

  it('setTab should update currentTab and clear error/results', () => {
    const store = useDevToolsStore()
    
    store.error = 'test error'
    store.validateResult = { isValid: true, errorMessage: undefined, lineNumber: 0, position: 0 }
    
    store.setTab('encoding')
    
    expect(store.currentTab).toBe('encoding')
    expect(store.error).toBeNull()
    expect(store.validateResult).toBeNull()
  })

  it('setInput should update inputText and clear error/results', () => {
    const store = useDevToolsStore()
    
    store.error = 'test error'
    store.validateResult = { isValid: false, errorMessage: 'invalid', lineNumber: 0, position: 0 }
    
    store.setInput('new input')
    
    expect(store.inputText).toBe('new input')
    expect(store.error).toBeNull()
    expect(store.validateResult).toBeNull()
  })

  it('setOutput should update outputText', () => {
    const store = useDevToolsStore()
    
    store.setOutput('output text')
    
    expect(store.outputText).toBe('output text')
  })

  it('setIndentSize should update indentSize', () => {
    const store = useDevToolsStore()
    
    store.setIndentSize(4)
    
    expect(store.indentSize).toBe(4)
  })

  it('setEncodingType should update encodingType and clear error', () => {
    const store = useDevToolsStore()
    
    store.error = 'test error'
    store.setEncodingType('url')
    
    expect(store.encodingType).toBe('url')
    expect(store.error).toBeNull()
  })

  it('setHashType should update hashType and clear error', () => {
    const store = useDevToolsStore()
    
    store.error = 'test error'
    store.setHashType('md5')
    
    expect(store.hashType).toBe('md5')
    expect(store.error).toBeNull()
  })

  it('clearError should reset error to null', () => {
    const store = useDevToolsStore()
    
    store.error = 'test error'
    store.clearError()
    
    expect(store.error).toBeNull()
  })

  it('clearInput should reset input/output/error/results', () => {
    const store = useDevToolsStore()
    
    store.inputText = 'input'
    store.outputText = 'output'
    store.error = 'error'
    store.validateResult = { isValid: true, errorMessage: undefined, lineNumber: 0, position: 0 }
    store.hashAllResult = { md5: '1', sha1: '2', sha256: '3', sha512: '4' }
    
    store.clearInput()
    
    expect(store.inputText).toBe('')
    expect(store.outputText).toBe('')
    expect(store.error).toBeNull()
    expect(store.validateResult).toBeNull()
    expect(store.hashAllResult).toBeNull()
  })

  it('clearOutput should reset output/error/results', () => {
    const store = useDevToolsStore()
    
    store.inputText = 'input'
    store.outputText = 'output'
    store.error = 'error'
    store.validateResult = { isValid: true, errorMessage: undefined, lineNumber: 0, position: 0 }
    store.hashAllResult = { md5: '1', sha1: '2', sha256: '3', sha512: '4' }
    
    store.clearOutput()
    
    expect(store.inputText).toBe('input')
    expect(store.outputText).toBe('')
    expect(store.error).toBeNull()
    expect(store.validateResult).toBeNull()
    expect(store.hashAllResult).toBeNull()
  })

  it('swapInputOutput should swap input and output text', () => {
    const store = useDevToolsStore()
    
    store.inputText = 'input'
    store.outputText = 'output'
    store.error = 'error'
    store.validateResult = { isValid: true, errorMessage: undefined, lineNumber: 0, position: 0 }
    
    store.swapInputOutput()
    
    expect(store.inputText).toBe('output')
    expect(store.outputText).toBe('input')
    expect(store.error).toBeNull()
    expect(store.validateResult).toBeNull()
  })

  it('formatJson should set error when input is empty', async () => {
    const store = useDevToolsStore()
    
    store.inputText = ''
    await store.formatJson()
    
    expect(store.error).toBe('请输入JSON文本')
    expect(store.isProcessing).toBe(false)
  })

  it('setConverterDirection should update converterDirection', () => {
    const store = useDevToolsStore()
    
    store.setConverterDirection('yaml-to-json')
    
    expect(store.converterDirection).toBe('yaml-to-json')
  })

  it('setJsonPathExpression should update jsonPathExpression', () => {
    const store = useDevToolsStore()
    
    store.setJsonPathExpression('$.name')
    
    expect(store.jsonPathExpression).toBe('$.name')
  })
})
