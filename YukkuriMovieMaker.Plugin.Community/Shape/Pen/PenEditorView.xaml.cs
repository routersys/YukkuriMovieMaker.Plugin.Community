using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    public partial class PenEditorView : UserControl
    {
        public PenEditorView()
        {
            InitializeComponent();
            layerColumn.Width = new GridLength(PenSettings.Default.LayerPanelWidth);
            layerPanel.SizeChanged += OnLayerPanelSizeChanged;
            canvas.StrokeCompleted += OnStrokeCompleted;
            canvas.LassoCompleted += OnLassoCompleted;
            canvas.SelectionTransformStarted += OnSelectionTransformStarted;
            canvas.SelectionTransformed += OnSelectionTransformed;
            canvas.SelectionTransformCompleted += OnSelectionTransformCompleted;
            AttachEditor(layerOpacitySlider);
            AttachEditor(layerLengthSlider);
            AttachEditor(layerOffsetSlider);
        }

        void OnFitToScreenClick(object sender, RoutedEventArgs e)
        {
            canvas.ResetView();
        }

        void OnLayerRowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2 || sender is not FrameworkElement element || element.DataContext is not PenLayer layer)
                return;

            layer.BeginRename();
            e.Handled = true;
        }

        void OnRenameLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is PenLayer layer)
                layer.CommitRename();
        }

        void OnRenameKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not PenLayer layer)
                return;

            if (e.Key is Key.Enter)
            {
                layer.CommitRename();
                e.Handled = true;
            }
            else if (e.Key is Key.Escape)
            {
                layer.CancelRename();
                e.Handled = true;
            }
        }

        void OnLayerPanelSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged && e.NewSize.Width > 0)
                PenSettings.Default.LayerPanelWidth = e.NewSize.Width;
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

        void OnSelectionTransformStarted(object? sender, EventArgs e)
        {
            if (DataContext is PenEditorViewModel viewModel)
                viewModel.BeginSelectionTransform();
        }

        void OnSelectionTransformed(object? sender, PenSelectionTransformedEventArgs e)
        {
            if (DataContext is PenEditorViewModel viewModel)
                viewModel.TransformSelection(e.Matrix);
        }

        void OnSelectionTransformCompleted(object? sender, EventArgs e)
        {
            if (DataContext is PenEditorViewModel viewModel)
                viewModel.EndSelectionTransform();
        }
    }
}
