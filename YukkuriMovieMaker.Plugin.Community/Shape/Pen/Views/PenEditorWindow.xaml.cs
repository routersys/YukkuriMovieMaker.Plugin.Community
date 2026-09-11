using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views
{
    public partial class PenEditorWindow : Window
    {
        public PenEditorWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
