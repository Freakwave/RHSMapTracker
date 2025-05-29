using Mapsui;
using Mapsui.Animations;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LiveTrackingMap
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            if (this.DataContext is MainViewModel viewModel)
            {
                MyMapControl.Map = viewModel.Map; // Assign the Map object from ViewModel to the MapControl
                viewModel.InitializeEditing(MyMapControl);
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (this.DataContext is MainViewModel viewModel)
            {
                viewModel.TogglePolygonEdit();
            }
        }
    }
}