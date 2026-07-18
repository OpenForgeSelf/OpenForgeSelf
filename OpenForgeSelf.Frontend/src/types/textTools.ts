export type TextToolTab = 'formatter' | 'encoding' | 'hash' | 'stats'

export type FormatterType = 'json' | 'xml' | 'html'

export type EncodingType = 'base64' | 'url' | 'unicode'

export type HashType = 'md5' | 'sha1' | 'sha256' | 'sha512'

export interface TextStats {
  charCount: number
  charCountNoSpace: number
  wordCount: number
  lineCount: number
  byteCount: number
}
