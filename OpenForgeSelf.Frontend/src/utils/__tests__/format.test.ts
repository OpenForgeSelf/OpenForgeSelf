import { describe, it, expect } from 'vitest'
import {
  formatFileSize,
  formatDuration,
  formatDate,
  truncateText,
  formatPercentage
} from '../format'

describe('formatFileSize', () => {
  it('should format 0 bytes correctly', () => {
    expect(formatFileSize(0)).toBe('0 B')
  })

  it('should format negative bytes as 0', () => {
    expect(formatFileSize(-100)).toBe('0 B')
  })

  it('should format bytes correctly', () => {
    expect(formatFileSize(500)).toBe('500 B')
  })

  it('should format kilobytes correctly', () => {
    expect(formatFileSize(1024)).toBe('1 KB')
    expect(formatFileSize(1536)).toBe('1.5 KB')
  })

  it('should format megabytes correctly', () => {
    expect(formatFileSize(1048576)).toBe('1 MB')
    expect(formatFileSize(2 * 1024 * 1024)).toBe('2 MB')
  })

  it('should format gigabytes correctly', () => {
    expect(formatFileSize(1073741824)).toBe('1 GB')
  })

  it('should format terabytes correctly', () => {
    expect(formatFileSize(1099511627776)).toBe('1 TB')
  })

  it('should format 1 byte correctly', () => {
    expect(formatFileSize(1)).toBe('1 B')
  })

  it('should format 1023 bytes correctly', () => {
    expect(formatFileSize(1023)).toBe('1023 B')
  })

  it('should format decimal values correctly', () => {
    expect(formatFileSize(1536)).toBe('1.5 KB')
  })
})

describe('formatDuration', () => {
  it('should format 0 seconds correctly', () => {
    expect(formatDuration(0)).toBe('0秒')
  })

  it('should format negative seconds as 0', () => {
    expect(formatDuration(-10)).toBe('0秒')
  })

  it('should format seconds only', () => {
    expect(formatDuration(45)).toBe('45秒')
  })

  it('should format minutes and seconds', () => {
    expect(formatDuration(125)).toBe('2分5秒')
  })

  it('should format hours, minutes and seconds', () => {
    expect(formatDuration(3661)).toBe('1小时1分1秒')
  })

  it('should format exact hours', () => {
    expect(formatDuration(7200)).toBe('2小时0分0秒')
  })

  it('should format 1 second correctly', () => {
    expect(formatDuration(1)).toBe('1秒')
  })

  it('should format 59 seconds correctly', () => {
    expect(formatDuration(59)).toBe('59秒')
  })

  it('should format 60 seconds as 1 minute', () => {
    expect(formatDuration(60)).toBe('1分0秒')
  })

  it('should format 1 hour exactly', () => {
    expect(formatDuration(3600)).toBe('1小时0分0秒')
  })

  it('should handle float seconds by flooring', () => {
    expect(formatDuration(45.9)).toBe('45秒')
  })
})

describe('formatDate', () => {
  it('should format date with default format', () => {
    const date = new Date(2024, 5, 15, 10, 30, 45)
    expect(formatDate(date)).toBe('2024-06-15 10:30:45')
  })

  it('should format date with custom format', () => {
    const date = new Date(2024, 5, 15, 10, 30, 45)
    expect(formatDate(date, 'YYYY-MM-DD')).toBe('2024-06-15')
  })

  it('should format date from string', () => {
    expect(formatDate('2024-06-15T10:30:45', 'YYYY/MM/DD')).toBe('2024/06/15')
  })

  it('should format date from timestamp', () => {
    const timestamp = new Date(2024, 5, 15).getTime()
    expect(formatDate(timestamp, 'YYYY-MM-DD')).toBe('2024-06-15')
  })

  it('should return empty string for invalid date', () => {
    expect(formatDate('invalid-date')).toBe('')
  })

  it('should pad single digits with leading zero', () => {
    const date = new Date(2024, 0, 5, 3, 5, 9)
    expect(formatDate(date)).toBe('2024-01-05 03:05:09')
  })
})

describe('truncateText', () => {
  it('should return original text if shorter than maxLength', () => {
    expect(truncateText('Hello', 10)).toBe('Hello')
  })

  it('should truncate text longer than maxLength', () => {
    expect(truncateText('Hello World', 8)).toBe('Hello...')
  })

  it('should handle empty string', () => {
    expect(truncateText('', 10)).toBe('')
  })

  it('should use custom suffix', () => {
    expect(truncateText('Hello World', 8, '...')).toBe('Hello...')
  })

  it('should handle maxLength less than suffix length', () => {
    expect(truncateText('Hello World', 2, '...')).toBe('..')
  })

  it('should return suffix when maxLength equals suffix length', () => {
    expect(truncateText('Hello World', 3, '...')).toBe('...')
  })

  it('should return original text when length equals maxLength', () => {
    expect(truncateText('Hello', 5)).toBe('Hello')
  })

  it('should handle maxLength of 0', () => {
    expect(truncateText('Hello', 0)).toBe('')
  })

  it('should handle custom longer suffix', () => {
    expect(truncateText('Hello World', 10, '----')).toBe('Hello ----')
  })

  it('should handle null/undefined input gracefully', () => {
    expect(truncateText('', 10)).toBe('')
  })
})

describe('formatPercentage', () => {
  it('should return 0% when total is 0', () => {
    expect(formatPercentage(50, 0)).toBe('0%')
  })

  it('should calculate percentage correctly', () => {
    expect(formatPercentage(25, 100)).toBe('25.0%')
  })

  it('should support custom decimals', () => {
    expect(formatPercentage(1, 3, 2)).toBe('33.33%')
  })

  it('should handle 100% correctly', () => {
    expect(formatPercentage(100, 100)).toBe('100.0%')
  })

  it('should handle 0% correctly', () => {
    expect(formatPercentage(0, 100)).toBe('0.0%')
  })

  it('should handle value greater than total', () => {
    expect(formatPercentage(150, 100)).toBe('150.0%')
  })

  it('should handle negative value', () => {
    expect(formatPercentage(-25, 100)).toBe('-25.0%')
  })

  it('should handle 0 decimals', () => {
    expect(formatPercentage(33, 100, 0)).toBe('33%')
  })

  it('should handle 3 decimals', () => {
    expect(formatPercentage(1, 3, 3)).toBe('33.333%')
  })

  it('should handle value and total both 0', () => {
    expect(formatPercentage(0, 0)).toBe('0%')
  })
})
