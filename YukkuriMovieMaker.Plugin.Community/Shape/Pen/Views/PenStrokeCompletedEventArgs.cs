using System.Windows.Input;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
{
    internal sealed class PenStrokeCompletedEventArgs(StylusPointCollection stylusPoints) : EventArgs
    {
        public StylusPointCollection StylusPoints { get; } = stylusPoints;
    }
}
