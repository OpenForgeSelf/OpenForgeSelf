import type {
  ValidateResult,
  HashAllResult,
  RegexMatchResult,
  RegexReplaceResult,
  RegexSplitResult,
  RegexPatternItem,
  TimestampCurrentResult,
  TimestampConvertResult,
  TimezoneItem,
  TimezoneConvertResult,
  ColorConvertResult,
  ColorPaletteResult,
  ColorContrastResult,
  JwtDecodeResult,
  JwtValidateResult,
  JwtGenerateResult,
  UuidGenerateResult,
  SnowflakeGenerateResult,
  QrCodeGenerateResult,
} from '@/types/devTools'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

async function request<T>(url: string, body: object): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${url}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
  })

  const data = await response.json()

  if (!response.ok || !data.success) {
    throw new Error(data.message || data.error || `HTTP error: ${response.status}`)
  }

  return data.data !== undefined ? data.data : data
}

async function formatJson(text: string, indentSize: number = 2): Promise<string> {
  return request<string>('/devtools/json/format', { text, indentSize })
}

async function minifyJson(text: string): Promise<string> {
  return request<string>('/devtools/json/minify', { text })
}

async function validateJson(text: string): Promise<ValidateResult> {
  return request<ValidateResult>('/devtools/json/validate', { text })
}

async function jsonPathQuery(text: string, expression: string): Promise<string> {
  return request<string>('/devtools/json/jsonpath', { text, expression })
}

async function jsonToYaml(text: string): Promise<string> {
  return request<string>('/devtools/json/to-yaml', { text })
}

async function yamlToJson(text: string): Promise<string> {
  return request<string>('/devtools/yaml/to-json', { text })
}

async function formatYaml(text: string): Promise<string> {
  return request<string>('/devtools/yaml/format', { text })
}

async function validateYaml(text: string): Promise<ValidateResult> {
  return request<ValidateResult>('/devtools/yaml/validate', { text })
}

async function formatXml(text: string, indentSize: number = 2): Promise<string> {
  return request<string>('/devtools/xml/format', { text, indentSize })
}

async function minifyXml(text: string): Promise<string> {
  return request<string>('/devtools/xml/minify', { text })
}

async function validateXml(text: string): Promise<ValidateResult> {
  return request<ValidateResult>('/devtools/xml/validate', { text })
}

async function base64Encode(text: string): Promise<string> {
  return request<string>('/devtools/encode/base64', { text })
}

async function base64Decode(text: string): Promise<string> {
  return request<string>('/devtools/decode/base64', { text })
}

async function urlEncode(text: string): Promise<string> {
  return request<string>('/devtools/encode/url', { text })
}

async function urlDecode(text: string): Promise<string> {
  return request<string>('/devtools/decode/url', { text })
}

async function unicodeEncode(text: string): Promise<string> {
  return request<string>('/devtools/encode/unicode', { text })
}

async function unicodeDecode(text: string): Promise<string> {
  return request<string>('/devtools/decode/unicode', { text })
}

async function htmlEncode(text: string): Promise<string> {
  return request<string>('/devtools/encode/html', { text })
}

async function htmlDecode(text: string): Promise<string> {
  return request<string>('/devtools/decode/html', { text })
}

async function hexEncode(text: string): Promise<string> {
  return request<string>('/devtools/encode/hex', { text })
}

async function hexDecode(text: string): Promise<string> {
  return request<string>('/devtools/decode/hex', { text })
}

async function computeMd5(text: string): Promise<string> {
  return request<string>('/devtools/hash/md5', { text })
}

async function computeSha1(text: string): Promise<string> {
  return request<string>('/devtools/hash/sha1', { text })
}

async function computeSha256(text: string): Promise<string> {
  return request<string>('/devtools/hash/sha256', { text })
}

async function computeSha512(text: string): Promise<string> {
  return request<string>('/devtools/hash/sha512', { text })
}

async function computeAllHashes(text: string): Promise<HashAllResult> {
  return request<HashAllResult>('/devtools/hash/all', { text })
}

async function computeHmac(text: string, key: string, algorithm: string): Promise<string> {
  return request<string>('/devtools/hash/hmac', { text, key, algorithm })
}

async function aesEncrypt(text: string, key: string, iv?: string): Promise<string> {
  return request<string>('/devtools/encrypt/aes', { text, key, iv })
}

async function aesDecrypt(text: string, key: string, iv?: string): Promise<string> {
  return request<string>('/devtools/decrypt/aes', { text, key, iv })
}

async function requestGet<T>(url: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${url}`, {
    method: 'GET',
    headers: {
      'Content-Type': 'application/json',
    },
  })

  const data = await response.json()

  if (!response.ok || !data.success) {
    throw new Error(data.message || data.error || `HTTP error: ${response.status}`)
  }

  return data.data !== undefined ? data.data : data
}

async function testRegex(
  pattern: string,
  input: string,
  options?: {
    ignoreCase?: boolean
    multiline?: boolean
    singleline?: boolean
    ignorePatternWhitespace?: boolean
    rightToLeft?: boolean
  }
): Promise<RegexMatchResult> {
  return request<RegexMatchResult>('/devtools/regex/test', {
    pattern,
    input,
    ignoreCase: options?.ignoreCase ?? false,
    multiline: options?.multiline ?? false,
    singleline: options?.singleline ?? false,
    ignorePatternWhitespace: options?.ignorePatternWhitespace ?? false,
    rightToLeft: options?.rightToLeft ?? false,
  })
}

async function regexReplace(
  pattern: string,
  input: string,
  replacement: string,
  options?: {
    ignoreCase?: boolean
    multiline?: boolean
    singleline?: boolean
    ignorePatternWhitespace?: boolean
    rightToLeft?: boolean
  }
): Promise<RegexReplaceResult> {
  return request<RegexReplaceResult>('/devtools/regex/replace', {
    pattern,
    input,
    replacement,
    ignoreCase: options?.ignoreCase ?? false,
    multiline: options?.multiline ?? false,
    singleline: options?.singleline ?? false,
    ignorePatternWhitespace: options?.ignorePatternWhitespace ?? false,
    rightToLeft: options?.rightToLeft ?? false,
  })
}

async function regexSplit(
  pattern: string,
  input: string,
  options?: {
    ignoreCase?: boolean
    multiline?: boolean
    singleline?: boolean
    ignorePatternWhitespace?: boolean
    rightToLeft?: boolean
  }
): Promise<RegexSplitResult> {
  return request<RegexSplitResult>('/devtools/regex/split', {
    pattern,
    input,
    ignoreCase: options?.ignoreCase ?? false,
    multiline: options?.multiline ?? false,
    singleline: options?.singleline ?? false,
    ignorePatternWhitespace: options?.ignorePatternWhitespace ?? false,
    rightToLeft: options?.rightToLeft ?? false,
  })
}

async function getRegexPatterns(category?: string): Promise<RegexPatternItem[]> {
  const url = category ? `/devtools/regex/patterns?category=${encodeURIComponent(category)}` : '/devtools/regex/patterns'
  return requestGet<RegexPatternItem[]>(url)
}

async function getCurrentTimestamp(): Promise<TimestampCurrentResult> {
  return requestGet<TimestampCurrentResult>('/devtools/timestamp/current')
}

async function timestampToDateTime(
  timestamp: number,
  timeUnit?: string,
  timezone?: string
): Promise<TimestampConvertResult> {
  return request<TimestampConvertResult>('/devtools/timestamp/to-datetime', {
    timestamp,
    timeUnit,
    timezone,
  })
}

async function dateTimeToTimestamp(
  dateTime: string,
  timeUnit?: string,
  timezone?: string
): Promise<TimestampConvertResult> {
  return request<TimestampConvertResult>('/devtools/timestamp/from-datetime', {
    dateTime,
    timeUnit,
    timezone,
  })
}

async function formatTimestamp(
  timestamp: number,
  format: string,
  timeUnit?: string,
  timezone?: string
): Promise<TimestampConvertResult> {
  return request<TimestampConvertResult>('/devtools/timestamp/format', {
    timestamp,
    format,
    timeUnit,
    timezone,
  })
}

async function getTimezoneList(): Promise<TimezoneItem[]> {
  return requestGet<TimezoneItem[]>('/devtools/timestamp/timezones')
}

async function convertTimezone(
  timestamp: number,
  fromZone: string,
  toZone: string,
  timeUnit?: string
): Promise<TimezoneConvertResult> {
  return request<TimezoneConvertResult>('/devtools/timestamp/convert-timezone', {
    timestamp,
    fromZone,
    toZone,
    timeUnit,
  })
}

async function convertColor(colorRequest: {
  hex?: string
  r?: number
  g?: number
  b?: number
  h?: number
  s?: number
  l?: number
  alpha?: number
}): Promise<ColorConvertResult> {
  return request<ColorConvertResult>('/devtools/color/convert', colorRequest)
}

async function generateColorPalette(
  baseColor: string,
  count?: number,
  scheme?: string
): Promise<ColorPaletteResult> {
  return request<ColorPaletteResult>('/devtools/color/palette', {
    baseColor,
    count: count ?? 5,
    scheme: scheme ?? 'analogous',
  })
}

async function checkColorContrast(
  foreground: string,
  background: string
): Promise<ColorContrastResult> {
  return request<ColorContrastResult>('/devtools/color/contrast', {
    foreground,
    background,
  })
}

async function decodeJwt(token: string): Promise<JwtDecodeResult> {
  return request<JwtDecodeResult>('/devtools/jwt/decode', { token })
}

async function validateJwt(
  token: string,
  secret: string,
  algorithm: string = 'HS256'
): Promise<JwtValidateResult> {
  return request<JwtValidateResult>('/devtools/jwt/validate', {
    token,
    secret,
    algorithm,
  })
}

async function generateJwt(
  payload: Record<string, unknown>,
  secret: string,
  algorithm: string = 'HS256',
  expiresInMinutes?: number
): Promise<JwtGenerateResult> {
  return request<JwtGenerateResult>('/devtools/jwt/generate', {
    payload,
    secret,
    algorithm,
    expiresInMinutes,
  })
}

async function generateUuid(
  version: string = 'v4',
  count: number = 1,
  uppercase: boolean = false,
  withHyphens: boolean = true
): Promise<UuidGenerateResult> {
  return request<UuidGenerateResult>('/devtools/uuid/generate', {
    version,
    count,
    uppercase,
    withHyphens,
  })
}

async function generateSnowflakeId(
  workerId: number = 1,
  datacenterId: number = 1,
  count: number = 1
): Promise<SnowflakeGenerateResult> {
  return request<SnowflakeGenerateResult>('/devtools/uuid/snowflake', {
    workerId,
    datacenterId,
    count,
  })
}

async function generateQrCode(
  text: string,
  size: number = 256,
  level: string = 'M',
  margin: number = 4
): Promise<QrCodeGenerateResult> {
  return request<QrCodeGenerateResult>('/devtools/qrcode/generate', {
    text,
    size,
    level,
    margin,
  })
}

async function generateCustomQrCode(
  text: string,
  size: number = 256,
  level: string = 'M',
  margin: number = 4,
  foregroundColor: string = '#000000',
  backgroundColor: string = '#FFFFFF'
): Promise<QrCodeGenerateResult> {
  return request<QrCodeGenerateResult>('/devtools/qrcode/custom', {
    text,
    size,
    level,
    margin,
    foregroundColor,
    backgroundColor,
  })
}

export const devToolsApi = {
  formatJson,
  minifyJson,
  validateJson,
  jsonPathQuery,
  jsonToYaml,
  yamlToJson,
  formatYaml,
  validateYaml,
  formatXml,
  minifyXml,
  validateXml,
  base64Encode,
  base64Decode,
  urlEncode,
  urlDecode,
  unicodeEncode,
  unicodeDecode,
  htmlEncode,
  htmlDecode,
  hexEncode,
  hexDecode,
  computeMd5,
  computeSha1,
  computeSha256,
  computeSha512,
  computeAllHashes,
  computeHmac,
  aesEncrypt,
  aesDecrypt,
  testRegex,
  regexReplace,
  regexSplit,
  getRegexPatterns,
  getCurrentTimestamp,
  timestampToDateTime,
  dateTimeToTimestamp,
  formatTimestamp,
  getTimezoneList,
  convertTimezone,
  convertColor,
  generateColorPalette,
  checkColorContrast,
  decodeJwt,
  validateJwt,
  generateJwt,
  generateUuid,
  generateSnowflakeId,
  generateQrCode,
  generateCustomQrCode,
}
