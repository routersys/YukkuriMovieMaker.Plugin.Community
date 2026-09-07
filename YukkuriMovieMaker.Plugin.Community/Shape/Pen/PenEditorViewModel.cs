using System.Collections.Immutable;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

        public double CanvasWidth { get; }

        public double CanvasHeight { get; }

        public BitmapSource BackgroundImage { get => backgroundImage; private set => Set(ref backgroundImage, value); }
        BitmapSource backgroundImage;

        public BitmapSource? DocumentImage { get => documentImage; private set => Set(ref documentImage, value); }
        BitmapSource? documentImage;

        public ImmutableList<PenLayer> Layers
        {
            get => document.Layers;
            set
            {
                document.Layers = value;
                ActiveLayer = value.IsEmpty ? null : value[^1];
                OnPropertyChanged();
                UpdateDocumentImage();
            }
        }

        public PenLayer? ActiveLayer { get => activeLayer; set => Set(ref activeLayer, value); }
        PenLayer? activeLayer;

        public Color WetInkColor { get => wetInkColor; set => Set(ref wetInkColor, value); }
        Color wetInkColor = PenSettings.Default.PenStyle.StrokeColor;

        public double WetInkThickness { get => wetInkThickness; set => Set(ref wetInkThickness, value); }
        double wetInkThickness = PenSettings.Default.PenStyle.StrokeThickness;

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

            backgroundImage = BackgroundImage = RenderBackground();
            EnsureActiveLayer();
            UpdateDocumentImage();
        }

        public void AddStroke(StylusPointCollection stylusPoints)
        {
            if (stylusPoints.Count == 0)
                return;

            EnsureActiveLayer();
            var layer = activeLayer;
            if (layer is null)
                return;

            var stroke = new Stroke(stylusPoints, PenStyleFactory.CreatePen());
            layer.Strokes = layer.Strokes.Add(new SerializableStroke(stroke));
            UpdateDocumentImage();
        }

        void EnsureActiveLayer()
        {
            if (activeLayer is not null && document.Layers.Contains(activeLayer))
                return;
            if (!document.Layers.IsEmpty)
            {
                ActiveLayer = document.Layers[^1];
                return;
            }

            var layer = new PenLayer();
            document.Layers = [layer];
            ActiveLayer = layer;
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
            disposer.Dispose();
        }
    }
}
