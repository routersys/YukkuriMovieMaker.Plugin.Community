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
            canvas.LassoCompleted += OnLassoCompleted;
            canvas.SelectionMoved += OnSelectionMoved;
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

        void OnLassoCompleted(object? sender, PenLassoCompletedEventArgs e)
        {
            if (DataContext is PenEditorViewModel viewModel)
                viewModel.SelectByLasso(e.LassoPoints);
        }

        void OnSelectionMoved(object? sender, PenSelectionMovedEventArgs e)
        {
            if (DataContext is PenEditorViewModel viewModel)
                viewModel.MoveSelection(e.Delta);
        }
    }
}
