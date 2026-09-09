using System.Windows;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    static class PenFillFigures
    {
        public const int MinLength = 3;

        public static Geometry CreateGeometry(SerializableStroke stroke)
        {
            var geometry = new StreamGeometry { FillRule = FillRule.EvenOdd };
            using (var context = geometry.Open())
            {
                var points = stroke.StylusPoints;
                var figures = stroke.FillFigures ?? [];
                var index = 0;
                for (var i = 0; i < figures.Length; i++)
                {
                    var length = figures[i];
                    if (length < MinLength || index + length > points.Length)
                        break;

                    context.BeginFigure(new Point(points[index].X, points[index].Y), true, true);
                    for (var j = 1; j < length; j++)
                        context.LineTo(new Point(points[index + j].X, points[index + j].Y), false, false);
                    index += length;
                }
            }
            geometry.Freeze();
            return geometry;
        }
    }
}
