using System.Collections.Immutable;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using FrameTime = YukkuriMovieMaker.Player.Video.FrameTime;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenEditorViewModel : Bindable, IDisposable
    {
        readonly DisposeCollector disposer = new();
        readonly IEditorInfo info;
        readonly ITimelineSourceAndDevices source;
        readonly PenShapeParameter document = new();
        readonly IShapeSource documentSource;
        readonly PenPreviewRenderer previewRenderer;
        readonly TimelineItemSourceDescription documentDescription;
        readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

        int layerNumber;
        bool isRenderQueued;

        public double CanvasWidth { get; }

        public double CanvasHeight { get; }

        public BitmapSource BackgroundImage { get => backgroundImage; private set => Set(ref backgroundImage, value); }
        BitmapSource backgroundImage;

        public BitmapSource? DocumentImage { get => documentImage; private set => Set(ref documentImage, value); }
        BitmapSource? documentImage;

        public ImmutableList<PenLayer> Layers
        {
            get => document.Layers;
            set => SetLayers(value, value.IsEmpty ? null : value[^1]);
        }

        public ImmutableList<PenLayer> DisplayLayers { get => displayLayers; private set => Set(ref displayLayers, value); }
        ImmutableList<PenLayer> displayLayers = [];

        public PenLayer? ActiveLayer
        {
            get => activeLayer;
            set
            {
                if (Set(ref activeLayer, value))
                    UpdateCommands();
            }
        }
        PenLayer? activeLayer;

        public Color WetInkColor { get => wetInkColor; set => Set(ref wetInkColor, value); }
        Color wetInkColor = PenSettings.Default.PenStyle.StrokeColor;

        public double WetInkThickness { get => wetInkThickness; set => Set(ref wetInkThickness, value); }
        double wetInkThickness = PenSettings.Default.PenStyle.StrokeThickness;

        public ActionCommand AddLayerCommand { get; }

        public ActionCommand DuplicateLayerCommand { get; }

        public ActionCommand DeleteLayerCommand { get; }

        public ActionCommand MoveLayerUpCommand { get; }

        public ActionCommand MoveLayerDownCommand { get; }

        public PenEditorViewModel(IEditorInfo info)
        {
            this.info = info;
            source = info.CreateTimelineVideoSource();
            disposer.Collect(source);

            CanvasWidth = info.VideoInfo.Width;
            CanvasHeight = info.VideoInfo.Height;

            previewRenderer = new PenPreviewRenderer(source.Devices);
            disposer.Collect(previewRenderer);
            documentSource = document.CreateShapeSource(source.Devices);
            disposer.Collect(documentSource);

            var fps = info.VideoInfo.FPS;
            var screenSize = new System.Drawing.Size(info.VideoInfo.Width, info.VideoInfo.Height);
            var timelineDescription = new TimelineSourceDescription(
                screenSize,
                new FrameTime(0, fps),
                new FrameTime(1, fps),
                fps,
                TimelineSourceUsage.Paused,
                Guid.Empty,
                []);
            documentDescription = new TimelineItemSourceDescription(timelineDescription, 0, 1, 0);

            AddLayerCommand = new ActionCommand(_ => true, _ => AddLayer());
            DuplicateLayerCommand = new ActionCommand(_ => activeLayer is not null, _ => DuplicateLayer());
            DeleteLayerCommand = new ActionCommand(_ => activeLayer is not null && document.Layers.Count > 1, _ => DeleteLayer());
            MoveLayerUpCommand = new ActionCommand(_ => CanMoveLayer(1), _ => MoveLayer(1));
            MoveLayerDownCommand = new ActionCommand(_ => CanMoveLayer(-1), _ => MoveLayer(-1));

            document.UndoRedoCommandCreated += OnDocumentChanged;

            backgroundImage = BackgroundImage = RenderBackground();
            AddLayer();
            UpdateDocumentImage();
        }

        public void AddStroke(StylusPointCollection stylusPoints)
        {
            if (stylusPoints.Count == 0)
                return;

            var layer = activeLayer;
            if (layer is null || layer.IsLocked || !layer.IsVisible)
                return;

            var stroke = new Stroke(stylusPoints, PenStyleFactory.CreatePen());
            layer.Strokes = layer.Strokes.Add(new SerializableStroke(stroke));
        }

        void AddLayer()
        {
            var layer = new PenLayer { Name = CreateLayerName() };
            var layers = document.Layers;
            var index = activeLayer is null ? layers.Count : layers.IndexOf(activeLayer) + 1;
            SetLayers(layers.Insert(index, layer), layer);
        }

        void DuplicateLayer()
        {
            var layer = activeLayer;
            if (layer is null)
                return;

            var copy = new PenLayer
            {
                Name = CreateLayerName(),
                IsVisible = layer.IsVisible,
                IsLocked = layer.IsLocked,
                BlendMode = layer.BlendMode,
                IsRangeOverridden = layer.IsRangeOverridden,
                Strokes = layer.Strokes,
            };
            copy.Opacity.CopyFrom(layer.Opacity);
            copy.Length.CopyFrom(layer.Length);
            copy.Offset.CopyFrom(layer.Offset);

            var layers = document.Layers;
            SetLayers(layers.Insert(layers.IndexOf(layer) + 1, copy), copy);
        }

        void DeleteLayer()
        {
            var layer = activeLayer;
            if (layer is null)
                return;

            var layers = document.Layers;
            var index = layers.IndexOf(layer);
            if (index < 0 || layers.Count <= 1)
                return;

            var next = layers.RemoveAt(index);
            SetLayers(next, next[Math.Min(index, next.Count - 1)]);
        }

        bool CanMoveLayer(int delta)
        {
            if (activeLayer is null)
                return false;

            var index = document.Layers.IndexOf(activeLayer);
            var target = index + delta;
            return index >= 0 && target >= 0 && target < document.Layers.Count;
        }

        void MoveLayer(int delta)
        {
            var layer = activeLayer;
            if (layer is null)
                return;

            var layers = document.Layers;
            var index = layers.IndexOf(layer);
            var target = index + delta;
            if (index < 0 || target < 0 || target >= layers.Count)
                return;

            SetLayers(layers.RemoveAt(index).Insert(target, layer), layer);
        }

        void SetLayers(ImmutableList<PenLayer> layers, PenLayer? active)
        {
            document.Layers = layers;
            DisplayLayers = layers.Reverse();
            ActiveLayer = active;
            OnPropertyChanged(nameof(Layers));
            UpdateCommands();
            InvalidateDocument();
        }

        string CreateLayerName()
        {
            layerNumber++;
            return string.Format(Texts.LayerDefaultName, layerNumber);
        }

        void UpdateCommands()
        {
            DuplicateLayerCommand.RaiseCanExecuteChanged();
            DeleteLayerCommand.RaiseCanExecuteChanged();
            MoveLayerUpCommand.RaiseCanExecuteChanged();
            MoveLayerDownCommand.RaiseCanExecuteChanged();
        }

        void OnDocumentChanged(object? sender, YukkuriMovieMaker.UndoRedo.UndoRedoEventArgs e)
        {
            InvalidateDocument();
        }

        void InvalidateDocument()
        {
            if (isRenderQueued)
                return;

            isRenderQueued = true;
            dispatcher.InvokeAsync(() =>
            {
                isRenderQueued = false;
                UpdateDocumentImage();
            }, DispatcherPriority.Render);
        }

        void UpdateDocumentImage()
        {
            documentSource.Update(documentDescription);
            DocumentImage = previewRenderer.Render(documentSource.Output, info.VideoInfo.Width, info.VideoInfo.Height);
        }

        BitmapSource RenderBackground()
        {
            var time = info.ItemPosition.Time < TimeSpan.Zero
                ? info.ItemPosition.Time
                : info.ItemDuration.Time < info.ItemPosition.Time
                ? info.VideoInfo.GetTimeFrom(info.ItemPosition.Frame + info.ItemDuration.Frame - 1)
                : info.TimelinePosition.Time;
            source.Update(time, TimelineSourceUsage.Paused);
            return source.RenderBitmapSource();
        }

        public void Dispose()
        {
            document.UndoRedoCommandCreated -= OnDocumentChanged;
            disposer.Dispose();
        }
    }
}
