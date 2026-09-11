namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    readonly record struct PenOrderEntry(int LayerIndex, int StrokeIndex, int Number, bool IsIndependent, bool IsDrawn);
}
