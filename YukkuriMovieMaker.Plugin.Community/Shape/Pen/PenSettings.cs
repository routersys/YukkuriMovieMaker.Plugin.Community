using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenSettings : SettingsBase<PenSettings>
    {
        public const double MinLayerPanelWidth = 160;

        public override SettingsCategory Category => SettingsCategory.None;

        public override string Name => "Pen";

        public override bool HasSettingView => false;

        public override object SettingView => throw new NotImplementedException();

        public PenMode PenMode { get => penMode; set => Set(ref penMode, value); }
        PenMode penMode = PenMode.Pen;

        public PenSelectionKind SelectionKind { get => selectionKind; set => Set(ref selectionKind, value); }
        PenSelectionKind selectionKind = PenSelectionKind.Lasso;

        public double LayerPanelWidth { get => layerPanelWidth; set => Set(ref layerPanelWidth, Math.Max(MinLayerPanelWidth, value)); }
        double layerPanelWidth = 240;

        public PenStyleSettings PenStyle { get; } = new() { StrokeColor = Colors.White };
        public PenStyleSettings HighlighterStyle { get; } = new() { StrokeColor = Colors.Yellow };
        public EraserStyleSettings EraserStyle { get; } = new ();

        public override void Initialize()
        {

        }
    }
}
