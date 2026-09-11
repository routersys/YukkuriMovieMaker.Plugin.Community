using System.Collections.Immutable;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
{
    class PenLayerRenderer(IGraphicsDevicesAndContext devices) : IDisposable
    {
        readonly List<PenLayerElement> elements = [];

        InkBezierSegment[] segmentBuffer = [];
        ImmutableList<SerializableStroke> strokes = [];
        bool isLegacy;

        public int TotalPointCount { get; private set; }

        public int ElementCount => elements.Count;

        public bool SetStrokes(ImmutableList<SerializableStroke> value, bool isLegacy)
        {
            if (strokes == value && this.isLegacy == isLegacy)
                return false;
            strokes = value;
            this.isLegacy = isLegacy;

            ClearElements();

            var pointFrom = 0;
            var maxSegmentCount = 0;
            foreach (var stroke in value)
            {
                var isFill = stroke.FillFigures is not null;
                IPenGeometry geometry = isFill
                    ? new PenFillGeometry(stroke, devices.D2D.Factory)
                    : new PenStrokeGeometry(stroke, isLegacy);
                elements.Add(new PenLayerElement(geometry, pointFrom, isFill));
                pointFrom += geometry.PointCount;
                var segmentCount = geometry.MaxSegmentCount;
                if (segmentCount > maxSegmentCount)
                    maxSegmentCount = segmentCount;
            }
            TotalPointCount = pointFrom;
            if (segmentBuffer.Length < maxSegmentCount)
                segmentBuffer = new InkBezierSegment[maxSegmentCount];
            return true;
        }

        public void Draw(ID2D1DeviceContext6 dc, int pointFrom, int pointLength, double thickness, PenDrawResources resources)
        {
            DrawElements(dc, true, pointFrom, pointLength, thickness, resources);
            DrawElements(dc, false, pointFrom, pointLength, thickness, resources);
        }

        void DrawElements(ID2D1DeviceContext6 dc, bool isFill, int pointFrom, int pointLength, double thickness, PenDrawResources resources)
        {
            foreach (var element in elements)
            {
                if (element.IsFill != isFill || !TryGetDrawRange(element, pointFrom, pointLength, out var start, out var end))
                    continue;

                element.Geometry.Draw(dc, start, end, thickness, segmentBuffer, resources);
            }
        }

        public bool IsDrawn(int index, int pointFrom, int pointLength)
            => TryGetDrawRange(elements[index], pointFrom, pointLength, out _, out _);

        static bool TryGetDrawRange(in PenLayerElement element, int pointFrom, int pointLength, out int start, out int end)
        {
            start = Math.Max(0, pointFrom - element.PointFrom);
            end = Math.Min(element.Geometry.PointCount, pointFrom + pointLength - element.PointFrom);
            return start < end;
        }

        void ClearElements()
        {
            foreach (var element in elements)
                element.Geometry.Dispose();
            elements.Clear();
        }

        public void Dispose()
        {
            ClearElements();
        }
    }
}
