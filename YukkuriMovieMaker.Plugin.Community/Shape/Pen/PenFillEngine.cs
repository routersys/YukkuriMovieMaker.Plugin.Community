using System.Windows;
using System.Windows.Media.Imaging;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    class PenFillEngine
    {
        const int DirectionRight = 0;
        const int DirectionDown = 1;
        const int DirectionLeft = 2;
        const int DirectionUp = 3;
        const int DirectionCount = 4;
        const int InitialCapacity = 1024;
        const double MinFigureArea = 1;
        const float NeutralPressure = 0.5f;

        static readonly double SimplifyTolerance = Math.Sqrt(2);

        byte[] region = [];
        byte[] outgoing = [];
        int[] stack = [];
        int[] ranges = [];
        int[] lengths = [];
        bool[] kept = [];
        Point[] loop = [];
        Point[] vertices = [];

        int width;
        int height;
        int pitch;
        int stackCount;
        int rangeCount;
        int loopCount;
        int vertexCount;
        int figureCount;

        public bool TryFill(WriteableBitmap image, Point seed, int difference, int expansion, out SerializableStylusPoint[] points, out int[] figures)
        {
            points = [];
            figures = [];

            var imageWidth = image.PixelWidth;
            var imageHeight = image.PixelHeight;
            var seedX = (int)Math.Floor(seed.X);
            var seedY = (int)Math.Floor(seed.Y);
            if (imageWidth <= 0 || imageHeight <= 0 || seedX < 0 || seedY < 0 || seedX >= imageWidth || seedY >= imageHeight)
                return false;

            EnsureBuffers(imageWidth, imageHeight);

            int left, top, right, bottom;
            image.Lock();
            try
            {
                if (!Flood(image, seedX, seedY, difference, out left, out top, out right, out bottom))
                    return false;
            }
            finally
            {
                image.Unlock();
            }

            Expand(expansion, ref left, ref top, ref right, ref bottom);
            Trace(left, top, right, bottom);
            if (figureCount == 0)
                return false;

            points = new SerializableStylusPoint[vertexCount];
            for (var i = 0; i < vertexCount; i++)
                points[i] = new SerializableStylusPoint(vertices[i].X, vertices[i].Y, NeutralPressure);
            figures = new int[figureCount];
            Array.Copy(lengths, figures, figureCount);
            return true;
        }

        void EnsureBuffers(int imageWidth, int imageHeight)
        {
            pitch = imageWidth + 1;
            if (width != imageWidth || height != imageHeight)
            {
                width = imageWidth;
                height = imageHeight;
                region = new byte[imageWidth * imageHeight];
                outgoing = new byte[pitch * (imageHeight + 1)];
                return;
            }

            Array.Clear(region);
            Array.Clear(outgoing);
        }

        unsafe bool Flood(WriteableBitmap image, int seedX, int seedY, int difference, out int left, out int top, out int right, out int bottom)
        {
            var pixels = (byte*)image.BackBuffer;
            var stride = (nint)image.BackBufferStride;
            var seed = *(uint*)(pixels + seedY * stride + seedX * 4);

            left = right = seedX;
            top = bottom = seedY;
            stackCount = 0;
            Push(seedX, seedY);

            var map = region;
            var isFilled = false;
            while (stackCount > 0)
            {
                var index = stack[--stackCount];
                var y = index / width;
                var x = index - y * width;
                var row = y * width;
                var pixelRow = pixels + y * stride;
                if (map[row + x] != 0 || !IsMatch(pixelRow, x, seed, difference))
                    continue;

                var spanLeft = x;
                while (spanLeft > 0 && map[row + spanLeft - 1] == 0 && IsMatch(pixelRow, spanLeft - 1, seed, difference))
                    spanLeft--;
                var spanRight = x;
                while (spanRight < width - 1 && map[row + spanRight + 1] == 0 && IsMatch(pixelRow, spanRight + 1, seed, difference))
                    spanRight++;

                Array.Fill(map, (byte)1, row + spanLeft, spanRight - spanLeft + 1);
                isFilled = true;

                if (spanLeft < left)
                    left = spanLeft;
                if (spanRight > right)
                    right = spanRight;
                if (y < top)
                    top = y;
                if (y > bottom)
                    bottom = y;

                if (y > 0)
                    ScanRow(pixelRow - stride, y - 1, spanLeft, spanRight, seed, difference);
                if (y < height - 1)
                    ScanRow(pixelRow + stride, y + 1, spanLeft, spanRight, seed, difference);
            }
            return isFilled;
        }

        unsafe void ScanRow(byte* pixelRow, int y, int spanLeft, int spanRight, uint seed, int difference)
        {
            var row = y * width;
            var map = region;
            var isInside = false;
            for (var x = spanLeft; x <= spanRight; x++)
            {
                if (map[row + x] != 0 || !IsMatch(pixelRow, x, seed, difference))
                {
                    isInside = false;
                    continue;
                }

                if (isInside)
                    continue;
                Push(x, y);
                isInside = true;
            }
        }

        static unsafe bool IsMatch(byte* pixelRow, int x, uint seed, int difference)
        {
            var value = *(uint*)(pixelRow + x * 4);
            if (value == seed)
                return true;

            for (var shift = 0; shift < 32; shift += 8)
            {
                var delta = (int)((value >> shift) & 0xFF) - (int)((seed >> shift) & 0xFF);
                if (delta > difference || -delta > difference)
                    return false;
            }
            return true;
        }

        void Push(int x, int y)
        {
            if (stackCount == stack.Length)
                Array.Resize(ref stack, stack.Length == 0 ? InitialCapacity : stack.Length * 2);
            stack[stackCount++] = y * width + x;
        }

        void Expand(int expansion, ref int left, ref int top, ref int right, ref int bottom)
        {
            for (var step = 1; step <= expansion; step++)
            {
                var stepLeft = Math.Max(0, left - 1);
                var stepTop = Math.Max(0, top - 1);
                var stepRight = Math.Min(width - 1, right + 1);
                var stepBottom = Math.Min(height - 1, bottom + 1);
                var value = (byte)(step + 1);
                var map = region;
                for (var y = stepTop; y <= stepBottom; y++)
                {
                    var row = y * width;
                    var above = y > 0 ? row - width : row;
                    var below = y < height - 1 ? row + width : row;
                    for (var x = stepLeft; x <= stepRight; x++)
                    {
                        if (map[row + x] != 0)
                            continue;

                        var from = x > 0 ? x - 1 : x;
                        var to = x < width - 1 ? x + 1 : x;
                        if (HasNeighbor(map, above, from, to, step) || HasNeighbor(map, row, from, to, step) || HasNeighbor(map, below, from, to, step))
                            map[row + x] = value;
                    }
                }
                left = stepLeft;
                top = stepTop;
                right = stepRight;
                bottom = stepBottom;
            }
        }

        static bool HasNeighbor(byte[] map, int row, int from, int to, int step)
        {
            for (var x = from; x <= to; x++)
            {
                var value = map[row + x];
                if (value != 0 && value <= step)
                    return true;
            }
            return false;
        }

        void Trace(int left, int top, int right, int bottom)
        {
            vertexCount = 0;
            figureCount = 0;

            var map = region;
            var edges = outgoing;
            for (var y = top; y <= bottom; y++)
            {
                var row = y * width;
                var above = row - width;
                var below = row + width;
                var hasAbove = y > 0;
                var hasBelow = y < height - 1;
                var vertexRow = y * pitch;
                var nextVertexRow = vertexRow + pitch;
                for (var x = left; x <= right; x++)
                {
                    if (map[row + x] == 0)
                        continue;

                    if (!hasAbove || map[above + x] == 0)
                        edges[vertexRow + x + 1] |= 1 << DirectionLeft;
                    if (!hasBelow || map[below + x] == 0)
                        edges[nextVertexRow + x] |= 1 << DirectionRight;
                    if (x == 0 || map[row + x - 1] == 0)
                        edges[vertexRow + x] |= 1 << DirectionDown;
                    if (x == width - 1 || map[row + x + 1] == 0)
                        edges[nextVertexRow + x + 1] |= 1 << DirectionUp;
                }
            }

            for (var y = top; y <= bottom + 1; y++)
            {
                for (var x = left; x <= right + 1; x++)
                {
                    while (outgoing[y * pitch + x] != 0)
                        Walk(x, y);
                }
            }
        }

        void Walk(int startX, int startY)
        {
            var x = startX;
            var y = startY;
            var direction = GetFirstDirection(outgoing[y * pitch + x]);
            loopCount = 0;
            while (direction >= 0)
            {
                outgoing[y * pitch + x] &= (byte)~(1 << direction);
                AppendLoop(x, y);
                x += GetDeltaX(direction);
                y += GetDeltaY(direction);
                if (x == startX && y == startY)
                    break;
                direction = GetNextDirection(outgoing[y * pitch + x], direction);
            }
            Emit();
        }

        static int GetFirstDirection(byte bits)
        {
            for (var direction = 0; direction < DirectionCount; direction++)
            {
                if ((bits & (1 << direction)) != 0)
                    return direction;
            }
            return -1;
        }

        static int GetNextDirection(byte bits, int incoming)
        {
            var left = (incoming + DirectionCount - 1) % DirectionCount;
            if ((bits & (1 << left)) != 0)
                return left;
            if ((bits & (1 << incoming)) != 0)
                return incoming;
            var right = (incoming + 1) % DirectionCount;
            return (bits & (1 << right)) != 0 ? right : -1;
        }

        static int GetDeltaX(int direction) => direction switch
        {
            DirectionRight => 1,
            DirectionLeft => -1,
            _ => 0,
        };

        static int GetDeltaY(int direction) => direction switch
        {
            DirectionDown => 1,
            DirectionUp => -1,
            _ => 0,
        };

        void AppendLoop(int x, int y)
        {
            if (loopCount == loop.Length)
                Array.Resize(ref loop, loop.Length == 0 ? InitialCapacity : loop.Length * 2);
            loop[loopCount++] = new Point(x, y);
        }

        void Emit()
        {
            if (loopCount < PenFillFigures.MinLength || Math.Abs(GetArea()) < MinFigureArea)
                return;

            var count = Simplify();
            if (count < PenFillFigures.MinLength)
                return;

            for (var i = 0; i < loopCount; i++)
            {
                if (kept[i])
                    AppendVertex(loop[i]);
            }
            AppendLength(count);
        }

        double GetArea()
        {
            var area = 0.0;
            var previous = loop[loopCount - 1];
            for (var i = 0; i < loopCount; i++)
            {
                var current = loop[i];
                area += previous.X * current.Y - current.X * previous.Y;
                previous = current;
            }
            return area / 2;
        }

        int Simplify()
        {
            if (kept.Length < loopCount)
                kept = new bool[loopCount];
            Array.Clear(kept, 0, loopCount);

            var farthest = GetFarthest();
            kept[0] = true;
            kept[farthest] = true;
            SimplifyRange(0, farthest);
            SimplifyRange(farthest, loopCount);

            var count = 0;
            for (var i = 0; i < loopCount; i++)
            {
                if (kept[i])
                    count++;
            }
            return count;
        }

        int GetFarthest()
        {
            var origin = loop[0];
            var farthest = 1;
            var distance = -1.0;
            for (var i = 1; i < loopCount; i++)
            {
                var dx = loop[i].X - origin.X;
                var dy = loop[i].Y - origin.Y;
                var current = dx * dx + dy * dy;
                if (current <= distance)
                    continue;
                distance = current;
                farthest = i;
            }
            return farthest;
        }

        void SimplifyRange(int first, int last)
        {
            rangeCount = 0;
            PushRange(first, last);
            while (rangeCount > 0)
            {
                var end = ranges[--rangeCount];
                var start = ranges[--rangeCount];
                if (end - start < 2)
                    continue;

                var from = GetLoopPoint(start);
                var to = GetLoopPoint(end);
                var farthest = -1;
                var distance = SimplifyTolerance;
                for (var i = start + 1; i < end; i++)
                {
                    var current = GetDistance(GetLoopPoint(i), from, to);
                    if (current <= distance)
                        continue;
                    distance = current;
                    farthest = i;
                }
                if (farthest < 0)
                    continue;

                kept[farthest] = true;
                PushRange(start, farthest);
                PushRange(farthest, end);
            }
        }

        Point GetLoopPoint(int index) => loop[index == loopCount ? 0 : index];

        void PushRange(int start, int end)
        {
            if (rangeCount + 2 > ranges.Length)
                Array.Resize(ref ranges, ranges.Length == 0 ? InitialCapacity : ranges.Length * 2);
            ranges[rangeCount++] = start;
            ranges[rangeCount++] = end;
        }

        static double GetDistance(Point point, Point from, Point to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var lengthSquared = dx * dx + dy * dy;
            var px = point.X - from.X;
            var py = point.Y - from.Y;
            if (lengthSquared <= 0)
                return Math.Sqrt(px * px + py * py);

            var rate = Math.Clamp((px * dx + py * dy) / lengthSquared, 0, 1);
            var offsetX = px - dx * rate;
            var offsetY = py - dy * rate;
            return Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
        }

        void AppendVertex(Point point)
        {
            if (vertexCount == vertices.Length)
                Array.Resize(ref vertices, vertices.Length == 0 ? InitialCapacity : vertices.Length * 2);
            vertices[vertexCount++] = point;
        }

        void AppendLength(int length)
        {
            if (figureCount == lengths.Length)
                Array.Resize(ref lengths, lengths.Length == 0 ? InitialCapacity : lengths.Length * 2);
            lengths[figureCount++] = length;
        }
    }
}
