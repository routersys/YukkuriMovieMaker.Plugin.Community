using System.Collections.Immutable;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    class PenLayerRenderer : IDisposable
    {
        readonly List<PenStrokeGeometry> geometries = [];

        InkBezierSegment[] segmentBuffer = [];
        ImmutableList<SerializableStroke> strokes = [];

        public int TotalPointCount { get; private set; }

        public bool SetStrokes(ImmutableList<SerializableStroke> value)
        {
            if (strokes == value)
                return false;
            strokes = value;

            ClearGeometries();

            var total = 0;
            var maxSegmentCount = 0;
            foreach (var stroke in value)
            {
                var geometry = new PenStrokeGeometry(stroke);
                geometries.Add(geometry);
                total += geometry.PointCount;
                var segmentCount = geometry.MaxSegmentCount;
                if (segmentCount > maxSegmentCount)
                    maxSegmentCount = segmentCount;
            }
            TotalPointCount = total;
            if (segmentBuffer.Length < maxSegmentCount)
                segmentBuffer = new InkBezierSegment[maxSegmentCount];
            return true;
        }

        public void Draw(ID2D1DeviceContext6 dc, int pointFrom, int pointLength, double thickness, InkStyleResourceManager inkStyleResourceManager, SolidColorBrushManager solidColorBrushManager)
        {
            var currentPoint = 0;
            foreach (var geometry in geometries)
            {
                var stroke = geometry.Stroke;
                var currentStrokeLength = geometry.PointCount;
                var start = Math.Max(0, pointFrom - currentPoint);
                var end = Math.Min(currentStrokeLength, pointFrom + pointLength - currentPoint);
                currentPoint += currentStrokeLength;
                if (start >= end)
                    continue;

                var ink = geometry.GetInk(dc, start, end, thickness, segmentBuffer);
                var inkStyle = inkStyleResourceManager.GetInkStyle(dc, stroke.DrawingAttributes);

                Color4 color;
                if (stroke.DrawingAttributes.IsHighlighter)
                {
                    dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
                    var c = stroke.DrawingAttributes.Color;
                    color = new Color4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f / 2f);
                }
                else
                {
                    dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
                    color = stroke.DrawingAttributes.Color.ToColor4();
                }
                var brush = solidColorBrushManager.GetBrush(dc, color);

                dc.DrawInk(ink, brush, inkStyle);
                dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
            }
        }

        void ClearGeometries()
        {
            foreach (var geometry in geometries)
                geometry.Dispose();
            geometries.Clear();
        }

        public void Dispose()
        {
            ClearGeometries();
        }
    }
}
