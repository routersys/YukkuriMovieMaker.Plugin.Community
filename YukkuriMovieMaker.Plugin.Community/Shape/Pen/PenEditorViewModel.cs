using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenEditorViewModel : Bindable, IDisposable
    {
        readonly DisposeCollector disposer = new();
        readonly IEditorInfo info;
        readonly ITimelineSourceAndDevices source;

        public double CanvasWidth { get; }

        public double CanvasHeight { get; }

        public BitmapSource BackgroundImage { get => backgroundImage; private set => Set(ref backgroundImage, value); }
        BitmapSource backgroundImage;

        public PenEditorViewModel(IEditorInfo info)
        {
            this.info = info;
            source = info.CreateTimelineVideoSource();
            disposer.Collect(source);

            CanvasWidth = info.VideoInfo.Width;
            CanvasHeight = info.VideoInfo.Height;
            backgroundImage = BackgroundImage = RenderBackground();
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
