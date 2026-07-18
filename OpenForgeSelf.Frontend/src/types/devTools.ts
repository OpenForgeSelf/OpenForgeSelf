export type DevToolTab = 'json' | 'yaml' | 'xml' | 'converter' | 'encoding' | 'hash' | 'encrypt' | 'regex' | 'timestamp' | 'color' | 'jwt' | 'uuid' | 'qrcode'

export type EncodingType = 'base64' | 'url' | 'unicode' | 'html' | 'hex'

export type HashType = 'md5' | 'sha1' | 'sha256' | 'sha512'

export type EncryptMode = 'hmac' | 'aes'

export type HashAlgorithm = 'md5' | 'sha1' | 'sha256' | 'sha384' | 'sha512'

export interface ValidateResult {
  isValid: boolean
  errorMessage?: string
  lineNumber: number
  position: number
}

export interface HashAllResult {
  md5: string
  sha1: string
  sha256: string
  sha512: string
}

export interface ApiResponse<T> {
  success: boolean
  data?: T
  message: string
  code: number
}

export interface RegexMatchItem {
  index: number
  length: number
  value: string
  groups: RegexGroupItem[]
}

export interface RegexGroupItem {
  name: string
  value: string
  index: number
  length: number
  success: boolean
}

export interface RegexMatchResult {
  success: boolean
  matchCount: number
  captureGroupCount: number
  matches: RegexMatchItem[]
  error?: string
}

export interface RegexReplaceResult {
  result: string
  replacementCount: number
  error?: string
}

export interface RegexSplitResult {
  parts: string[]
  error?: string
}

export interface RegexPatternItem {
  name: string
  pattern: string
  description: string
  category: string
  example?: string
}

export interface TimestampCurrentResult {
  timestampSeconds: number
  timestampMilliseconds: number
  dateTimeIso: string
  dateTimeLocal: string
}

export interface TimestampConvertResult {
  timestampSeconds: number
  timestampMilliseconds: number
  dateTimeIso: string
  dateTimeLocal: string
  formats: Record<string, string>
  timezone?: string
}

export interface TimezoneItem {
  id: string
  displayName: string
  baseUtcOffset: string
}

export interface TimezoneConvertResult {
  fromTimestamp: number
  toTimestamp: number
  fromDateTime: string
  toDateTime: string
  fromZone: string
  toZone: string
}

export interface ColorConvertResult {
  hex: string
  hexWithAlpha: string
  r: number
  g: number
  b: number
  a: number
  h: number
  s: number
  l: number
  rgbString: string
  rgbaString: string
  hslString: string
  hslaString: string
}

export interface ColorPaletteResult {
  baseColor: string
  scheme: string
  colors: string[]
}

export interface ColorContrastResult {
  ratio: number
  aaNormal: boolean
  aaLarge: boolean
  aaaNormal: boolean
  aaaLarge: boolean
  level: string
}

export interface JwtDecodeResult {
  success: boolean
  header?: Record<string, unknown>
  payload?: Record<string, unknown>
  signature?: string
  isExpired: boolean
  issuedAt?: string
  expiration?: string
  timeRemaining?: string
  errorMessage?: string
}

export interface JwtValidateResult {
  isValid: boolean
  errorMessage?: string
}

export interface JwtGenerateResult {
  token: string
  issuedAt: string
  expiration?: string
}

export interface UuidGenerateResult {
  ids: string[]
  version: string
  count: number
}

export interface SnowflakeIdInfo {
  id: string
  timestamp: string
  workerId: number
  datacenterId: number
  sequence: number
}

export interface SnowflakeGenerateResult {
  ids: SnowflakeIdInfo[]
  count: number
}

export interface QrCodeGenerateResult {
  imageBase64: string
  size: number
  level: string
}
