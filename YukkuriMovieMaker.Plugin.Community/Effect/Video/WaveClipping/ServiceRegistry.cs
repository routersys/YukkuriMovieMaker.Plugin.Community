using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.WaveClipping
{
    internal sealed class ServiceRegistry : IDisposable
    {
        private static readonly Lazy<ServiceRegistry> _instance =
            new(static () => new ServiceRegistry(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static ServiceRegistry Instance => _instance.Value;

        private readonly GraphicsEffectPool _effectPool = new();
        private int _disposed;

        private ServiceRegistry() { }

        public GraphicsEffectPool EffectPool
        {
            get
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
                return _effectPool;
            }
        }

        public WaveClippingEffectProcessor CreateProcessor(
            IGraphicsDevicesAndContext devices,
            WaveClippingEffect effect)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
            return new WaveClippingEffectProcessor(devices, effect);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            _effectPool.Dispose();
        }
    }
}