using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    class PenFillRequestedEventArgs(Point point) : EventArgs
    {
        public Point Point { get; } = point;
    }
}
