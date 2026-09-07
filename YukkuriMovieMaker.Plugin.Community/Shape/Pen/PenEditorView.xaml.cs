using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    public partial class PenEditorView : UserControl
    {
        public PenEditorView()
        {
            InitializeComponent();
            canvas.StrokeCompleted += OnStrokeCompleted;
            AttachEditor(layerOpacitySlider);
        }

        void AttachEditor(IPropertyEditorControl editor)
        {
            editor.BeginEdit += OnBeginEdit;
            editor.EndEdit += OnEndEdit;
        }

        void OnBeginEdit(object? sender, EventArgs e)
        {
            if (DataContext is PenEditorViewModel viewModel)
                viewModel.BeginEditUnit();
        }

        void OnEndEdit(object? sender, EventArgs e)
        {
            if (DataContext is PenEditorViewModel viewModel)
                viewModel.EndEditUnit();
        }

        void OnStrokeCompleted(object? sender, PenStrokeCompletedEventArgs e)
        {
            if (DataContext is PenEditorViewModel viewModel)
                viewModel.AddStroke(e.StylusPoints);
        }
    }
}
