using Vortice.Direct2D1;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    sealed class PenComposeChain
    {
        public ID2D1Effect? Effect;
        public ID2D1Image? Image;
        public ID2D1Image? ClipBase;

        public void Reset()
        {
            Effect = null;
            Image = null;
            ClipBase = null;
        }
    }
}
