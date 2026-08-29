import type { HashType, TextStats } from '@/types/textTools'

function formatJson(text: string, indentSize: number = 2): string {
  const parsed = JSON.parse(text)
  return JSON.stringify(parsed, null, indentSize)
}

function minifyJson(text: string): string {
  const parsed = JSON.parse(text)
  return JSON.stringify(parsed)
}

function formatXml(text: string, indentSize: number = 2): string {
  let formatted = ''
  let indent = 0
  const indentStr = ' '.repeat(indentSize)

  text = text.trim().replace(/></g, '>\n<')

  const lines = text.split('\n')
  for (const line of lines) {
    const trimmed = line.trim()
    if (!trimmed) continue

    if (trimmed.match(/^<\//)) {
      indent = Math.max(0, indent - 1)
    }

    formatted += indentStr.repeat(indent) + trimmed + '\n'

    if (trimmed.match(/^<[^/?][^>]*[^/]>$/) && !trimmed.match(/^<[^>]*\/>$/)) {
      indent++
    }
  }

  return formatted.trim()
}

function minifyXml(text: string): string {
  return text
    .replace(/\s+/g, ' ')
    .replace(/>\s+</g, '><')
    .trim()
}

function formatHtml(text: string, indentSize: number = 2): string {
  return formatXml(text, indentSize)
}

function minifyHtml(text: string): string {
  return minifyXml(text)
}

function encodeBase64(text: string): string {
  return btoa(unescape(encodeURIComponent(text)))
}

function decodeBase64(text: string): string {
  return decodeURIComponent(escape(atob(text)))
}

function encodeUrl(text: string): string {
  return encodeURIComponent(text)
}

function decodeUrl(text: string): string {
  return decodeURIComponent(text)
}

function encodeUnicode(text: string): string {
  let result = ''
  for (let i = 0; i < text.length; i++) {
    const code = text.charCodeAt(i)
    result += '\\u' + code.toString(16).padStart(4, '0')
  }
  return result
}

function decodeUnicode(text: string): string {
  return text.replace(/\\u([0-9a-fA-F]{4})/g, (_, hex) => {
    return String.fromCharCode(parseInt(hex, 16))
  })
}

async function computeHash(text: string, type: HashType): Promise<string> {
  const encoder = new TextEncoder()
  const data = encoder.encode(text)

  let algorithm: string
  switch (type) {
    case 'md5':
      return md5(text)
    case 'sha1':
      algorithm = 'SHA-1'
      break
    case 'sha256':
      algorithm = 'SHA-256'
      break
    case 'sha512':
      algorithm = 'SHA-512'
      break
    default:
      throw new Error(`不支持的哈希类型: ${type}`)
  }

  const hashBuffer = await crypto.subtle.digest(algorithm, data)
  const hashArray = Array.from(new Uint8Array(hashBuffer))
  return hashArray.map(b => b.toString(16).padStart(2, '0')).join('')
}

function md5(text: string): string {
  function rotateLeft(value: number, shift: number): number {
    return (value << shift) | (value >>> (32 - shift))
  }

  function addUnsigned(x: number, y: number): number {
    const result = (x & 0x7FFFFFFF) + (y & 0x7FFFFFFF)
    if (x & 0x80000000) {
      if (y & 0x80000000) {
        return (result ^ 0x80000000 ^ 0x80000000) >>> 0
      } else {
        return (result ^ 0x80000000) >>> 0
      }
    } else {
      if (y & 0x80000000) {
        return (result ^ 0x80000000) >>> 0
      } else {
        return result >>> 0
      }
    }
  }

  function F(x: number, y: number, z: number): number { return (x & y) | ((~x) & z) }
  function G(x: number, y: number, z: number): number { return (x & z) | (y & (~z)) }
  function H(x: number, y: number, z: number): number { return x ^ y ^ z }
  function I(x: number, y: number, z: number): number { return y ^ (x | (~z)) }

  function FF(a: number, b: number, c: number, d: number, x: number, s: number, ac: number): number {
    a = addUnsigned(a, addUnsigned(addUnsigned(F(b, c, d), x), ac))
    return addUnsigned(rotateLeft(a, s), b)
  }

  function GG(a: number, b: number, c: number, d: number, x: number, s: number, ac: number): number {
    a = addUnsigned(a, addUnsigned(addUnsigned(G(b, c, d), x), ac))
    return addUnsigned(rotateLeft(a, s), b)
  }

  function HH(a: number, b: number, c: number, d: number, x: number, s: number, ac: number): number {
    a = addUnsigned(a, addUnsigned(addUnsigned(H(b, c, d), x), ac))
    return addUnsigned(rotateLeft(a, s), b)
  }

  function II(a: number, b: number, c: number, d: number, x: number, s: number, ac: number): number {
    a = addUnsigned(a, addUnsigned(addUnsigned(I(b, c, d), x), ac))
    return addUnsigned(rotateLeft(a, s), b)
  }

  function convertToWordArray(str: string): number[] {
    let wordCount
    let bytePosition
    const messageLength = str.length
    const numberOfWords = ((messageLength + 8) - ((messageLength + 8) % 64)) / 64 + 1
    const wordArray: number[] = new Array(numberOfWords * 16 - 1).fill(0)
    let byteCount = 0
    while (byteCount < messageLength) {
      wordCount = (byteCount - (byteCount % 4)) / 4
      bytePosition = (byteCount % 4) * 8
      wordArray[wordCount] = (wordArray[wordCount] | (str.charCodeAt(byteCount) << bytePosition))
      byteCount++
    }
    wordCount = (byteCount - (byteCount % 4)) / 4
    bytePosition = (byteCount % 4) * 8
    wordArray[wordCount] = wordArray[wordCount] | (0x80 << bytePosition)
    wordArray[numberOfWords * 16 - 2] = messageLength << 3
    wordArray[numberOfWords * 16 - 1] = messageLength >>> 29
    return wordArray
  }

  function wordToHex(value: number): string {
    let hex = ''
    for (let i = 0; i <= 3; i++) {
      const byte = (value >>> (i * 8)) & 255
      hex += ('0' + byte.toString(16)).slice(-2)
    }
    return hex
  }

  const x = convertToWordArray(text)
  let a = 1732584193
  let b = -271733879
  let c = -1732584194
  let d = 271733878

  for (let k = 0; k < x.length; k += 16) {
    const AA = a, BB = b, CC = c, DD = d

    a = FF(a, b, c, d, x[k + 0], 7, -680876936)
    d = FF(d, a, b, c, x[k + 1], 12, -389564586)
    c = FF(c, d, a, b, x[k + 2], 17, 606105819)
    b = FF(b, c, d, a, x[k + 3], 22, -1044525330)
    a = FF(a, b, c, d, x[k + 4], 7, -176418897)
    d = FF(d, a, b, c, x[k + 5], 12, 1200080426)
    c = FF(c, d, a, b, x[k + 6], 17, -1473231341)
    b = FF(b, c, d, a, x[k + 7], 22, -45705983)
    a = FF(a, b, c, d, x[k + 8], 7, 1770035416)
    d = FF(d, a, b, c, x[k + 9], 12, -1958414417)
    c = FF(c, d, a, b, x[k + 10], 17, -42063)
    b = FF(b, c, d, a, x[k + 11], 22, -1990404162)
    a = FF(a, b, c, d, x[k + 12], 7, 1804603682)
    d = FF(d, a, b, c, x[k + 13], 12, -40341101)
    c = FF(c, d, a, b, x[k + 14], 17, -1502002290)
    b = FF(b, c, d, a, x[k + 15], 22, 1236535329)

    a = GG(a, b, c, d, x[k + 1], 5, -165796510)
    d = GG(d, a, b, c, x[k + 6], 9, -1069501632)
    c = GG(c, d, a, b, x[k + 11], 14, 643717713)
    b = GG(b, c, d, a, x[k + 0], 20, -373897302)
    a = GG(a, b, c, d, x[k + 5], 5, -701558691)
    d = GG(d, a, b, c, x[k + 10], 9, 38016083)
    c = GG(c, d, a, b, x[k + 15], 14, -660478335)
    b = GG(b, c, d, a, x[k + 4], 20, -405537848)
    a = GG(a, b, c, d, x[k + 9], 5, 568446438)
    d = GG(d, a, b, c, x[k + 14], 9, -1019803690)
    c = GG(c, d, a, b, x[k + 3], 14, -187363961)
    b = GG(b, c, d, a, x[k + 8], 20, 1163531501)
    a = GG(a, b, c, d, x[k + 13], 5, -1444681467)
    d = GG(d, a, b, c, x[k + 2], 9, -51403784)
    c = GG(c, d, a, b, x[k + 7], 14, 1735328473)
    b = GG(b, c, d, a, x[k + 12], 20, -1926607734)

    a = HH(a, b, c, d, x[k + 5], 4, -378558)
    d = HH(d, a, b, c, x[k + 8], 11, -2022574463)
    c = HH(c, d, a, b, x[k + 11], 16, 1839030562)
    b = HH(b, c, d, a, x[k + 14], 23, -35309556)
    a = HH(a, b, c, d, x[k + 1], 4, -1530992060)
    d = HH(d, a, b, c, x[k + 4], 11, 1272893353)
    c = HH(c, d, a, b, x[k + 7], 16, -155497632)
    b = HH(b, c, d, a, x[k + 10], 23, -1094730640)
    a = HH(a, b, c, d, x[k + 13], 4, 681279174)
    d = HH(d, a, b, c, x[k + 0], 11, -358537222)
    c = HH(c, d, a, b, x[k + 3], 16, -722521979)
    b = HH(b, c, d, a, x[k + 6], 23, 76029189)
    a = HH(a, b, c, d, x[k + 9], 4, -640364487)
    d = HH(d, a, b, c, x[k + 12], 11, -421815835)
    c = HH(c, d, a, b, x[k + 15], 16, 530742520)
    b = HH(b, c, d, a, x[k + 2], 23, -995338651)

    a = II(a, b, c, d, x[k + 0], 6, -198630844)
    d = II(d, a, b, c, x[k + 7], 10, 1126891415)
    c = II(c, d, a, b, x[k + 14], 15, -1416354905)
    b = II(b, c, d, a, x[k + 5], 21, -57434055)
    a = II(a, b, c, d, x[k + 12], 6, 1700485571)
    d = II(d, a, b, c, x[k + 3], 10, -1894986606)
    c = II(c, d, a, b, x[k + 10], 15, -1051523)
    b = II(b, c, d, a, x[k + 1], 21, -2054922799)
    a = II(a, b, c, d, x[k + 8], 6, 1873313359)
    d = II(d, a, b, c, x[k + 15], 10, -30611744)
    c = II(c, d, a, b, x[k + 6], 15, -1560198380)
    b = II(b, c, d, a, x[k + 13], 21, 1309151649)
    a = II(a, b, c, d, x[k + 4], 6, -145523070)
    d = II(d, a, b, c, x[k + 11], 10, -1120210379)
    c = II(c, d, a, b, x[k + 2], 15, 718787259)
    b = II(b, c, d, a, x[k + 9], 21, -343485551)

    a = addUnsigned(a, AA)
    b = addUnsigned(b, BB)
    c = addUnsigned(c, CC)
    d = addUnsigned(d, DD)
  }

  return (wordToHex(a) + wordToHex(b) + wordToHex(c) + wordToHex(d)).toLowerCase()
}

function getStats(text: string): TextStats {
  const charCount = text.length
  const charCountNoSpace = text.replace(/\s/g, '').length
  const words = text.trim() ? text.trim().split(/\s+/).filter(w => w.length > 0) : []
  const wordCount = words.length
  const lineCount = text ? text.split('\n').length : 0
  const byteCount = new TextEncoder().encode(text).length

  return {
    charCount,
    charCountNoSpace,
    wordCount,
    lineCount,
    byteCount
  }
}

export const textToolsApi = {
  formatJson,
  minifyJson,
  formatXml,
  minifyXml,
  formatHtml,
  minifyHtml,
  encodeBase64,
  decodeBase64,
  encodeUrl,
  decodeUrl,
  encodeUnicode,
  decodeUnicode,
  computeHash,
  getStats
}
