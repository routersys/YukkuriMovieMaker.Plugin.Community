using System.Windows;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    sealed class PenOrderBadgeVisual : DrawingVisual
    {
        public void MoveTo(Point point) => VisualOffset = new Vector(point.X, point.Y);
    }
}
