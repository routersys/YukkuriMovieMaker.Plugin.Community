using System.Collections.Concurrent;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.WaveClipping
{
    internal sealed class ResourceTracker : IDisposable
    {
        private readonly ConcurrentStack<IDisposable> _resources = new();
        private int _disposed;

        public T Track<T>(T resource) where T : IDisposable
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
            _resources.Push(resource);
            return resource;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            List<Exception>? exceptions = null;
            while (_resources.TryPop(out var resource))
            {
                try { resource.Dispose(); }
                catch (Exception ex)
                {
                    (exceptions ??= []).Add(ex);
                }
            }

            if (exceptions is not null)
                throw new AggregateException("One or more resources failed to dispose.", exceptions);
        }
    }
}