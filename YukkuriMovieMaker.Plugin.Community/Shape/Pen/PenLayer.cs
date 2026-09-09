using System.Collections.Immutable;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.UndoRedo;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenLayer : Animatable
    {
        const double IndentStep = 12;

        public Guid Id { get; init; } = Guid.NewGuid();

        public string Name { get => name; set => Set(ref name, value); }
        string name = string.Empty;

        public Guid ParentId { get => parentId; set => Set(ref parentId, value); }
        Guid parentId = Guid.Empty;

        public bool IsFolder { get => isFolder; set => Set(ref isFolder, value); }
        bool isFolder = false;

        [IgnoreUndoRedo]
        public bool IsExpanded { get => isExpanded; set => Set(ref isExpanded, value); }
        bool isExpanded = true;

        public bool IsVisible { get => isVisible; set => Set(ref isVisible, value); }
        bool isVisible = true;

        public bool IsLocked { get => isLocked; set => Set(ref isLocked, value); }
        bool isLocked = false;

        public Blend BlendMode { get => blendMode; set => Set(ref blendMode, value); }
        Blend blendMode = Blend.Normal;

        public ImmutableList<SerializableStroke> Strokes { get => strokes; set => Set(ref strokes, value); }
        ImmutableList<SerializableStroke> strokes = [];

        public Animation Opacity { get; } = new Animation(100, 0, 100);

        public bool IsClipping { get => isClipping; set => Set(ref isClipping, value); }
        bool isClipping = false;

        public bool IsRangeOverridden { get => isRangeOverridden; set => Set(ref isRangeOverridden, value); }
        bool isRangeOverridden = false;

        public Animation Length { get; } = new Animation(100, 0, 100);

        public Animation Offset { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [JsonIgnore]
        [IgnoreUndoRedo]
        public System.Windows.Media.ImageSource? Thumbnail { get => thumbnail; set => Set(ref thumbnail, value); }
        System.Windows.Media.ImageSource? thumbnail;

        [JsonIgnore]
        [IgnoreUndoRedo]
        public int Depth
        {
            get => depth;
            set
            {
                if (Set(ref depth, value))
                    OnPropertyChanged(nameof(IndentWidth));
            }
        }
        int depth;

        [JsonIgnore]
        [IgnoreUndoRedo]
        public double IndentWidth => depth * IndentStep;

        [JsonIgnore]
        [IgnoreUndoRedo]
        public bool IsRenaming { get => isRenaming; set => Set(ref isRenaming, value); }
        bool isRenaming;

        [JsonIgnore]
        [IgnoreUndoRedo]
        public string EditName { get => editName; set => Set(ref editName, value); }
        string editName = string.Empty;

        public void BeginRename()
        {
            EditName = Name;
            IsRenaming = true;
        }

        public void CommitRename()
        {
            if (!IsRenaming)
                return;

            IsRenaming = false;
            if (!string.IsNullOrWhiteSpace(EditName))
                Name = EditName;
        }

        public void CancelRename()
        {
            IsRenaming = false;
        }

        public PenLayer Clone(Guid id)
        {
            var layer = new PenLayer
            {
                Id = id,
                ParentId = ParentId,
                IsFolder = IsFolder,
                IsExpanded = IsExpanded,
                Name = Name,
                IsVisible = IsVisible,
                IsLocked = IsLocked,
                BlendMode = BlendMode,
                IsClipping = IsClipping,
                IsRangeOverridden = IsRangeOverridden,
                Strokes = Strokes,
            };
            layer.Opacity.CopyFrom(Opacity);
            layer.Length.CopyFrom(Length);
            layer.Offset.CopyFrom(Offset);
            return layer;
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Opacity, Length, Offset];
    }
}
