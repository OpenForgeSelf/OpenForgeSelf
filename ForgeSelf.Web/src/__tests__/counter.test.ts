import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useCounterStore } from '../stores/counter'

describe('Counter Store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('initializes with count 0', () => {
    const store = useCounterStore()
    expect(store.count).toBe(0)
  })

  it('increments count', () => {
    const store = useCounterStore()
    store.increment()
    expect(store.count).toBe(1)
  })

  it('computes doubleCount correctly', () => {
    const store = useCounterStore()
    expect(store.doubleCount).toBe(0)
    store.increment()
    expect(store.doubleCount).toBe(2)
    store.increment()
    expect(store.doubleCount).toBe(4)
  })
})