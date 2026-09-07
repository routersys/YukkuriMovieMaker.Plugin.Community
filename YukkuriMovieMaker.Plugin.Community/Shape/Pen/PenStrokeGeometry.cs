using Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    class PenStrokeGeometry : IDisposable
    {
        readonly InkPoint[] points;

        ID2D1Ink? ink;
        double inkThickness;
        int inkStart;
        int inkEnd;

        public SerializableStroke Stroke { get; }

        public int PointCount => points.Length;

        public int MaxSegmentCount => GetSegmentCount(points.Length);

        public PenStrokeGeometry(SerializableStroke stroke)
        {
            Stroke = stroke;

            var bezierPoints = stroke.ToStroke().GetBezierStylusPoints();
            var height = (float)stroke.DrawingAttributes.Height;
            points = new InkPoint[bezierPoints.Count];
            for (var i = 0; i < points.Length; i++)
            {
                var point = bezierPoints[i];
                points[i] = new InkPoint()
                {
                    X = (float)point.X,
                    Y = (float)point.Y,
                    Radius = height * point.PressureFactor,
                };
            }
        }

        public ID2D1Ink GetInk(ID2D1DeviceContext6 dc, int start, int end, double thickness, InkBezierSegment[] segments)
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
                for (var i = start + 1; i < end; i += 3)
                {
                    var remaining = end - i;
                    var point1 = GetScaledPoint(i, scale);
                    var point2 = remaining > 1 ? GetScaledPoint(i + 1, scale) : point1;
                    var point3 = remaining > 2 ? GetScaledPoint(i + 2, scale) : point2;
                    segments[segmentCount] = new InkBezierSegment()
                    {
                        Point1 = point1,
                        Point2 = point2,
                        Point3 = point3,
                    };
                    segmentCount++;
                }
            }
            newInk.AddSegments(segments, segmentCount);

            ink = newInk;
            inkThickness = thickness;
            inkStart = start;
            inkEnd = end;
            return newInk;
        }

        InkPoint GetScaledPoint(int index, float thickness)
        {
            var point = points[index];
            point.Radius = point.Radius * thickness / 100f;
            return point;
        }

        static int GetSegmentCount(int pointCount) => Math.Max(1, (pointCount + 1) / 3);

        public void Dispose()
        {
            ink?.Dispose();
            ink = null;
        }
    }
}
