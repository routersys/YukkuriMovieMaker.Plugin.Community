using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.WaveClipping
{
    internal sealed class WaveClippingEffectProcessor : VideoEffectProcessorBase
    {
        private readonly WaveClippingEffect _item;
        private WaveClippingCustomEffect? _effect;

        public WaveClippingEffectProcessor(
            IGraphicsDevicesAndContext devices,
            WaveClippingEffect item)
            : base(devices)
        {
            _item = item;
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || _effect is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            _effect.Amplitude = (float)(_item.Amplitude.GetValue(frame, length, fps) / 100.0);
            _effect.Frequency = (float)_item.Frequency.GetValue(frame, length, fps);
            _effect.Phase = (float)_item.Phase.GetValue(frame, length, fps);
            _effect.EdgePosition = (float)(_item.ClipPosition.GetValue(frame, length, fps) / 100.0);
            _effect.BandWidth = (float)(_item.BandWidth.GetValue(frame, length, fps) / 100.0);
            _effect.Softness = (float)_item.Softness.GetValue(frame, length, fps);
            _effect.Mode = (float)(int)_item.Mode;
            _effect.IsInverted = _item.IsInverted ? 1.0f : 0.0f;

            return effectDescription.DrawDescription;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            var pool = ServiceRegistry.Instance.EffectPool;
            _effect = pool.Rent(devices);
            if (!_effect.IsEnabled)
            {
                pool.Return(devices, _effect);
                _effect = null;
                return null;
            }

            disposer.Collect(new PoolReturnHandle(pool, devices, _effect));
            var output = _effect.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            _effect?.SetInput(0, input, true);
        }

        protected override void ClearEffectChain()
        {
            _effect?.SetInput(0, null, true);
        }

        private sealed class PoolReturnHandle : IDisposable
        {
            private readonly GraphicsEffectPool _pool;
            private readonly IGraphicsDevicesAndContext _devices;
            private WaveClippingCustomEffect? _effect;

            public PoolReturnHandle(
                GraphicsEffectPool pool,
                IGraphicsDevicesAndContext devices,
                WaveClippingCustomEffect effect)
            {
                _pool = pool;
                _devices = devices;
                _effect = effect;
            }

            public void Dispose()
            {
                var effect = Interlocked.Exchange(ref _effect, null);
                if (effect is null) return;
                _pool.Return(_devices, effect);
            }
        }
    }
}