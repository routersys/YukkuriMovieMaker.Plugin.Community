using System.Windows.Ink;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    static class PenStyleFactory
    {
        public static DrawingAttributes CreatePen()
        {
            var size = PenSettings.Default.PenStyle.StrokeThickness;
            var color = PenSettings.Default.PenStyle.StrokeColor;
            return new DrawingAttributes
            {
                Color = color,
                Width = size,
                Height = size,
                IsHighlighter = false,
                FitToCurve = true,
                IgnorePressure = !PenSettings.Default.PenStyle.IsPressure,
                StylusTip = StylusTip.Ellipse,
            };
        }

        public static DrawingAttributes CreateHighlighter()
        {
            var size = PenSettings.Default.HighlighterStyle.StrokeThickness;
            var color = PenSettings.Default.HighlighterStyle.StrokeColor;
            return new DrawingAttributes
            {
                Color = color,
                Width = size / 2,
                Height = size,
                IsHighlighter = true,
                FitToCurve = true,
                IgnorePressure = !PenSettings.Default.HighlighterStyle.IsPressure,
                StylusTip = StylusTip.Rectangle,
            };
        }
    }
}
