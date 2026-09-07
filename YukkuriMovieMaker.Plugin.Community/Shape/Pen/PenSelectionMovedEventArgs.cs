using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal sealed class PenSelectionMovedEventArgs(Vector delta) : EventArgs
    {
        public Vector Delta { get; } = delta;
    }
}
