using System.Collections.Immutable;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal sealed class PenLayerEffectChain(IGraphicsDevicesAndContext devices) : IDisposable
    {
        readonly List<IVideoEffectProcessor> processors = [];
        readonly List<ID2D1Image?> inputs = [];
        ImmutableList<IVideoEffect> effects = [];
        ID2D1Image? output;

        public bool Synchronize(ImmutableList<IVideoEffect> next)
        {
            if (ReferenceEquals(effects, next))
                return false;

            var reusable = new Dictionary<IVideoEffect, IVideoEffectProcessor>(processors.Count);
            for (var i = 0; i < effects.Count && i < processors.Count; i++)
                reusable.TryAdd(effects[i], processors[i]);

            var updated = new List<IVideoEffectProcessor>(next.Count);
            foreach (var effect in next)
                updated.Add(reusable.Remove(effect, out var processor) ? processor : effect.CreateVideoEffect(devices));

            foreach (var processor in reusable.Values)
            {
                processor.ClearInput();
                processor.Dispose();
            }

            processors.Clear();
            processors.AddRange(updated);
            inputs.Clear();
            for (var i = 0; i < processors.Count; i++)
                inputs.Add(null);

            effects = next;
            output = null;
            return true;
        }

        public ID2D1Image Apply(ID2D1Image image, EffectDescription description, out bool isOutputChanged)
        {
            var current = image;
            for (var i = 0; i < processors.Count; i++)
            {
                if (!effects[i].IsEnabled)
                    continue;

                if (!ReferenceEquals(inputs[i], current))
                {
                    processors[i].SetInput(current);
                    inputs[i] = current;
                }

                var draw = processors[i].Update(description);
                current = processors[i].Output;
                description = description with { DrawDescription = draw };
            }

            isOutputChanged = !ReferenceEquals(output, current);
            output = current;
            return current;
        }

        public void Dispose()
        {
            foreach (var processor in processors)
            {
                processor.ClearInput();
                processor.Dispose();
            }
            processors.Clear();
            inputs.Clear();
            effects = [];
            output = null;
        }
    }
}
