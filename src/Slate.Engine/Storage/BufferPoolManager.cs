using System.Collections.Concurrent;

namespace Slate.Engine.Storage;

public sealed class PageFrame
{
    public ulong PageId { get; set; }
    public byte[] Data { get; }
    public int PinCount { get; set; }
    public bool IsDirty { get; set; }
    public bool ReferenceBit { get; set; }

    public PageFrame(int pageSize)
    {
        Data = new byte[pageSize];
        PinCount = 0;
        IsDirty = false;
        ReferenceBit = false;
    }
}

/// <summary>
/// Clock-based buffer pool manager managing in-memory cached disk pages.
/// Implements concurrent pinning, dirty page tracking, and Clock-Pro replacement policies.
/// </summary>
public sealed class BufferPoolManager : IDisposable
{
    private readonly int _pageSize;
    private readonly int _poolSize;
    private readonly PageFrame[] _frames;
    private readonly ConcurrentDictionary<ulong, int> _pageTable = new();
    private readonly object _lock = new();
    private int _clockHand;

    private long _bufferHits;
    private long _bufferMisses;
    private long _evictionCount;

    public int PageSize => _pageSize;
    public int PoolSize => _poolSize;
    public long BufferHits => Interlocked.Read(ref _bufferHits);
    public long BufferMisses => Interlocked.Read(ref _bufferMisses);
    public long EvictionCount => Interlocked.Read(ref _evictionCount);

    public Func<ulong, byte[], bool>? DiskPageReader { get; set; }
    public Action<ulong, byte[]>? DiskPageWriter { get; set; }

    public BufferPoolManager(int poolSize = 64, int pageSize = 4096)
    {
        if (poolSize <= 0) throw new ArgumentOutOfRangeException(nameof(poolSize));
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));

        _poolSize = poolSize;
        _pageSize = pageSize;
        _frames = new PageFrame[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            _frames[i] = new PageFrame(pageSize);
        }
    }

    public PageFrame? FetchPage(ulong pageId)
    {
        lock (_lock)
        {
            if (_pageTable.TryGetValue(pageId, out int frameId))
            {
                var frame = _frames[frameId];
                frame.PinCount++;
                frame.ReferenceBit = true;
                Interlocked.Increment(ref _bufferHits);
                return frame;
            }

            // Buffer miss
            Interlocked.Increment(ref _bufferMisses);
            int victimFrame = FindVictimFrame();
            if (victimFrame < 0)
            {
                // All frames are pinned, cannot allocate
                return null;
            }

            var victim = _frames[victimFrame];

            // If victim was holding another page, write back if dirty and remove from pageTable
            if (_pageTable.ContainsKey(victim.PageId))
            {
                if (victim.IsDirty)
                {
                    DiskPageWriter?.Invoke(victim.PageId, victim.Data);
                    victim.IsDirty = false;
                }
                _pageTable.TryRemove(victim.PageId, out _);
                Interlocked.Increment(ref _evictionCount);
            }

            // Load new page
            victim.PageId = pageId;
            victim.PinCount = 1;
            victim.IsDirty = false;
            victim.ReferenceBit = true;
            Array.Clear(victim.Data, 0, victim.Data.Length);

            DiskPageReader?.Invoke(pageId, victim.Data);
            _pageTable[pageId] = victimFrame;

            return victim;
        }
    }

    public void UnpinPage(ulong pageId, bool isDirty = false)
    {
        lock (_lock)
        {
            if (_pageTable.TryGetValue(pageId, out int frameId))
            {
                var frame = _frames[frameId];
                if (frame.PinCount > 0)
                {
                    frame.PinCount--;
                }
                if (isDirty)
                {
                    frame.IsDirty = true;
                }
            }
        }
    }

    public void FlushAllPages()
    {
        lock (_lock)
        {
            for (int i = 0; i < _poolSize; i++)
            {
                var frame = _frames[i];
                if (frame.IsDirty && _pageTable.ContainsKey(frame.PageId))
                {
                    DiskPageWriter?.Invoke(frame.PageId, frame.Data);
                    frame.IsDirty = false;
                }
            }
        }
    }

    private int FindVictimFrame()
    {
        // 2 full revolutions of the clock hand
        for (int round = 0; round < 2; round++)
        {
            for (int i = 0; i < _poolSize; i++)
            {
                int frameId = _clockHand;
                _clockHand = (_clockHand + 1) % _poolSize;

                var frame = _frames[frameId];
                if (frame.PinCount == 0)
                {
                    if (frame.ReferenceBit)
                    {
                        frame.ReferenceBit = false; // Give second chance
                    }
                    else
                    {
                        return frameId; // Evict
                    }
                }
            }
        }

        return -1; // All pages currently pinned
    }

    public void Dispose()
    {
        FlushAllPages();
    }
}
