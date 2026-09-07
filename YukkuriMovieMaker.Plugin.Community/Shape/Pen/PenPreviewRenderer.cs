using System.Numerics;
using System.Windows;
using System.Windows.Media.Imaging;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal sealed class PenPreviewRenderer(IGraphicsDevicesAndContext devices) : IDisposable
    {
        readonly DisposeCollector disposer = new();

        ID2D1Bitmap1? target;
        ID2D1Bitmap1? staging;
        WriteableBitmap? bitmap;
        int width;
        int height;

        public WriteableBitmap Render(ID2D1Image image, int width, int height)
        {
            EnsureResources(width, height);
            if (target is null || staging is null || bitmap is null)
                throw new InvalidOperationException("preview resources are not ready.");

            var dc = devices.DeviceContext;
            dc.Target = target;
            dc.BeginDraw();
            dc.Clear(new Color4(0, 0, 0, 0));
            dc.DrawImage(image, new Vector2(width / 2f, height / 2f));
            dc.EndDraw();
            dc.Target = null;

            staging.CopyFromBitmap(target);
            var mapped = staging.Map(MapOptions.Read);
            try
            {
                bitmap.Lock();
                CopyPixels(mapped, bitmap, width, height);
                bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                bitmap.Unlock();
                staging.Unmap();
            }
            return bitmap;
        }

        static unsafe void CopyPixels(MappedRectangle mapped, WriteableBitmap bitmap, int width, int height)
        {
            var source = (byte*)mapped.Bits;
            var destination = (byte*)bitmap.BackBuffer;
            var sourceStride = mapped.Pitch;
            var destinationStride = bitmap.BackBufferStride;
            var rowBytes = (uint)(width * 4);
            for (var y = 0; y < height; y++)
                Buffer.MemoryCopy(source + (nint)y * sourceStride, destination + (nint)y * destinationStride, rowBytes, rowBytes);
        }

        void EnsureResources(int width, int height)
        {
            if (this.width == width && this.height == height && target is not null && staging is not null && bitmap is not null)
                return;

            if (target is not null)
                disposer.RemoveAndDispose(ref target);
            if (staging is not null)
                disposer.RemoveAndDispose(ref staging);

            var dc = devices.DeviceContext;
            var format = new Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied);
            var size = new SizeI(width, height);
            target = dc.CreateBitmap(size, new BitmapProperties1(format, 96, 96, BitmapOptions.Target));
            disposer.Collect(target);
            staging = dc.CreateBitmap(size, new BitmapProperties1(format, 96, 96, BitmapOptions.CannotDraw | BitmapOptions.CpuRead));
            disposer.Collect(staging);
            bitmap = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null);

            this.width = width;
            this.height = height;
        }

        public void Dispose()
        {
            disposer.Dispose();
        }
    }
}
