using System.Collections.Immutable;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    class PenLayerRenderer(IGraphicsDevicesAndContext devices) : IDisposable
    {
        readonly List<IPenGeometry> geometries = [];

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
            AddGeometries(value, true, ref total, ref maxSegmentCount);
            AddGeometries(value, false, ref total, ref maxSegmentCount);
            TotalPointCount = total;
            if (segmentBuffer.Length < maxSegmentCount)
                segmentBuffer = new InkBezierSegment[maxSegmentCount];
            return true;
        }

        void AddGeometries(ImmutableList<SerializableStroke> value, bool isFill, ref int total, ref int maxSegmentCount)
        {
            foreach (var stroke in value)
            {
                if ((stroke.FillFigures is not null) != isFill)
                    continue;

                IPenGeometry geometry = isFill
                    ? new PenFillGeometry(stroke, devices.D2D.Factory)
                    : new PenStrokeGeometry(stroke);
                geometries.Add(geometry);
                total += geometry.PointCount;
                var segmentCount = geometry.MaxSegmentCount;
                if (segmentCount > maxSegmentCount)
                    maxSegmentCount = segmentCount;
            }
        }

        public void Draw(ID2D1DeviceContext6 dc, int pointFrom, int pointLength, double thickness, InkStyleResourceManager inkStyleResourceManager, SolidColorBrushManager solidColorBrushManager)
        {
            var currentPoint = 0;
            foreach (var geometry in geometries)
            {
                var currentLength = geometry.PointCount;
                var start = Math.Max(0, pointFrom - currentPoint);
                var end = Math.Min(currentLength, pointFrom + pointLength - currentPoint);
                currentPoint += currentLength;
                if (start >= end)
                    continue;

                geometry.Draw(dc, start, end, thickness, segmentBuffer, inkStyleResourceManager, solidColorBrushManager);
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
