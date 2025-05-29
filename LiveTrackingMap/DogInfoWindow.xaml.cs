using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LiveTrackingMap
{
    /// <summary>
    /// Interaktionslogik für DogInfoWindow.xaml
    /// </summary>
    public partial class DogInfoWindow : Window
    {
        public DogInfoWindow()
        {
            InitializeComponent();
            this.DataContextChanged += DogInfoWindow_DataContextChanged;
        }

        private void DogInfoWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
