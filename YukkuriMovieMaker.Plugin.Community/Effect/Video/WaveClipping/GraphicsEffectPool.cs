using System.Collections.Concurrent;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.WaveClipping
{
    internal sealed class GraphicsEffectPool : IDisposable
    {
        private const int MaxPoolSizePerDevice = 1;
        private const long ExpirationMs = 7000;
        private const int TimerIntervalMs = 1000;

        private readonly ConcurrentDictionary<IGraphicsDevicesAndContext, Bucket> _buckets = new();
        private readonly Timer _timer;
        private int _disposed;

        public GraphicsEffectPool()
        {
            _timer = new Timer(OnTimerTick, null, TimerIntervalMs, TimerIntervalMs);
        }

        public WaveClippingCustomEffect Rent(IGraphicsDevicesAndContext devices)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

            var bucket = _buckets.GetOrAdd(devices, static _ => new Bucket());
            if (bucket.TryRent(out var pooled))
                return pooled!;

            return new WaveClippingCustomEffect(devices);
        }

        public void Return(IGraphicsDevicesAndContext devices, WaveClippingCustomEffect effect)
        {
            if (!effect.IsEnabled || Volatile.Read(ref _disposed) == 1)
            {
                effect.Dispose();
                return;
            }

            effect.ClearInput();

            var bucket = _buckets.GetOrAdd(devices, static _ => new Bucket());
            if (!bucket.TryReturn(effect, MaxPoolSizePerDevice))
                effect.Dispose();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            _timer.Dispose();

            foreach (var bucket in _buckets.Values)
                bucket.DisposeAll();

            _buckets.Clear();
        }

        private void OnTimerTick(object? state)
        {
            if (Volatile.Read(ref _disposed) == 1)
                return;

            foreach (var kvp in _buckets)
            {
                kvp.Value.PurgeExpired(ExpirationMs);

                if (kvp.Value.IsEmpty)
                    _buckets.TryRemove(kvp);
            }
        }

        private sealed class Bucket
        {
            private readonly object _lock = new();
            private readonly List<PooledEntry> _entries = new();

            public bool IsEmpty
            {
                get { lock (_lock) { return _entries.Count == 0; } }
            }

            public bool TryRent(out WaveClippingCustomEffect? effect)
            {
                lock (_lock)
                {
                    for (int i = _entries.Count - 1; i >= 0; i--)
                    {
                        var entry = _entries[i];
                        _entries.RemoveAt(i);
                        if (entry.Effect.IsEnabled)
                        {
                            effect = entry.Effect;
                            return true;
                        }
                        entry.Effect.Dispose();
                    }
                }
                effect = null;
                return false;
            }

            public bool TryReturn(WaveClippingCustomEffect effect, int maxSize)
            {
                lock (_lock)
                {
                    if (_entries.Count >= maxSize)
                        return false;
                    _entries.Add(new PooledEntry(effect));
                    return true;
                }
            }

            public void PurgeExpired(long expirationMs)
            {
                lock (_lock)
                {
                    var cutoff = Environment.TickCount64 - expirationMs;
                    for (int i = _entries.Count - 1; i >= 0; i--)
                    {
                        if (_entries[i].ReturnTimeTicks < cutoff)
                        {
                            var effect = _entries[i].Effect;
                            _entries.RemoveAt(i);
                            try { effect.Dispose(); } catch (Exception) { }
                        }
                    }
                }
            }

            public void DisposeAll()
            {
                lock (_lock)
                {
                    foreach (var entry in _entries)
                    {
                        try { entry.Effect.Dispose(); } catch (Exception) { }
                    }
                    _entries.Clear();
                }
            }
        }

        private readonly struct PooledEntry
        {
            public readonly WaveClippingCustomEffect Effect;
            public readonly long ReturnTimeTicks;

            public PooledEntry(WaveClippingCustomEffect effect)
            {
                Effect = effect;
                ReturnTimeTicks = Environment.TickCount64;
            }
        }
    }
}