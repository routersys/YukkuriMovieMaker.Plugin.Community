namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    static class PenFillExpansionExtensions
    {
        const int LowPixels = 1;
        const int MediumPixels = 2;
        const int HighPixels = 4;

        public static int ToPixels(this PenFillExpansion expansion) => expansion switch
        {
            PenFillExpansion.Low => LowPixels,
            PenFillExpansion.Medium => MediumPixels,
            PenFillExpansion.High => HighPixels,
            _ => 0,
        };
    }
}
