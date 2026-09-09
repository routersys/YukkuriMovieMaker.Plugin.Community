using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    class PenStrokeGeometry : IPenGeometry
    {
        readonly SerializableStroke stroke;
        readonly InkPoint[] points;

        ID2D1Ink? ink;
        double inkThickness;
        int inkStart;
        int inkEnd;

        public int PointCount => points.Length;

        public int MaxSegmentCount => GetSegmentCount(points.Length);

        public PenStrokeGeometry(SerializableStroke stroke)
        {
            this.stroke = stroke;

            var source = stroke.StylusPoints;
            var height = (float)stroke.DrawingAttributes.Height;
            points = new InkPoint[source.Length];
            for (var i = 0; i < points.Length; i++)
            {
                var point = source[i];
                points[i] = new InkPoint()
                {
                    X = (float)point.X,
                    Y = (float)point.Y,
                    Radius = height * point.PressureFactor,
                };
            }
        }

        public void Draw(ID2D1DeviceContext6 dc, int start, int end, double thickness, InkBezierSegment[] segments, InkStyleResourceManager inkStyleResourceManager, SolidColorBrushManager solidColorBrushManager)
        {
            var currentInk = GetInk(dc, start, end, thickness, segments);
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

            dc.DrawInk(currentInk, brush, inkStyle);
            dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
        }

        ID2D1Ink GetInk(ID2D1DeviceContext6 dc, int start, int end, double thickness, InkBezierSegment[] segments)
        {
            if (ink is not null && inkStart == start && inkEnd == end && inkThickness == thickness)
                return ink;

            ink?.Dispose();

            var scale = (float)thickness;
            var startPoint = GetScaledPoint(start, scale);
            var newInk = dc.CreateInk(startPoint);

            var segmentCount = 0;
            if (end - start <= 1)
            {
                segments[0] = new InkBezierSegment()
                {
                    Point1 = startPoint,
                    Point2 = startPoint,
                    Point3 = startPoint,
                };
                segmentCount = 1;
            }
            else
            {
                var previous = startPoint;
                for (var i = start + 1; i < end; i++)
                {
                    var current = GetScaledPoint(i, scale);
                    segments[segmentCount] = new InkBezierSegment()
                    {
                        Point1 = Interpolate(previous, current, 1f / 3f),
                        Point2 = Interpolate(previous, current, 2f / 3f),
                        Point3 = current,
                    };
                    segmentCount++;
                    previous = current;
                }
            }
            newInk.AddSegments(segments, segmentCount);

            ink = newInk;
            inkThickness = thickness;
            inkStart = start;
            inkEnd = end;
            return newInk;
        }

        static InkPoint Interpolate(in InkPoint from, in InkPoint to, float rate) => new()
        {
            X = from.X + (to.X - from.X) * rate,
            Y = from.Y + (to.Y - from.Y) * rate,
            Radius = from.Radius + (to.Radius - from.Radius) * rate,
        };

        InkPoint GetScaledPoint(int index, float thickness)
        {
            var point = points[index];
            point.Radius = point.Radius * thickness / 100f;
            return point;
        }

        static int GetSegmentCount(int pointCount) => Math.Max(1, pointCount - 1);

        public void Dispose()
        {
            ink?.Dispose();
            ink = null;
        }
    }
}
