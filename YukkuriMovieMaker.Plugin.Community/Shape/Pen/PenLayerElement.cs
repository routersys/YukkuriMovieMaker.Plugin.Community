namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    readonly record struct PenLayerElement(IPenGeometry Geometry, int PointFrom, bool IsFill);
}
