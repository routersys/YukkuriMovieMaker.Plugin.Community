using System.Collections.Immutable;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenLayer : Animatable
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        public string Name { get => name; set => Set(ref name, value); }
        string name = string.Empty;

        public bool IsVisible { get => isVisible; set => Set(ref isVisible, value); }
        bool isVisible = true;

        public bool IsLocked { get => isLocked; set => Set(ref isLocked, value); }
        bool isLocked = false;

        public Blend BlendMode { get => blendMode; set => Set(ref blendMode, value); }
        Blend blendMode = Blend.Normal;

        public ImmutableList<SerializableStroke> Strokes { get => strokes; set => Set(ref strokes, value); }
        ImmutableList<SerializableStroke> strokes = [];

        public Animation Opacity { get; } = new Animation(100, 0, 100);

        public bool IsRangeOverridden { get => isRangeOverridden; set => Set(ref isRangeOverridden, value); }
        bool isRangeOverridden = false;

        public Animation Length { get; } = new Animation(100, 0, 100);

        public Animation Offset { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Opacity, Length, Offset];
    }
}
