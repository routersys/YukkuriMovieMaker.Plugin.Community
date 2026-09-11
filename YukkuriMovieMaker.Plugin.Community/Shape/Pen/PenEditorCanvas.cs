using System.Globalization;
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
        const double DragThreshold = 3.0;
        const float NeutralPressure = 0.5f;
        const double SelectionGrabMargin = 4.0;
        const double HandleSize = 8.0;
        const double RotateHandleSize = 10.0;
        const double RotateHandleDistance = 22.0;
        const double MinSelectionScale = 0.01;
        const double RotationSnapAngle = 15.0;
        const double MaxStabilizationStrength = 0.95;
        const double StabilizationSettleDistance = 0.5;
        const double BadgeRadius = 9.0;
        const double BadgeRingRadius = 12.0;
        const double BadgeFontSize = 11.0;
        const double BadgeStackStep = BadgeRadius * 2 + 2;

        static readonly System.Windows.Media.Brush BackgroundBrush = CreateFrozenBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        static readonly System.Windows.Media.Brush CheckerBrush = CreateCheckerBrush();
        static readonly System.Windows.Media.Pen BorderPen = CreateFrozenPen(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF), 1.0);
        static readonly System.Windows.Media.Pen OutlineShadowPen = CreateFrozenPen(Color.FromArgb(0xFF, 0x00, 0x00, 0x00), 1.0);
        static readonly System.Windows.Media.Pen OutlinePen = CreateFrozenDashedPen(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 1.0);
        static readonly System.Windows.Media.Brush HandleBrush = CreateFrozenBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
        static readonly System.Windows.Media.Pen HandlePen = CreateFrozenPen(Color.FromArgb(0xFF, 0x00, 0x00, 0x00), 1.0);
        static readonly System.Windows.Media.Pen BrushSizeShadowPen = CreateFrozenPen(Color.FromArgb(0xFF, 0x00, 0x00, 0x00), 3.0);
        static readonly System.Windows.Media.Pen BrushSizePen = CreateFrozenPen(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 1.0);
        static readonly System.Windows.Media.Brush BadgeBrush = CreateFrozenBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
        static readonly System.Windows.Media.Brush BadgeSelectedBrush = CreateFrozenBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
        static readonly System.Windows.Media.Brush BadgeDimBrush = CreateFrozenBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));
        static readonly System.Windows.Media.Brush BadgeDimSelectedBrush = CreateFrozenBrush(Color.FromArgb(0x60, 0x00, 0x00, 0x00));
        static readonly System.Windows.Media.Pen BadgePen = CreateFrozenPen(Color.FromArgb(0xFF, 0x00, 0x00, 0x00), 1.0);
        static readonly System.Windows.Media.Pen BadgeSelectedPen = CreateFrozenPen(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 1.0);
        static readonly System.Windows.Media.Pen BadgeDimPen = CreateFrozenPen(Color.FromArgb(0x60, 0x00, 0x00, 0x00), 1.0);
        static readonly System.Windows.Media.Pen BadgeDimSelectedPen = CreateFrozenPen(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF), 1.0);
        static readonly System.Windows.Media.Pen BadgeDropPen = CreateFrozenPen(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 2.0);
        static readonly System.Windows.Media.Pen BadgeDropShadowPen = CreateFrozenPen(Color.FromArgb(0xFF, 0x00, 0x00, 0x00), 4.0);
        static readonly Typeface BadgeTypeface = new(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

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

        public static readonly DependencyProperty IsOrderModeProperty =
            DependencyProperty.Register(nameof(IsOrderMode), typeof(bool), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnIsOrderModeChanged));

        public static readonly DependencyProperty OrderBadgesProperty =
            DependencyProperty.Register(nameof(OrderBadges), typeof(PenOrderBadge[]), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(null, OnOrderBadgesChanged));

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

        public bool IsOrderMode
        {
            get => (bool)GetValue(IsOrderModeProperty);
            set => SetValue(IsOrderModeProperty, value);
        }

        public PenOrderBadge[]? OrderBadges
        {
            get => (PenOrderBadge[]?)GetValue(OrderBadgesProperty);
            set => SetValue(OrderBadgesProperty, value);
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

        public event EventHandler<PenViewChangedEventArgs>? ViewChanged;

        public event EventHandler<PenOrderBadgeEventArgs>? OrderBadgePressed;

        public event EventHandler<PenOrderBadgeEventArgs>? OrderBadgeDropped;

        public event EventHandler? OrderBackgroundPressed;

        readonly DrawingVisual wetInkVisual = new();
        readonly DrawingGroup wetInkDrawing = new();
        readonly MatrixTransform wetInkTransform = new();
        readonly PenPointerPressure pointerPressure = new();
        readonly StreamGeometry lassoGeometry = new();
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

        readonly Dictionary<int, Geometry> badgeGeometries = [];
        readonly List<PenOrderBadgeVisual> badgeVisuals = [];
        readonly List<int> badgeSignatures = [];
        readonly List<Point> badgeCenters = [];
        readonly PenOrderBadgeVisual dropVisual = new();
        double pixelsPerDip = 1;
        bool isOrderDragging;
        bool isOrderDragMoved;
        int dragLayerIndex;
        int dropBadgeIndex = -1;
        Point orderPressPoint;
        Point orderPointer;

        Point origin;
        bool isPanning;
        bool isPanMoved;
        Point panStart;

        public PenEditorCanvas()
        {
            Focusable = true;
            ClipToBounds = true;

            Cursor = Cursors.Cross;
            Loaded += OnCanvasLoaded;
            Unloaded += OnCanvasUnloaded;
            wetInkVisual.Transform = wetInkTransform;
            using (var context = wetInkVisual.RenderOpen())
                context.DrawDrawing(wetInkDrawing);
            AddVisualChild(wetInkVisual);
            brushSizeVisual.Transform = brushSizeTransform;
            brushSizeVisual.Opacity = 0;
            using (var context = brushSizeVisual.RenderOpen())
            {
                context.DrawGeometry(null, BrushSizeShadowPen, brushSizeGeometry);
                context.DrawGeometry(null, BrushSizePen, brushSizeGeometry);
            }
            AddVisualChild(brushSizeVisual);
            dropVisual.Opacity = 0;
            using (var context = dropVisual.RenderOpen())
            {
                context.DrawEllipse(null, BadgeDropShadowPen, default, BadgeRingRadius, BadgeRingRadius);
                context.DrawEllipse(null, BadgeDropPen, default, BadgeRingRadius, BadgeRingRadius);
            }
            AddVisualChild(dropVisual);
            UpdateWetInkBrush();
        }

        void OnCanvasLoaded(object sender, RoutedEventArgs e)
        {
            pointerPressure.Attach(this);
            SetPixelsPerDip(VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            SetPixelsPerDip(newDpi.PixelsPerDip);
        }

        void SetPixelsPerDip(double value)
        {
            if (pixelsPerDip == value)
                return;
            pixelsPerDip = value;
            badgeGeometries.Clear();
            for (var i = 0; i < badgeSignatures.Count; i++)
                badgeSignatures[i] = -1;
            SyncBadgeVisuals();
        }

        void OnCanvasUnloaded(object sender, RoutedEventArgs e) => pointerPressure.Dispose();

        protected override int VisualChildrenCount => badgeVisuals.Count + 3;

        protected override Visual GetVisualChild(int index)
        {
            if (index == 0)
                return wetInkVisual;
            var badgeIndex = index - 1;
            if (badgeIndex < badgeVisuals.Count)
                return badgeVisuals[badgeIndex];
            return badgeIndex == badgeVisuals.Count
                ? dropVisual
                : badgeIndex == badgeVisuals.Count + 1
                    ? brushSizeVisual
                    : throw new ArgumentOutOfRangeException(nameof(index));
        }

        public bool IsStrokeInProgress => strokePoints is not null || lassoPoints is not null || isMovingSelection || isOrderDragging;

        public void BeginStroke(Point canvasPoint, float pressure)
        {
            if (IsStrokeInProgress)
                return;

            if (IsOrderMode)
            {
                BeginOrder(canvasPoint);
                return;
            }

            if (!IsEditable)
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
            if (isOrderDragging)
            {
                MoveOrder(canvasPoint);
                return;
            }

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

            if (isOrderDragging)
            {
                EndOrder();
                return;
            }

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

        void BeginOrder(Point canvasPoint)
        {
            var index = HitTestBadge(canvasPoint);
            if (index < 0)
            {
                OrderBackgroundPressed?.Invoke(this, EventArgs.Empty);
                return;
            }

            var badges = OrderBadges!;
            var badge = badges[index];
            var isToggle = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            OrderBadgePressed?.Invoke(this, new PenOrderBadgeEventArgs(badge.LayerIndex, badge.StrokeIndex, isToggle));

            index = HitTestBadge(canvasPoint);
            if (index < 0)
                return;
            badge = OrderBadges![index];
            if (!badge.IsMovable || !badge.IsSelected)
                return;

            isOrderDragging = true;
            isOrderDragMoved = false;
            dragLayerIndex = badge.LayerIndex;
            dropBadgeIndex = -1;
            orderPressPoint = canvasPoint;
            orderPointer = canvasPoint;
        }

        void MoveOrder(Point canvasPoint)
        {
            orderPointer = canvasPoint;
            if (!isOrderDragMoved)
            {
                var zoom = Zoom;
                var dx = (canvasPoint.X - orderPressPoint.X) * zoom;
                var dy = (canvasPoint.Y - orderPressPoint.Y) * zoom;
                if (dx * dx + dy * dy < DragThreshold * DragThreshold)
                    return;
                isOrderDragMoved = true;
            }

            var index = HitTestBadge(canvasPoint);
            if (index >= 0)
            {
                var badge = OrderBadges![index];
                if (badge.IsSelected || badge.LayerIndex != dragLayerIndex)
                    index = -1;
            }
            dropBadgeIndex = index;
            UpdateDropVisual();
            InvalidateVisual();
        }

        void EndOrder()
        {
            isOrderDragging = false;
            var index = isOrderDragMoved ? dropBadgeIndex : -1;
            isOrderDragMoved = false;
            dropBadgeIndex = -1;
            UpdateDropVisual();
            InvalidateVisual();

            var badges = OrderBadges;
            if (badges is null || index < 0 || index >= badges.Length)
                return;
            var badge = badges[index];
            OrderBadgeDropped?.Invoke(this, new PenOrderBadgeEventArgs(badge.LayerIndex, badge.StrokeIndex, false));
        }

        int HitTestBadge(Point canvasPoint)
        {
            var screen = CanvasToScreen(canvasPoint);
            for (var i = badgeCenters.Count - 1; i >= 0; i--)
            {
                var center = badgeCenters[i];
                var dx = center.X - screen.X;
                var dy = center.Y - screen.Y;
                if (dx * dx + dy * dy <= BadgeRadius * BadgeRadius)
                    return i;
            }
            return -1;
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

            var rotateRadius = RotateHandleSize / 2 / Zoom;
            var rotateX = canvasPoint.X - centerX;
            var rotateY = canvasPoint.Y - (bounds.Y - RotateHandleDistance / Zoom);
            if (rotateX * rotateX + rotateY * rotateY <= rotateRadius * rotateRadius)
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
            RaiseViewChanged();
            InvalidateVisual();
        }

        void RaiseViewChanged()
            => ViewChanged?.Invoke(this, new PenViewChangedEventArgs(Zoom, origin, RenderSize, VisualTreeHelper.GetDpi(this).DpiScaleX));

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
                drawingContext.DrawImage(documentImage, new Rect(0, 0, documentImage.Width, documentImage.Height));
            drawingContext.DrawRectangle(null, BorderPen, rect);
            DrawSelection(drawingContext);
            DrawOrderBadges(drawingContext);
        }

        void DrawOrderBadges(DrawingContext drawingContext)
        {
            var badges = OrderBadges;
            if (!IsOrderMode || badges is null || !isOrderDragMoved)
                return;

            var pointer = CanvasToScreen(orderPointer);
            var count = Math.Min(badges.Length, badgeCenters.Count);
            for (var i = 0; i < count; i++)
            {
                var badge = badges[i];
                if (!badge.IsSelected || badge.LayerIndex != dragLayerIndex)
                    continue;
                drawingContext.DrawLine(OutlineShadowPen, badgeCenters[i], pointer);
                drawingContext.DrawLine(OutlinePen, badgeCenters[i], pointer);
            }
        }

        static void OnOrderBadgesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is PenEditorCanvas canvas)
                canvas.SyncBadgeVisuals();
        }

        void SyncBadgeVisuals()
        {
            var badges = IsOrderMode ? OrderBadges : null;
            var count = badges?.Length ?? 0;

            while (badgeVisuals.Count > count)
            {
                var last = badgeVisuals.Count - 1;
                RemoveVisualChild(badgeVisuals[last]);
                badgeVisuals.RemoveAt(last);
                badgeSignatures.RemoveAt(last);
                badgeCenters.RemoveAt(last);
            }
            while (badgeVisuals.Count < count)
            {
                var visual = new PenOrderBadgeVisual();
                AddVisualChild(visual);
                badgeVisuals.Add(visual);
                badgeSignatures.Add(-1);
                badgeCenters.Add(default);
            }

            for (var i = 0; i < count; i++)
            {
                var badge = badges![i];
                var signature = (badge.Number << 3) | (badge.IsSelected ? 4 : 0) | (badge.IsDrawn ? 2 : 0) | (badge.IsIndependent ? 1 : 0);
                if (badgeSignatures[i] != signature)
                {
                    RenderBadge(badgeVisuals[i], badge);
                    badgeSignatures[i] = signature;
                }
            }
            UpdateBadgeTransforms();
            if (dropBadgeIndex >= count)
                dropBadgeIndex = -1;
            UpdateDropVisual();
        }

        void RenderBadge(PenOrderBadgeVisual visual, in PenOrderBadge badge)
        {
            var fill = badge.IsDrawn
                ? (badge.IsSelected ? BadgeSelectedBrush : BadgeBrush)
                : (badge.IsSelected ? BadgeDimSelectedBrush : BadgeDimBrush);
            var pen = badge.IsDrawn
                ? (badge.IsSelected ? BadgeSelectedPen : BadgePen)
                : (badge.IsSelected ? BadgeDimSelectedPen : BadgeDimPen);
            var textBrush = badge.IsDrawn
                ? (badge.IsSelected ? BadgeBrush : BadgeSelectedBrush)
                : (badge.IsSelected ? BadgeDimBrush : BadgeDimSelectedBrush);

            using var context = visual.RenderOpen();
            if (badge.IsIndependent)
            {
                context.DrawEllipse(null, OutlineShadowPen, default, BadgeRingRadius, BadgeRingRadius);
                context.DrawEllipse(null, OutlinePen, default, BadgeRingRadius, BadgeRingRadius);
            }
            context.DrawEllipse(fill, pen, default, BadgeRadius, BadgeRadius);
            context.DrawGeometry(textBrush, null, GetBadgeGeometry(badge.Number));
        }

        Geometry GetBadgeGeometry(int number)
        {
            if (badgeGeometries.TryGetValue(number, out var geometry))
                return geometry;

            var text = new FormattedText(number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, BadgeTypeface, BadgeFontSize, BadgeSelectedBrush, pixelsPerDip);
            geometry = text.BuildGeometry(new Point(-text.Width / 2, -text.Height / 2));
            geometry.Freeze();
            badgeGeometries.Add(number, geometry);
            return geometry;
        }

        void UpdateBadgeTransforms()
        {
            var badges = OrderBadges;
            if (badges is null)
                return;

            var count = Math.Min(badges.Length, badgeVisuals.Count);
            for (var i = 0; i < count; i++)
            {
                var center = ResolveBadgeStack(CanvasToScreen(badges[i].Start), i);
                badgeCenters[i] = center;
                badgeVisuals[i].MoveTo(center);
            }
        }

        Point ResolveBadgeStack(Point center, int index)
        {
            var i = 0;
            while (i < index)
            {
                var other = badgeCenters[i];
                var dx = center.X - other.X;
                var dy = center.Y - other.Y;
                if (dx * dx + dy * dy < 1)
                {
                    center = new Point(other.X + BadgeStackStep, other.Y);
                    i = 0;
                    continue;
                }
                i++;
            }
            return center;
        }

        void UpdateDropVisual()
        {
            if (dropBadgeIndex < 0 || dropBadgeIndex >= badgeCenters.Count)
            {
                dropVisual.Opacity = 0;
                return;
            }

            dropVisual.MoveTo(badgeCenters[dropBadgeIndex]);
            dropVisual.Opacity = 1;
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
            RaiseViewChanged();
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
                if (!isPanMoved && Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
                    return;

                isPanMoved = true;
                origin = new Point(origin.X + delta.X, origin.Y + delta.Y);
                panStart = panPosition;
                UpdateWetInkTransform();
                RaiseViewChanged();
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
            if (e.StylusDevice is { } device)
            {
                var points = device.GetStylusPoints(this);
                if (points.Count > 0)
                    return points[^1].PressureFactor;
            }

            return pointerPressure.HasPressure ? pointerPressure.Pressure : NeutralPressure;
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
            UpdateBadgeTransforms();
            UpdateDropVisual();
        }

        void UpdateBrushSize()
        {
            var radius = WetInkThickness * Zoom / 2;
            if (!isPointerInside || !IsEditable || IsSelectionMode || IsFillMode || IsOrderMode || radius <= 0)
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
            if (IsOrderMode)
            {
                Cursor = HitTestBadge(canvasPoint) >= 0 ? Cursors.Hand : Cursors.Arrow;
                return;
            }

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

            canvas.Cursor = canvas.IsOrderMode ? Cursors.Arrow : (bool)e.NewValue ? Cursors.Cross : Cursors.No;
            canvas.UpdateBrushSize();
        }

        static void OnIsOrderModeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not PenEditorCanvas canvas)
                return;

            canvas.Cursor = (bool)e.NewValue ? Cursors.Arrow : canvas.IsEditable ? Cursors.Cross : Cursors.No;
            canvas.UpdateBrushSize();
            canvas.SyncBadgeVisuals();
        }

        static void OnWetInkColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is PenEditorCanvas canvas)
                canvas.UpdateWetInkBrush();
        }

        void DrawSelection(DrawingContext drawingContext)
        {
            var bounds = SelectionBounds;
            if (!bounds.IsEmpty && !IsOrderMode)
            {
                var origin = CanvasToScreen(new Point(bounds.X, bounds.Y));
                var zoom = Zoom;
                var frame = new Rect(origin, new Size(bounds.Width * zoom, bounds.Height * zoom));
                drawingContext.PushGuidelineSet(CreateGuidelines(frame));
                DrawOutline(drawingContext, frame);
                DrawHandles(drawingContext, origin, frame.Width, frame.Height);
                drawingContext.Pop();
            }

            var points = lassoPoints;
            if (points is null || points.Count < 2)
                return;

            if (IsRectangleSelection)
            {
                var area = new Rect(CanvasToScreen(points[0]), CanvasToScreen(points[1]));
                drawingContext.PushGuidelineSet(CreateGuidelines(area));
                DrawOutline(drawingContext, area);
                drawingContext.Pop();
                return;
            }

            using (var context = lassoGeometry.Open())
            {
                context.BeginFigure(CanvasToScreen(points[0]), false, true);
                for (var i = 1; i < points.Count; i++)
                    context.LineTo(CanvasToScreen(points[i]), true, false);
            }
            drawingContext.DrawGeometry(null, OutlineShadowPen, lassoGeometry);
            drawingContext.DrawGeometry(null, OutlinePen, lassoGeometry);
        }

        static GuidelineSet CreateGuidelines(Rect rect)
        {
            var half = OutlinePen.Thickness / 2;
            var guidelines = new GuidelineSet();
            guidelines.GuidelinesX.Add(rect.Left + half);
            guidelines.GuidelinesX.Add(rect.Left + rect.Width / 2 + half);
            guidelines.GuidelinesX.Add(rect.Right + half);
            guidelines.GuidelinesY.Add(rect.Top + half);
            guidelines.GuidelinesY.Add(rect.Top + rect.Height / 2 + half);
            guidelines.GuidelinesY.Add(rect.Bottom + half);
            return guidelines;
        }

        static void DrawOutline(DrawingContext drawingContext, Rect rect)
        {
            drawingContext.DrawRectangle(null, OutlineShadowPen, rect);
            drawingContext.DrawRectangle(null, OutlinePen, rect);
        }

        static void DrawHandles(DrawingContext drawingContext, Point origin, double width, double height)
        {
            var centerX = origin.X + width / 2;
            var rotate = new Point(centerX, origin.Y - RotateHandleDistance);
            drawingContext.DrawLine(OutlineShadowPen, new Point(centerX, origin.Y), rotate);
            drawingContext.DrawLine(OutlinePen, new Point(centerX, origin.Y), rotate);
            drawingContext.DrawEllipse(HandleBrush, HandlePen, rotate, RotateHandleSize / 2, RotateHandleSize / 2);

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
