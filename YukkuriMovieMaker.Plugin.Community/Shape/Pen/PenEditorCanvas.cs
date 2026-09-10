using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal sealed class PenEditorCanvas : FrameworkElement
    {
        const double MinZoom = 0.05;
        const double MaxZoom = 32.0;
        const double ZoomStep = 1.2;
        const double CheckerCellSize = 8.0;
        const double PanThreshold = 3.0;
        const float NeutralPressure = 0.5f;
        const double SelectionGrabMargin = 4.0;
        const double HandleSize = 8.0;
        const double RotateHandleDistance = 22.0;
        const double MinSelectionScale = 0.01;
        const double RotationSnapAngle = 15.0;
        const double MaxStabilizationStrength = 0.95;
        const double StabilizationSettleDistance = 0.5;

        static readonly System.Windows.Media.Brush BackgroundBrush = CreateFrozenBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        static readonly System.Windows.Media.Brush CheckerBrush = CreateCheckerBrush();
        static readonly System.Windows.Media.Pen BorderPen = CreateFrozenPen(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF), 1.0);
        static readonly System.Windows.Media.Pen SelectionPen = CreateFrozenDashedPen(Color.FromArgb(0xFF, 0x2E, 0x86, 0xFF), 1.0);
        static readonly System.Windows.Media.Pen LassoPen = CreateFrozenDashedPen(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 1.0);
        static readonly System.Windows.Media.Brush HandleBrush = CreateFrozenBrush(Color.FromArgb(0xFF, 0x2E, 0x86, 0xFF));
        static readonly System.Windows.Media.Pen HandlePen = CreateFrozenPen(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 1.0);
        static readonly System.Windows.Media.Pen BrushSizePen = CreateFrozenPen(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF), 1.0);

        public static readonly DependencyProperty ImageProperty =
            DependencyProperty.Register(nameof(Image), typeof(ImageSource), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DocumentImageProperty =
            DependencyProperty.Register(nameof(DocumentImage), typeof(ImageSource), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CanvasWidthProperty =
            DependencyProperty.Register(nameof(CanvasWidth), typeof(double), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(1920d, FrameworkPropertyMetadataOptions.AffectsRender, OnCanvasSizeChanged));

        public static readonly DependencyProperty CanvasHeightProperty =
            DependencyProperty.Register(nameof(CanvasHeight), typeof(double), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(1080d, FrameworkPropertyMetadataOptions.AffectsRender, OnCanvasSizeChanged));

        public static readonly DependencyProperty WetInkColorProperty =
            DependencyProperty.Register(nameof(WetInkColor), typeof(Color), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(Colors.White, OnWetInkColorChanged));

        public static readonly DependencyProperty WetInkThicknessProperty =
            DependencyProperty.Register(nameof(WetInkThickness), typeof(double), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(10d, OnBrushSizeChanged));

        public static readonly DependencyProperty IsEditableProperty =
            DependencyProperty.Register(nameof(IsEditable), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(true, OnIsEditableChanged));

        public static readonly DependencyProperty IsRectangleSelectionProperty =
            DependencyProperty.Register(nameof(IsRectangleSelection), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty IsFillModeProperty =
            DependencyProperty.Register(nameof(IsFillMode), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(false, OnBrushSizeChanged));

        public static readonly DependencyProperty IsSelectionModeProperty =
            DependencyProperty.Register(nameof(IsSelectionMode), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnBrushSizeChanged));

        public static readonly DependencyProperty SelectionBoundsProperty =
            DependencyProperty.Register(nameof(SelectionBounds), typeof(Rect), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(Rect.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IgnoresPressureProperty =
            DependencyProperty.Register(nameof(IgnoresPressure), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty TaperLengthProperty =
            DependencyProperty.Register(nameof(TaperLength), typeof(double), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(0d, null, CoerceTaperLength));

        public static readonly DependencyProperty StabilizationStrengthProperty =
            DependencyProperty.Register(nameof(StabilizationStrength), typeof(double), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(0d, null, CoerceStabilizationStrength));

        public static readonly DependencyProperty WetInkUsesPressureProperty =
            DependencyProperty.Register(nameof(WetInkUsesPressure), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(true));

        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceZoom));

        public ImageSource? Image
        {
            get => (ImageSource?)GetValue(ImageProperty);
            set => SetValue(ImageProperty, value);
        }

        public ImageSource? DocumentImage
        {
            get => (ImageSource?)GetValue(DocumentImageProperty);
            set => SetValue(DocumentImageProperty, value);
        }

        public double CanvasWidth
        {
            get => (double)GetValue(CanvasWidthProperty);
            set => SetValue(CanvasWidthProperty, value);
        }

        public double CanvasHeight
        {
            get => (double)GetValue(CanvasHeightProperty);
            set => SetValue(CanvasHeightProperty, value);
        }

        public double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        public Color WetInkColor
        {
            get => (Color)GetValue(WetInkColorProperty);
            set => SetValue(WetInkColorProperty, value);
        }

        public double WetInkThickness
        {
            get => (double)GetValue(WetInkThicknessProperty);
            set => SetValue(WetInkThicknessProperty, value);
        }

        public bool WetInkUsesPressure
        {
            get => (bool)GetValue(WetInkUsesPressureProperty);
            set => SetValue(WetInkUsesPressureProperty, value);
        }

        public bool IgnoresPressure
        {
            get => (bool)GetValue(IgnoresPressureProperty);
            set => SetValue(IgnoresPressureProperty, value);
        }

        public double TaperLength
        {
            get => (double)GetValue(TaperLengthProperty);
            set => SetValue(TaperLengthProperty, value);
        }

        public double StabilizationStrength
        {
            get => (double)GetValue(StabilizationStrengthProperty);
            set => SetValue(StabilizationStrengthProperty, value);
        }

        public bool IsEditable
        {
            get => (bool)GetValue(IsEditableProperty);
            set => SetValue(IsEditableProperty, value);
        }

        public bool IsRectangleSelection
        {
            get => (bool)GetValue(IsRectangleSelectionProperty);
            set => SetValue(IsRectangleSelectionProperty, value);
        }

        public bool IsFillMode
        {
            get => (bool)GetValue(IsFillModeProperty);
            set => SetValue(IsFillModeProperty, value);
        }

        public bool IsSelectionMode
        {
            get => (bool)GetValue(IsSelectionModeProperty);
            set => SetValue(IsSelectionModeProperty, value);
        }

        public Rect SelectionBounds
        {
            get => (Rect)GetValue(SelectionBoundsProperty);
            set => SetValue(SelectionBoundsProperty, value);
        }

        public event EventHandler<PenLassoCompletedEventArgs>? LassoCompleted;

        public event EventHandler? SelectionTransformStarted;

        public event EventHandler<PenSelectionTransformedEventArgs>? SelectionTransformed;

        public event EventHandler? SelectionTransformCompleted;

        public event EventHandler<PenStrokeCompletedEventArgs>? StrokeCompleted;

        public event EventHandler<PenFillRequestedEventArgs>? FillRequested;

        readonly DrawingVisual wetInkVisual = new();
        readonly DrawingGroup wetInkDrawing = new();
        readonly MatrixTransform wetInkTransform = new();
        readonly DrawingVisual brushSizeVisual = new();
        readonly EllipseGeometry brushSizeGeometry = new();
        readonly TranslateTransform brushSizeTransform = new();

        System.Windows.Media.Brush wetInkBrush = System.Windows.Media.Brushes.White;
        System.Windows.Media.Pen? wetInkPen;
        double wetInkPenThickness;
        StylusPointCollection? strokePoints;
        PenInputSource inputSource;
        double[] taperDistances = [];
        Point rawPoint;
        List<Point>? lassoPoints;
        bool isMovingSelection;
        Point transformStart;
        Point transformPoint;
        Rect transformBounds;
        PenSelectionHandle activeHandle;

        Point brushSizePoint;
        bool isPointerInside;

        Point origin;
        bool isPanning;
        bool isPanMoved;
        Point panStart;

        public PenEditorCanvas()
        {
            Focusable = true;
            ClipToBounds = true;

            Cursor = Cursors.Cross;
            wetInkVisual.Transform = wetInkTransform;
            using (var context = wetInkVisual.RenderOpen())
                context.DrawDrawing(wetInkDrawing);
            AddVisualChild(wetInkVisual);
            brushSizeVisual.Transform = brushSizeTransform;
            brushSizeVisual.Opacity = 0;
            using (var context = brushSizeVisual.RenderOpen())
                context.DrawGeometry(null, BrushSizePen, brushSizeGeometry);
            AddVisualChild(brushSizeVisual);
            UpdateWetInkBrush();
        }

        protected override int VisualChildrenCount => 2;

        protected override Visual GetVisualChild(int index) => index switch
        {
            0 => wetInkVisual,
            1 => brushSizeVisual,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        public bool IsStrokeInProgress => strokePoints is not null || lassoPoints is not null || isMovingSelection;

        public void BeginStroke(Point canvasPoint, float pressure)
        {
            if (!IsEditable || IsStrokeInProgress)
                return;

            if (IsFillMode)
            {
                FillRequested?.Invoke(this, new PenFillRequestedEventArgs(canvasPoint));
                return;
            }

            if (IsSelectionMode)
            {
                BeginSelection(canvasPoint);
                return;
            }

            wetInkDrawing.Children.Clear();
            strokePoints = [new StylusPoint(canvasPoint.X, canvasPoint.Y, GetPressure(pressure))];
            rawPoint = canvasPoint;
        }

        public void AddStrokePoint(Point canvasPoint, float pressure)
        {
            if (lassoPoints is not null || isMovingSelection)
            {
                AddSelectionPoint(canvasPoint);
                return;
            }

            if (strokePoints is null)
                return;

            rawPoint = canvasPoint;
            var previous = strokePoints[^1];
            var point = Stabilize(previous, canvasPoint, GetPressure(pressure));
            if (previous.X == point.X && previous.Y == point.Y)
                return;

            strokePoints.Add(point);
            AppendWetInk();
        }

        public void EndStroke()
        {
            inputSource = PenInputSource.None;

            if (lassoPoints is not null || isMovingSelection)
            {
                EndSelection();
                return;
            }

            Settle();
            ApplyTaper();

            var points = strokePoints;
            strokePoints = null;
            wetInkDrawing.Children.Clear();
            if (points is not null && points.Count > 0)
                StrokeCompleted?.Invoke(this, new PenStrokeCompletedEventArgs(points));
        }

        void BeginSelection(Point canvasPoint)
        {
            var bounds = SelectionBounds;
            var handle = bounds.IsEmpty ? PenSelectionHandle.None : HitTestHandle(canvasPoint, bounds);
            if (handle is not PenSelectionHandle.None)
            {
                isMovingSelection = true;
                activeHandle = handle;
                transformStart = canvasPoint;
                transformPoint = canvasPoint;
                transformBounds = bounds;
                SelectionTransformStarted?.Invoke(this, EventArgs.Empty);
                return;
            }

            lassoPoints = [canvasPoint];
            InvalidateVisual();
        }

        void AddSelectionPoint(Point canvasPoint)
        {
            if (isMovingSelection)
            {
                if (canvasPoint == transformPoint)
                    return;

                transformPoint = canvasPoint;
                SelectionTransformed?.Invoke(this, new PenSelectionTransformedEventArgs(CreateTransform(canvasPoint)));
                return;
            }

            if (lassoPoints is null)
                return;

            if (IsRectangleSelection)
            {
                if (lassoPoints.Count < 2)
                    lassoPoints.Add(canvasPoint);
                else
                    lassoPoints[1] = canvasPoint;
                InvalidateVisual();
                return;
            }

            if (lassoPoints[^1] == canvasPoint)
                return;

            lassoPoints.Add(canvasPoint);
            InvalidateVisual();
        }

        void EndSelection()
        {
            if (isMovingSelection)
            {
                isMovingSelection = false;
                activeHandle = PenSelectionHandle.None;
                SelectionTransformCompleted?.Invoke(this, EventArgs.Empty);
                return;
            }

            var points = lassoPoints;
            lassoPoints = null;
            InvalidateVisual();
            if (points is null)
                return;

            if (IsRectangleSelection)
                points = CreateRectanglePolygon(points);
            LassoCompleted?.Invoke(this, new PenLassoCompletedEventArgs(points));
        }

        PenSelectionHandle HitTestHandle(Point canvasPoint, Rect bounds)
        {
            var half = HandleSize / 2 / Zoom;
            var centerX = bounds.X + bounds.Width / 2;
            var centerY = bounds.Y + bounds.Height / 2;

            if (Math.Abs(canvasPoint.X - centerX) <= half && Math.Abs(canvasPoint.Y - (bounds.Y - RotateHandleDistance / Zoom)) <= half)
                return PenSelectionHandle.Rotate;

            var zoom = Zoom;
            var canResizeX = bounds.Width * zoom > HandleSize;
            var canResizeY = bounds.Height * zoom > HandleSize;
            var left = canResizeX && Math.Abs(canvasPoint.X - bounds.X) <= half;
            var right = canResizeX && Math.Abs(canvasPoint.X - bounds.Right) <= half;
            var top = canResizeY && Math.Abs(canvasPoint.Y - bounds.Y) <= half;
            var bottom = canResizeY && Math.Abs(canvasPoint.Y - bounds.Bottom) <= half;
            var middleX = Math.Abs(canvasPoint.X - centerX) <= half;
            var middleY = Math.Abs(canvasPoint.Y - centerY) <= half;

            if (left && top)
                return PenSelectionHandle.TopLeft;
            if (right && top)
                return PenSelectionHandle.TopRight;
            if (left && bottom)
                return PenSelectionHandle.BottomLeft;
            if (right && bottom)
                return PenSelectionHandle.BottomRight;
            if (top && middleX)
                return PenSelectionHandle.Top;
            if (bottom && middleX)
                return PenSelectionHandle.Bottom;
            if (left && middleY)
                return PenSelectionHandle.Left;
            if (right && middleY)
                return PenSelectionHandle.Right;

            var margin = SelectionGrabMargin / Zoom;
            var inflated = bounds;
            inflated.Inflate(margin, margin);
            return inflated.Contains(canvasPoint) ? PenSelectionHandle.Move : PenSelectionHandle.None;
        }

        Matrix CreateTransform(Point canvasPoint)
        {
            var matrix = Matrix.Identity;
            if (activeHandle is PenSelectionHandle.Move)
            {
                matrix.Translate(canvasPoint.X - transformStart.X, canvasPoint.Y - transformStart.Y);
                return matrix;
            }

            var bounds = transformBounds;
            var centerX = bounds.X + bounds.Width / 2;
            var centerY = bounds.Y + bounds.Height / 2;

            var isConstrained = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            if (activeHandle is PenSelectionHandle.Rotate)
            {
                var from = Math.Atan2(transformStart.Y - centerY, transformStart.X - centerX);
                var to = Math.Atan2(canvasPoint.Y - centerY, canvasPoint.X - centerX);
                var angle = (to - from) * 180 / Math.PI;
                if (isConstrained)
                    angle = Math.Round(angle / RotationSnapAngle) * RotationSnapAngle;
                matrix.RotateAt(angle, centerX, centerY);
                return matrix;
            }

            GetScaleAnchor(bounds, out var anchor, out var origin);
            var scaleX = GetScale(canvasPoint.X - anchor.X, origin.X - anchor.X);
            var scaleY = GetScale(canvasPoint.Y - anchor.Y, origin.Y - anchor.Y);
            if (isConstrained && IsCornerHandle(activeHandle))
            {
                var magnitude = Math.Max(Math.Abs(scaleX), Math.Abs(scaleY));
                scaleX = scaleX < 0 ? -magnitude : magnitude;
                scaleY = scaleY < 0 ? -magnitude : magnitude;
            }
            matrix.ScaleAt(scaleX, scaleY, anchor.X, anchor.Y);
            return matrix;
        }

        void GetScaleAnchor(Rect bounds, out Point anchor, out Point origin)
        {
            var centerX = bounds.X + bounds.Width / 2;
            var centerY = bounds.Y + bounds.Height / 2;
            switch (activeHandle)
            {
                case PenSelectionHandle.TopLeft:
                    anchor = new Point(bounds.Right, bounds.Bottom);
                    origin = new Point(bounds.X, bounds.Y);
                    return;
                case PenSelectionHandle.TopRight:
                    anchor = new Point(bounds.X, bounds.Bottom);
                    origin = new Point(bounds.Right, bounds.Y);
                    return;
                case PenSelectionHandle.BottomLeft:
                    anchor = new Point(bounds.Right, bounds.Y);
                    origin = new Point(bounds.X, bounds.Bottom);
                    return;
                case PenSelectionHandle.BottomRight:
                    anchor = new Point(bounds.X, bounds.Y);
                    origin = new Point(bounds.Right, bounds.Bottom);
                    return;
                case PenSelectionHandle.Top:
                    anchor = new Point(centerX, bounds.Bottom);
                    origin = new Point(centerX, bounds.Y);
                    return;
                case PenSelectionHandle.Bottom:
                    anchor = new Point(centerX, bounds.Y);
                    origin = new Point(centerX, bounds.Bottom);
                    return;
                case PenSelectionHandle.Left:
                    anchor = new Point(bounds.Right, centerY);
                    origin = new Point(bounds.X, centerY);
                    return;
                default:
                    anchor = new Point(bounds.X, centerY);
                    origin = new Point(bounds.Right, centerY);
                    return;
            }
        }

        static bool IsCornerHandle(PenSelectionHandle handle)
            => handle is PenSelectionHandle.TopLeft or PenSelectionHandle.TopRight
                or PenSelectionHandle.BottomLeft or PenSelectionHandle.BottomRight;

        static double GetScale(double moved, double original)
        {
            if (original == 0)
                return 1;

            var scale = moved / original;
            if (scale >= MinSelectionScale || scale <= -MinSelectionScale)
                return scale;
            return scale < 0 ? -MinSelectionScale : MinSelectionScale;
        }

        static List<Point> CreateRectanglePolygon(List<Point> points)
        {
            if (points.Count < 2)
                return points;

            var start = points[0];
            var end = points[1];
            return [start, new Point(end.X, start.Y), end, new Point(start.X, end.Y)];
        }

        public void ResetView()
        {
            var size = RenderSize;
            if (size.Width <= 0 || size.Height <= 0 || CanvasWidth <= 0 || CanvasHeight <= 0)
                return;

            Zoom = Math.Min(size.Width / CanvasWidth, size.Height / CanvasHeight);
            origin = new Point(
                (size.Width - CanvasWidth * Zoom) / 2,
                (size.Height - CanvasHeight * Zoom) / 2);
            UpdateWetInkTransform();
            UpdateBrushSize();
            InvalidateVisual();
        }

        public Point ScreenToCanvas(Point point)
        {
            var zoom = Zoom;
            return new Point((point.X - origin.X) / zoom, (point.Y - origin.Y) / zoom);
        }

        public Point CanvasToScreen(Point point)
        {
            var zoom = Zoom;
            return new Point(point.X * zoom + origin.X, point.Y * zoom + origin.Y);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            ResetView();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var size = RenderSize;
            drawingContext.DrawRectangle(BackgroundBrush, null, new Rect(size));

            var rect = GetCanvasRect();
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            drawingContext.DrawRectangle(CheckerBrush, null, rect);
            var image = Image;
            if (image is not null)
                drawingContext.DrawImage(image, rect);
            var documentImage = DocumentImage;
            if (documentImage is not null)
                drawingContext.DrawImage(documentImage, rect);
            drawingContext.DrawRectangle(null, BorderPen, rect);
            DrawSelection(drawingContext);
        }

        protected override void OnStylusDown(StylusDownEventArgs e)
        {
            base.OnStylusDown(e);
            if (IsStrokeInProgress)
                return;

            Focus();
            var points = e.GetStylusPoints(this);
            if (points.Count > 0)
                BeginStroke(ScreenToCanvas(new Point(points[0].X, points[0].Y)), points[0].PressureFactor);
            if (IsFillMode)
            {
                e.Handled = true;
                return;
            }
            if (!IsStrokeInProgress)
                return;
            if (!CaptureStylus())
            {
                EndStroke();
                return;
            }

            inputSource = PenInputSource.Stylus;
            AddStylusPoints(points, 1);
            e.Handled = true;
        }

        protected override void OnStylusMove(StylusEventArgs e)
        {
            base.OnStylusMove(e);
            if (inputSource is not PenInputSource.Stylus || !IsStrokeInProgress)
                return;
            AddStylusPoints(e.GetStylusPoints(this), 0);
            e.Handled = true;
        }

        protected override void OnStylusUp(StylusEventArgs e)
        {
            base.OnStylusUp(e);
            if (inputSource is not PenInputSource.Stylus)
                return;

            if (IsStrokeInProgress)
            {
                AddStylusPoints(e.GetStylusPoints(this), 0);
                EndStroke();
            }
            ReleaseStylusCapture();
            e.Handled = true;
        }

        protected override void OnLostStylusCapture(StylusEventArgs e)
        {
            base.OnLostStylusCapture(e);
            EndStroke();
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            isPanning = false;
            EndStroke();
        }

        void AddStylusPoints(StylusPointCollection points, int startIndex)
        {
            for (var i = startIndex; i < points.Count; i++)
            {
                var point = points[i];
                AddStrokePoint(ScreenToCanvas(new Point(point.X, point.Y)), point.PressureFactor);
            }

            if (points.Count == 0)
                return;

            var last = points[points.Count - 1];
            brushSizePoint = ScreenToCanvas(new Point(last.X, last.Y));
            isPointerInside = true;
            UpdateBrushSize();
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.Delta == 0)
                return;

            var position = e.GetPosition(this);
            var canvasPosition = ScreenToCanvas(position);
            Zoom = e.Delta > 0 ? Zoom * ZoomStep : Zoom / ZoomStep;
            origin = new Point(
                position.X - canvasPosition.X * Zoom,
                position.Y - canvasPosition.Y * Zoom);
            UpdateWetInkTransform();
            UpdateBrushSize();
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.ChangedButton is MouseButton.Middle && !IsStrokeInProgress)
            {
                isPanning = true;
                isPanMoved = false;
                panStart = e.GetPosition(this);
                CaptureMouse();
                e.Handled = true;
                return;
            }

            if (e.ChangedButton is not MouseButton.Left || IsStrokeInProgress)
                return;

            Focus();
            BeginStroke(ScreenToCanvas(e.GetPosition(this)), GetInputPressure(e));
            if (IsFillMode)
            {
                e.Handled = true;
                return;
            }
            if (!IsStrokeInProgress)
                return;
            if (!CaptureMouse())
            {
                EndStroke();
                return;
            }

            inputSource = PenInputSource.Mouse;
            e.Handled = true;
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            brushSizePoint = ScreenToCanvas(e.GetPosition(this));
            isPointerInside = true;
            UpdateBrushSize();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            isPointerInside = false;
            UpdateBrushSize();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            brushSizePoint = ScreenToCanvas(e.GetPosition(this));
            isPointerInside = true;
            UpdateBrushSize();
            if (isPanning)
            {
                var panPosition = e.GetPosition(this);
                var delta = panPosition - panStart;
                if (!isPanMoved && Math.Abs(delta.X) < PanThreshold && Math.Abs(delta.Y) < PanThreshold)
                    return;

                isPanMoved = true;
                origin = new Point(origin.X + delta.X, origin.Y + delta.Y);
                panStart = panPosition;
                UpdateWetInkTransform();
                InvalidateVisual();
                return;
            }

            var canvasPoint = ScreenToCanvas(e.GetPosition(this));
            if (inputSource is not PenInputSource.Mouse || !IsStrokeInProgress)
            {
                UpdateCursor(canvasPoint);
                return;
            }

            AddStrokePoint(canvasPoint, GetInputPressure(e));
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.ChangedButton is MouseButton.Middle && isPanning)
            {
                isPanning = false;
                ReleaseMouseCapture();
                e.Handled = true;
                return;
            }

            if (e.ChangedButton is not MouseButton.Left || inputSource is not PenInputSource.Mouse || !IsStrokeInProgress)
                return;

            AddStrokePoint(ScreenToCanvas(e.GetPosition(this)), GetInputPressure(e));
            EndStroke();
            ReleaseMouseCapture();
            e.Handled = true;
        }

        float GetPressure(float pressure) => IgnoresPressure ? NeutralPressure : pressure;

        float GetInputPressure(MouseEventArgs e)
        {
            var device = e.StylusDevice;
            if (device is null)
                return NeutralPressure;

            var points = device.GetStylusPoints(this);
            return points.Count > 0 ? points[^1].PressureFactor : NeutralPressure;
        }

        void ApplyTaper()
        {
            var taper = TaperLength;
            var points = strokePoints;
            if (points is null || taper <= 0 || points.Count < 2)
                return;

            var count = points.Count;
            if (taperDistances.Length < count)
                taperDistances = new double[count];

            taperDistances[0] = 0;
            for (var i = 1; i < count; i++)
            {
                var dx = points[i].X - points[i - 1].X;
                var dy = points[i].Y - points[i - 1].Y;
                taperDistances[i] = taperDistances[i - 1] + Math.Sqrt(dx * dx + dy * dy);
            }

            var total = taperDistances[count - 1];
            if (total <= 0)
                return;

            var limit = Math.Min(taper, total / 2);
            for (var i = 0; i < count; i++)
            {
                var head = taperDistances[i];
                var rate = Math.Min(1, Math.Min(head, total - head) / limit);
                var point = points[i];
                points[i] = new StylusPoint(point.X, point.Y, (float)(point.PressureFactor * rate));
            }
        }

        void Settle()
        {
            if (strokePoints is null || StabilizationStrength <= 0)
                return;

            while (true)
            {
                var previous = strokePoints[^1];
                var dx = rawPoint.X - previous.X;
                var dy = rawPoint.Y - previous.Y;
                if (dx * dx + dy * dy <= StabilizationSettleDistance * StabilizationSettleDistance)
                    return;

                var point = Stabilize(previous, rawPoint, previous.PressureFactor);
                if (previous.X == point.X && previous.Y == point.Y)
                    return;

                strokePoints.Add(point);
            }
        }

        StylusPoint Stabilize(StylusPoint previous, Point canvasPoint, float pressure)
        {
            var strength = StabilizationStrength;
            if (strength <= 0)
                return new StylusPoint(canvasPoint.X, canvasPoint.Y, pressure);

            var rate = 1 - strength;
            return new StylusPoint(
                previous.X + (canvasPoint.X - previous.X) * rate,
                previous.Y + (canvasPoint.Y - previous.Y) * rate,
                pressure);
        }

        void AppendWetInk()
        {
            if (strokePoints is null)
                return;

            var count = strokePoints.Count;
            if (count < 2)
                return;

            var previous = strokePoints[count - 2];
            var current = strokePoints[count - 1];
            var thickness = WetInkUsesPressure
                ? WetInkThickness * (previous.PressureFactor + current.PressureFactor)
                : WetInkThickness;

            if (count == 2)
            {
                AppendWetInkFigure(ToPoint(previous), GetMidpoint(previous, current), null, thickness);
                return;
            }

            var beforePrevious = strokePoints[count - 3];
            AppendWetInkFigure(
                GetMidpoint(beforePrevious, previous),
                GetMidpoint(previous, current),
                ToPoint(previous),
                thickness);
        }

        void AppendWetInkFigure(Point start, Point end, Point? control, double thickness)
        {
            var pen = GetWetInkPen(thickness);
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(start, false, false);
                if (control is null)
                    context.LineTo(end, true, false);
                else
                    context.QuadraticBezierTo(control.Value, end, true, false);
            }
            geometry.Freeze();

            var drawing = new GeometryDrawing(null, pen, geometry);
            drawing.Freeze();
            wetInkDrawing.Children.Add(drawing);
        }

        System.Windows.Media.Pen GetWetInkPen(double thickness)
        {
            if (wetInkPen is not null && wetInkPenThickness == thickness)
                return wetInkPen;

            var pen = new System.Windows.Media.Pen(wetInkBrush, thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };
            pen.Freeze();
            wetInkPen = pen;
            wetInkPenThickness = thickness;
            return pen;
        }

        static Point ToPoint(StylusPoint point) => new(point.X, point.Y);

        static Point GetMidpoint(StylusPoint from, StylusPoint to)
            => new((from.X + to.X) / 2, (from.Y + to.Y) / 2);

        void UpdateWetInkBrush()
        {
            var brush = new SolidColorBrush(WetInkColor);
            brush.Freeze();
            wetInkBrush = brush;
            wetInkPen = null;
        }

        void UpdateWetInkTransform()
        {
            var zoom = Zoom;
            wetInkTransform.Matrix = new Matrix(zoom, 0, 0, zoom, origin.X, origin.Y);
        }

        void UpdateBrushSize()
        {
            var radius = WetInkThickness * Zoom / 2;
            if (!isPointerInside || !IsEditable || IsSelectionMode || IsFillMode || radius <= 0)
            {
                brushSizeVisual.Opacity = 0;
                return;
            }

            if (brushSizeGeometry.RadiusX != radius)
            {
                brushSizeGeometry.RadiusX = radius;
                brushSizeGeometry.RadiusY = radius;
            }

            var point = CanvasToScreen(brushSizePoint);
            brushSizeTransform.X = point.X;
            brushSizeTransform.Y = point.Y;
            brushSizeVisual.Opacity = 1;
        }

        static void OnBrushSizeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is PenEditorCanvas canvas)
                canvas.UpdateBrushSize();
        }

        void UpdateCursor(Point canvasPoint)
        {
            if (!IsEditable)
            {
                Cursor = Cursors.No;
                return;
            }

            var bounds = SelectionBounds;
            var handle = !IsSelectionMode || bounds.IsEmpty
                ? PenSelectionHandle.None
                : HitTestHandle(canvasPoint, bounds);
            Cursor = GetHandleCursor(handle);
        }

        static Cursor GetHandleCursor(PenSelectionHandle handle) => handle switch
        {
            PenSelectionHandle.TopLeft or PenSelectionHandle.BottomRight => Cursors.SizeNWSE,
            PenSelectionHandle.TopRight or PenSelectionHandle.BottomLeft => Cursors.SizeNESW,
            PenSelectionHandle.Top or PenSelectionHandle.Bottom => Cursors.SizeNS,
            PenSelectionHandle.Left or PenSelectionHandle.Right => Cursors.SizeWE,
            PenSelectionHandle.Move => Cursors.SizeAll,
            PenSelectionHandle.Rotate => Cursors.Hand,
            _ => Cursors.Cross,
        };

        static void OnIsEditableChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not PenEditorCanvas canvas)
                return;

            canvas.Cursor = (bool)e.NewValue ? Cursors.Cross : Cursors.No;
            canvas.UpdateBrushSize();
        }

        static void OnWetInkColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is PenEditorCanvas canvas)
                canvas.UpdateWetInkBrush();
        }

        void DrawSelection(DrawingContext drawingContext)
        {
            var bounds = SelectionBounds;
            if (!bounds.IsEmpty)
            {
                var origin = CanvasToScreen(new Point(bounds.X, bounds.Y));
                var zoom = Zoom;
                drawingContext.DrawRectangle(null, SelectionPen, new Rect(origin, new Size(bounds.Width * zoom, bounds.Height * zoom)));
                DrawHandles(drawingContext, origin, bounds.Width * zoom, bounds.Height * zoom);
            }

            var points = lassoPoints;
            if (points is null || points.Count < 2)
                return;

            if (IsRectangleSelection)
            {
                drawingContext.DrawRectangle(null, LassoPen, new Rect(CanvasToScreen(points[0]), CanvasToScreen(points[1])));
                return;
            }

            var previous = CanvasToScreen(points[0]);
            for (var i = 1; i < points.Count; i++)
            {
                var current = CanvasToScreen(points[i]);
                drawingContext.DrawLine(LassoPen, previous, current);
                previous = current;
            }
            drawingContext.DrawLine(LassoPen, previous, CanvasToScreen(points[0]));
        }

        static void DrawHandles(DrawingContext drawingContext, Point origin, double width, double height)
        {
            var centerX = origin.X + width / 2;
            var rotate = new Point(centerX, origin.Y - RotateHandleDistance);
            drawingContext.DrawLine(SelectionPen, new Point(centerX, origin.Y), rotate);
            DrawHandle(drawingContext, rotate);

            var canResizeX = width > HandleSize;
            var canResizeY = height > HandleSize;
            var centerY = origin.Y + height / 2;
            var right = origin.X + width;
            var bottom = origin.Y + height;
            if (canResizeX && canResizeY)
            {
                DrawHandle(drawingContext, new Point(origin.X, origin.Y));
                DrawHandle(drawingContext, new Point(right, origin.Y));
                DrawHandle(drawingContext, new Point(right, bottom));
                DrawHandle(drawingContext, new Point(origin.X, bottom));
            }
            if (canResizeY)
            {
                DrawHandle(drawingContext, new Point(centerX, origin.Y));
                DrawHandle(drawingContext, new Point(centerX, bottom));
            }
            if (canResizeX)
            {
                DrawHandle(drawingContext, new Point(right, centerY));
                DrawHandle(drawingContext, new Point(origin.X, centerY));
            }
        }

        static void DrawHandle(DrawingContext drawingContext, Point center)
        {
            var half = HandleSize / 2;
            drawingContext.DrawRectangle(HandleBrush, HandlePen, new Rect(center.X - half, center.Y - half, HandleSize, HandleSize));
        }

        Rect GetCanvasRect()
        {
            var zoom = Zoom;
            return new Rect(origin.X, origin.Y, CanvasWidth * zoom, CanvasHeight * zoom);
        }

        static void OnCanvasSizeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is PenEditorCanvas canvas)
                canvas.ResetView();
        }

        static object CoerceTaperLength(DependencyObject sender, object value)
        {
            var length = (double)value;
            return double.IsNaN(length) || length < 0 ? 0d : length;
        }

        static object CoerceStabilizationStrength(DependencyObject sender, object value)
        {
            var strength = (double)value;
            if (double.IsNaN(strength) || strength < 0)
                return 0d;
            return strength > MaxStabilizationStrength ? MaxStabilizationStrength : strength;
        }

        static object CoerceZoom(DependencyObject sender, object value)
        {
            var zoom = (double)value;
            if (double.IsNaN(zoom) || zoom < MinZoom)
                return MinZoom;
            return zoom > MaxZoom ? MaxZoom : zoom;
        }

        static System.Windows.Media.Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        static System.Windows.Media.Pen CreateFrozenPen(Color color, double thickness)
        {
            var pen = new System.Windows.Media.Pen(CreateFrozenBrush(color), thickness);
            pen.Freeze();
            return pen;
        }

        static System.Windows.Media.Pen CreateFrozenDashedPen(Color color, double thickness)
        {
            var pen = new System.Windows.Media.Pen(CreateFrozenBrush(color), thickness)
            {
                DashStyle = new DashStyle([4, 4], 0),
            };
            pen.Freeze();
            return pen;
        }

        static System.Windows.Media.Brush CreateCheckerBrush()
        {
            var light = CreateFrozenBrush(Color.FromRgb(0x50, 0x50, 0x50));
            var dark = CreateFrozenBrush(Color.FromRgb(0x40, 0x40, 0x40));
            var drawing = new DrawingGroup();
            using (var context = drawing.Open())
            {
                context.DrawRectangle(dark, null, new Rect(0, 0, CheckerCellSize * 2, CheckerCellSize * 2));
                context.DrawRectangle(light, null, new Rect(0, 0, CheckerCellSize, CheckerCellSize));
                context.DrawRectangle(light, null, new Rect(CheckerCellSize, CheckerCellSize, CheckerCellSize, CheckerCellSize));
            }
            drawing.Freeze();
            var brush = new DrawingBrush(drawing)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, CheckerCellSize * 2, CheckerCellSize * 2),
                ViewportUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.None,
            };
            brush.Freeze();
            return brush;
        }
    }
}
