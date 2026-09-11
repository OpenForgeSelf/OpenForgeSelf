import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { consumeTokenFromHash, installTokenHashWatcher } from '@/services/authInit';
import { STORAGE_KEY, getStoredToken } from '@/services/request';

/** 把地址栏重置为指定 path + search + fragment */
function setUrl(url: string): void {
  window.history.replaceState(null, '', url);
}

describe('consumeTokenFromHash', () => {
  beforeEach(() => {
    localStorage.clear();
    setUrl('/');
    // 屏蔽预期内的告警输出，保持测试输出干净
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    setUrl('/');
  });

  it('命中 #token= 时写入 localStorage 并清掉 fragment（保留 query）', () => {
    setUrl('/?from=tray#token=sk-abcdef1234567890');

    expect(consumeTokenFromHash()).toBe(true);
    expect(getStoredToken()).toBe('sk-abcdef1234567890');
    expect(localStorage.getItem(STORAGE_KEY)).toBe('sk-abcdef1234567890');
    // fragment 必须被清掉，query 必须保留
    expect(window.location.hash).toBe('');
    expect(window.location.search).toBe('?from=tray');
  });

  it('无 hash 时不写入，且不清空已有 token', () => {
    localStorage.setItem(STORAGE_KEY, 'sk-existing-token');
    setUrl('/');

    expect(consumeTokenFromHash()).toBe(false);
    expect(getStoredToken()).toBe('sk-existing-token');
    expect(window.location.hash).toBe('');
  });

  it('hash 中没有 token= 时不写入，但仍清掉 fragment', () => {
    localStorage.setItem(STORAGE_KEY, 'sk-existing-token');
    setUrl('/#/settings');

    expect(consumeTokenFromHash()).toBe(false);
    expect(getStoredToken()).toBe('sk-existing-token');
    expect(window.location.hash).toBe('');
  });

  it('token 长度不足 8 位时被忽略，且仍清掉 fragment', () => {
    localStorage.setItem(STORAGE_KEY, 'sk-existing-token');
    setUrl('/#token=abc');

    expect(consumeTokenFromHash()).toBe(false);
    expect(getStoredToken()).toBe('sk-existing-token');
    expect(window.location.hash).toBe('');
  });

  it('token 长度超过 200 位时被忽略，且仍清掉 fragment', () => {
    localStorage.setItem(STORAGE_KEY, 'sk-existing-token');
    setUrl(`/#token=sk-${'a'.repeat(210)}`);

    expect(consumeTokenFromHash()).toBe(false);
    expect(getStoredToken()).toBe('sk-existing-token');
    expect(window.location.hash).toBe('');
  });

  it('URL 编码的 token 会被解码后写入', () => {
    setUrl('/#token=sk%2Dabcdef1234567890');

    expect(consumeTokenFromHash()).toBe(true);
    expect(getStoredToken()).toBe('sk-abcdef1234567890');
    expect(window.location.hash).toBe('');
  });

  it('兼容 #/route?token= 形态（路由在前）', () => {
    setUrl('/#/settings?from=tray&token=sk-abcdef1234567890');

    expect(consumeTokenFromHash()).toBe(true);
    expect(getStoredToken()).toBe('sk-abcdef1234567890');
    expect(window.location.hash).toBe('');
  });

  it('非法转义串不抛异常，按未消费处理并清掉 fragment', () => {
    localStorage.setItem(STORAGE_KEY, 'sk-existing-token');
    setUrl('/#token=%E0%A4%A');

    expect(() => consumeTokenFromHash()).not.toThrow();
    expect(consumeTokenFromHash()).toBe(false);
    expect(getStoredToken()).toBe('sk-existing-token');
    expect(window.location.hash).toBe('');
  });

  it('已有旧 token 时被新 token 直接覆盖（Q5：不二次确认）', () => {
    localStorage.setItem(STORAGE_KEY, 'sk-old-token-0001');
    setUrl('/#token=sk-newtoken12345678');

    expect(consumeTokenFromHash()).toBe(true);
    expect(getStoredToken()).toBe('sk-newtoken12345678');
    expect(localStorage.getItem(STORAGE_KEY)).not.toBe('sk-old-token-0001');
  });
});

describe('installTokenHashWatcher', () => {
  let reloadSpy: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    localStorage.clear();
    setUrl('/');
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    reloadSpy = vi.fn();
    // 每个用例装一个持有独立 spy 的监听器；历史监听器指向旧 spy，不影响本用例断言
    installTokenHashWatcher(reloadSpy);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    setUrl('/');
  });

  it('同页仅改变 fragment 时也能消费 token 并触发刷新', () => {
    // 模拟「页面已打开 → 托盘再次打开」：只改 fragment，不重新加载文档
    setUrl('/#token=sk-hashchange1234567890');
    window.dispatchEvent(new HashChangeEvent('hashchange'));

    expect(getStoredToken()).toBe('sk-hashchange1234567890');
    expect(window.location.hash).toBe('');
    expect(reloadSpy).toHaveBeenCalledTimes(1);
  });

  it('fragment 中没有 token 时不刷新', () => {
    localStorage.setItem(STORAGE_KEY, 'sk-existing-token');
    setUrl('/#/settings');
    window.dispatchEvent(new HashChangeEvent('hashchange'));

    expect(getStoredToken()).toBe('sk-existing-token');
    expect(reloadSpy).not.toHaveBeenCalled();
  });
});
