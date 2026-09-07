using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal sealed class PenLassoCompletedEventArgs(IReadOnlyList<Point> lassoPoints) : EventArgs
    {
        public IReadOnlyList<Point> LassoPoints { get; } = lassoPoints;
    }
}
