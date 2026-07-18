using System.Collections;

namespace OpenForgeSelf.Backend.Plugins.SystemMonitor.Services;

public class CircularBuffer<T> : IEnumerable<T>
{
    private readonly T[] _buffer;
    private int _head;
    private int _tail;
    private int _count;
    private readonly object _lock = new();

    public int Capacity => _buffer.Length;
    public int Count => _count;

    public CircularBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero.", nameof(capacity));
        _buffer = new T[capacity];
        _head = 0;
        _tail = 0;
        _count = 0;
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % _buffer.Length;
            if (_count == _buffer.Length)
            {
                _tail = (_tail + 1) % _buffer.Length;
            }
            else
            {
                _count++;
            }
        }
    }

    public List<T> GetAll()
    {
        lock (_lock)
        {
            var result = new List<T>(_count);
            for (int i = 0; i < _count; i++)
            {
                int index = (_tail + i) % _buffer.Length;
                result.Add(_buffer[index]);
            }
            return result;
        }
    }

    public List<T> GetLast(int count)
    {
        lock (_lock)
        {
            if (count >= _count)
                return GetAll();
            var result = new List<T>(count);
            int start = _count - count;
            for (int i = start; i < _count; i++)
            {
                int index = (_tail + i) % _buffer.Length;
                result.Add(_buffer[index]);
            }
            return result;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _tail = 0;
            _count = 0;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return GetAll().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
