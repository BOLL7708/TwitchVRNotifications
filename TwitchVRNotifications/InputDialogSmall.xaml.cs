using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace TwitchVRNotifications
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class InputDialogSmall : Window
    {
        public string value;

        public InputDialogSmall(string value, string label)
        {
            this.value = value;
            InitializeComponent();
            labelValue.Content = label;
            textBoxValue.Text = value.ToString();
            textBoxValue.Focus();
            textBoxValue.SelectAll();
        }

        private void ButtonValueOK_Click(object sender, RoutedEventArgs e)
        {
            value = textBoxValue.Text;
            DialogResult = true;
        }
    }
}
