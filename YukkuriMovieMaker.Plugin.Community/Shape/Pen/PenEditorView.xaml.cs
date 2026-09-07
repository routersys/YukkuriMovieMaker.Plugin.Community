using System.Windows.Controls;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    public partial class PenEditorView : UserControl
    {
        public PenEditorView()
        {
            InitializeComponent();
            canvas.StrokeCompleted += OnStrokeCompleted;
        }

        void OnStrokeCompleted(object? sender, PenStrokeCompletedEventArgs e)
        {
            if (DataContext is PenEditorViewModel viewModel)
                viewModel.AddStroke(e.StylusPoints);
        }
    }
}
