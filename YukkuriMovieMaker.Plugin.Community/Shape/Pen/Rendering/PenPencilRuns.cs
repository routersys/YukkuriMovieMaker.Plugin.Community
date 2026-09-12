using System.Numerics;
using Vortice;
using Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
{
    sealed class PenPencilRuns : IDisposable
    {
        const float LayerPaddingPixels = 16f;

        readonly int[] starts;
        readonly byte[] levels;
        readonly ID2D1Ink?[] inks;
        readonly RawRectF[] bounds;
        readonly int[] builtStarts;
        readonly int[] builtEnds;
        readonly int pointCount;

        double thickness;
        int first = -1;
        int last = -1;
        int visibleCount;
        RawRectF contentBounds;

        public PenPencilRuns(byte[] pointLevels)
        {
            pointCount = pointLevels.Length;
            var count = 0;
            for (var i = 0; i < pointLevels.Length; i++)
            {
                if (i == 0 || pointLevels[i] != pointLevels[i - 1])
                    count++;
            }

            starts = new int[count];
            levels = new byte[count];
            var index = 0;
            for (var i = 0; i < pointLevels.Length; i++)
            {
                if (i != 0 && pointLevels[i] == pointLevels[i - 1])
                    continue;
                starts[index] = i;
                levels[index] = pointLevels[i];
                index++;
            }

            inks = new ID2D1Ink?[count];
            bounds = new RawRectF[count];
            builtStarts = new int[count];
            builtEnds = new int[count];
        }

        public void Draw(ID2D1DeviceContext6 dc, PenStrokeGeometry geometry, int start, int end, double thickness, InkBezierSegment[] segments, ID2D1InkStyle inkStyle, PencilBrushManager brushes, System.Windows.Media.Color color)
        {
            Update(dc, geometry, start, end, thickness, segments, inkStyle);
            if (visibleCount == 0)
                return;

            var padding = LayerPaddingPixels / GetScale(dc.Transform);
            var parameters = new LayerParameters1
            {
                ContentBounds = new RawRectF(contentBounds.Left - padding, contentBounds.Top - padding, contentBounds.Right + padding, contentBounds.Bottom + padding),
                MaskAntialiasMode = AntialiasMode.Aliased,
                MaskTransform = Matrix3x2.Identity,
                Opacity = 1f,
                LayerOptions = LayerOptions1.None,
            };
            dc.PushLayer(ref parameters, null);
            dc.PrimitiveBlend = PrimitiveBlend.Max;
            for (var run = first; run <= last; run++)
            {
                if (inks[run] is { } ink)
                    dc.DrawInk(ink, brushes.GetBrush(dc, color, levels[run]), inkStyle);
            }
            dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
            dc.PopLayer();
        }

        void Update(ID2D1DeviceContext6 dc, PenStrokeGeometry geometry, int start, int end, double thickness, InkBezierSegment[] segments, ID2D1InkStyle inkStyle)
        {
            if (this.thickness != thickness)
            {
                Release(0, inks.Length - 1);
                this.thickness = thickness;
            }

            var firstRun = FindRun(start);
            var lastRun = FindRun(end - 1);
            if (first >= 0)
            {
                Release(first, Math.Min(last, firstRun - 1));
                Release(Math.Max(first, lastRun + 1), last);
            }

            var scale = (float)thickness;
            visibleCount = 0;
            for (var run = firstRun; run <= lastRun; run++)
            {
                if (levels[run] == 0)
                    continue;

                var runStart = Math.Max(start, starts[run]);
                var runEnd = Math.Min(end, GetRunEnd(run));
                if (inks[run] is null || builtStarts[run] != runStart || builtEnds[run] != runEnd)
                {
                    inks[run]?.Dispose();
                    var ink = geometry.CreateInk(dc, runStart, runEnd, scale, segments);
                    inks[run] = ink;
                    bounds[run] = ink.GetBounds(inkStyle, null);
                    builtStarts[run] = runStart;
                    builtEnds[run] = runEnd;
                }

                contentBounds = visibleCount == 0 ? bounds[run] : Union(contentBounds, bounds[run]);
                visibleCount++;
            }

            first = firstRun;
            last = lastRun;
        }

        static float GetScale(in Matrix3x2 transform)
        {
            var scale = MathF.Sqrt(MathF.Abs(transform.GetDeterminant()));
            return scale > 0 ? scale : 1f;
        }

        int GetRunEnd(int run) => run + 1 < starts.Length ? starts[run + 1] + 1 : pointCount;

        int FindRun(int pointIndex)
        {
            var found = Array.BinarySearch(starts, pointIndex);
            return found >= 0 ? found : ~found - 1;
        }

        void Release(int fromRun, int toRun)
        {
            for (var run = fromRun; run <= toRun; run++)
            {
                inks[run]?.Dispose();
                inks[run] = null;
            }
        }

        static RawRectF Union(in RawRectF a, in RawRectF b) => new(
            Math.Min(a.Left, b.Left),
            Math.Min(a.Top, b.Top),
            Math.Max(a.Right, b.Right),
            Math.Max(a.Bottom, b.Bottom));

        public void Dispose()
        {
            Release(0, inks.Length - 1);
            first = -1;
            last = -1;
            visibleCount = 0;
        }
    }
}
