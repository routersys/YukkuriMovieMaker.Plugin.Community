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
        const float MousePressure = 0.5f;
        const double SelectionGrabMargin = 4.0;
        const double MaxStabilizationStrength = 0.95;

        static readonly System.Windows.Media.Brush BackgroundBrush = CreateFrozenBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        static readonly System.Windows.Media.Brush CheckerBrush = CreateCheckerBrush();
        static readonly System.Windows.Media.Pen BorderPen = CreateFrozenPen(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF), 1.0);
        static readonly System.Windows.Media.Pen SelectionPen = CreateFrozenDashedPen(Color.FromArgb(0xFF, 0x2E, 0x86, 0xFF), 1.0);
        static readonly System.Windows.Media.Pen LassoPen = CreateFrozenDashedPen(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 1.0);

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
                new FrameworkPropertyMetadata(10d));

        public static readonly DependencyProperty IsEditableProperty =
            DependencyProperty.Register(nameof(IsEditable), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(true, OnIsEditableChanged));

        public static readonly DependencyProperty IsSelectionModeProperty =
            DependencyProperty.Register(nameof(IsSelectionMode), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SelectionBoundsProperty =
            DependencyProperty.Register(nameof(SelectionBounds), typeof(Rect), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(Rect.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StabilizationStrengthProperty =
            DependencyProperty.Register(nameof(StabilizationStrength), typeof(double), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(0d));

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

        public event EventHandler<PenSelectionMovedEventArgs>? SelectionMoved;

        public event EventHandler<PenStrokeCompletedEventArgs>? StrokeCompleted;

        readonly DrawingVisual wetInkVisual = new();
        readonly DrawingGroup wetInkDrawing = new();
        readonly MatrixTransform wetInkTransform = new();

        System.Windows.Media.Brush wetInkBrush = System.Windows.Media.Brushes.White;
        System.Windows.Media.Pen? wetInkPen;
        double wetInkPenThickness;
        StylusPointCollection? strokePoints;
        List<Point>? lassoPoints;
        bool isMovingSelection;
        Point moveStart;
        Vector moveDelta;

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
            UpdateWetInkBrush();
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index)
            => index == 0 ? wetInkVisual : throw new ArgumentOutOfRangeException(nameof(index));

        public bool IsStrokeInProgress => strokePoints is not null || lassoPoints is not null || isMovingSelection;

        public void BeginStroke(Point canvasPoint, float pressure)
        {
            if (!IsEditable)
                return;

            if (IsSelectionMode)
            {
                BeginSelection(canvasPoint);
                return;
            }

            wetInkDrawing.Children.Clear();
            strokePoints = [new StylusPoint(canvasPoint.X, canvasPoint.Y, pressure)];
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

            var previous = strokePoints[^1];
            var point = Stabilize(previous, canvasPoint, pressure);
            if (previous.X == point.X && previous.Y == point.Y)
                return;

            strokePoints.Add(point);
            AppendWetInk();
        }

        public void EndStroke()
        {
            if (lassoPoints is not null || isMovingSelection)
            {
                EndSelection();
                return;
            }

            var points = strokePoints;
            strokePoints = null;
            wetInkDrawing.Children.Clear();
            if (points is not null && points.Count > 0)
                StrokeCompleted?.Invoke(this, new PenStrokeCompletedEventArgs(points));
        }

        void BeginSelection(Point canvasPoint)
        {
            var bounds = SelectionBounds;
            if (!bounds.IsEmpty)
            {
                var margin = SelectionGrabMargin / Zoom;
                bounds.Inflate(margin, margin);
            }
            if (!bounds.IsEmpty && bounds.Contains(canvasPoint))
            {
                isMovingSelection = true;
                moveStart = canvasPoint;
                moveDelta = default;
                return;
            }

            lassoPoints = [canvasPoint];
            InvalidateVisual();
        }

        void AddSelectionPoint(Point canvasPoint)
        {
            if (isMovingSelection)
            {
                moveDelta = canvasPoint - moveStart;
                InvalidateVisual();
                return;
            }

            if (lassoPoints is null || lassoPoints[^1] == canvasPoint)
                return;

            lassoPoints.Add(canvasPoint);
            InvalidateVisual();
        }

        void EndSelection()
        {
            if (isMovingSelection)
            {
                isMovingSelection = false;
                var delta = moveDelta;
                moveDelta = default;
                InvalidateVisual();
                if (delta.X != 0 || delta.Y != 0)
                    SelectionMoved?.Invoke(this, new PenSelectionMovedEventArgs(delta));
                return;
            }

            var points = lassoPoints;
            lassoPoints = null;
            InvalidateVisual();
            if (points is not null)
                LassoCompleted?.Invoke(this, new PenLassoCompletedEventArgs(points));
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
            Focus();
            var points = e.GetStylusPoints(this);
            if (points.Count > 0)
                BeginStroke(ScreenToCanvas(new Point(points[0].X, points[0].Y)), points[0].PressureFactor);
            if (!IsStrokeInProgress)
                return;
            CaptureStylus();
            AddStylusPoints(points, 1);
            e.Handled = true;
        }

        protected override void OnStylusMove(StylusEventArgs e)
        {
            base.OnStylusMove(e);
            if (!IsStrokeInProgress)
                return;
            AddStylusPoints(e.GetStylusPoints(this), 0);
            e.Handled = true;
        }

        protected override void OnStylusUp(StylusEventArgs e)
        {
            base.OnStylusUp(e);
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

            if (e.ChangedButton is not MouseButton.Left || e.StylusDevice is not null)
                return;

            Focus();
            BeginStroke(ScreenToCanvas(e.GetPosition(this)), MousePressure);
            if (!IsStrokeInProgress)
                return;
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
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

            if (!IsStrokeInProgress || e.StylusDevice is not null)
                return;

            AddStrokePoint(ScreenToCanvas(e.GetPosition(this)), MousePressure);
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

            if (e.ChangedButton is not MouseButton.Left || !IsStrokeInProgress)
                return;

            AddStrokePoint(ScreenToCanvas(e.GetPosition(this)), MousePressure);
            EndStroke();
            ReleaseMouseCapture();
            e.Handled = true;
        }

        StylusPoint Stabilize(StylusPoint previous, Point canvasPoint, float pressure)
        {
            var strength = StabilizationStrength;
            if (strength <= 0)
                return new StylusPoint(canvasPoint.X, canvasPoint.Y, pressure);

            var rate = 1 - Math.Min(strength, MaxStabilizationStrength);
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

        static void OnIsEditableChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is PenEditorCanvas canvas)
                canvas.Cursor = (bool)e.NewValue ? Cursors.Cross : Cursors.No;
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
                var origin = CanvasToScreen(new Point(bounds.X + moveDelta.X, bounds.Y + moveDelta.Y));
                var zoom = Zoom;
                drawingContext.DrawRectangle(null, SelectionPen, new Rect(origin, new Size(bounds.Width * zoom, bounds.Height * zoom)));
            }

            var points = lassoPoints;
            if (points is null || points.Count < 2)
                return;

            var previous = CanvasToScreen(points[0]);
            for (var i = 1; i < points.Count; i++)
            {
                var current = CanvasToScreen(points[i]);
                drawingContext.DrawLine(LassoPen, previous, current);
                previous = current;
            }
            drawingContext.DrawLine(LassoPen, previous, CanvasToScreen(points[0]));
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
