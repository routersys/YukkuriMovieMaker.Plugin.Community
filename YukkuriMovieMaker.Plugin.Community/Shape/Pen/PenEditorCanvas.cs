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

        static readonly System.Windows.Media.Brush BackgroundBrush = CreateFrozenBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        static readonly System.Windows.Media.Brush CheckerBrush = CreateCheckerBrush();
        static readonly System.Windows.Media.Pen BorderPen = CreateFrozenPen(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF), 1.0);

        public static readonly DependencyProperty ImageProperty =
            DependencyProperty.Register(nameof(Image), typeof(ImageSource), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CanvasWidthProperty =
            DependencyProperty.Register(nameof(CanvasWidth), typeof(double), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(1920d, FrameworkPropertyMetadataOptions.AffectsRender, OnCanvasSizeChanged));

        public static readonly DependencyProperty CanvasHeightProperty =
            DependencyProperty.Register(nameof(CanvasHeight), typeof(double), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(1080d, FrameworkPropertyMetadataOptions.AffectsRender, OnCanvasSizeChanged));

        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(PenEditorCanvas),
                new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceZoom));

        public ImageSource? Image
        {
            get => (ImageSource?)GetValue(ImageProperty);
            set => SetValue(ImageProperty, value);
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

        Point origin;
        bool isPanning;
        bool isPanMoved;
        Point panStart;
        bool isViewInitialized;

        public PenEditorCanvas()
        {
            Focusable = true;
            ClipToBounds = true;
        }

        public void ResetView()
        {
            var size = RenderSize;
            if (size.Width <= 0 || size.Height <= 0 || CanvasWidth <= 0 || CanvasHeight <= 0)
            {
                isViewInitialized = false;
                return;
            }

            Zoom = Math.Min(size.Width / CanvasWidth, size.Height / CanvasHeight);
            origin = new Point(
                (size.Width - CanvasWidth * Zoom) / 2,
                (size.Height - CanvasHeight * Zoom) / 2);
            isViewInitialized = true;
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
            if (!isViewInitialized)
                ResetView();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var size = RenderSize;
            drawingContext.DrawRectangle(BackgroundBrush, null, new Rect(size));

            if (!isViewInitialized)
                ResetView();

            var rect = GetCanvasRect();
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            drawingContext.DrawRectangle(CheckerBrush, null, rect);
            var image = Image;
            if (image is not null)
                drawingContext.DrawImage(image, rect);
            drawingContext.DrawRectangle(null, BorderPen, rect);
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
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.ChangedButton is not MouseButton.Middle)
                return;

            isPanning = true;
            isPanMoved = false;
            panStart = e.GetPosition(this);
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!isPanning)
                return;

            var position = e.GetPosition(this);
            var delta = position - panStart;
            if (!isPanMoved && Math.Abs(delta.X) < PanThreshold && Math.Abs(delta.Y) < PanThreshold)
                return;

            isPanMoved = true;
            origin = new Point(origin.X + delta.X, origin.Y + delta.Y);
            panStart = position;
            InvalidateVisual();
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.ChangedButton is not MouseButton.Middle || !isPanning)
                return;

            isPanning = false;
            ReleaseMouseCapture();
            e.Handled = true;
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
