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
    public partial class InputDialog : Window
    {
        public string value;

        public InputDialog(string value, string label, string description, string link = "")
        {
            this.value = value;
            InitializeComponent();
            labelValue.Content = label;

            textBlock_ValueDescription.Text = description.Trim()+" ";
            if(link.Length > 0)
            {
                var hp = new Hyperlink(new Run(link));
                hp.NavigateUri = new Uri("https://"+link);
                hp.Click += (sender, eventArgs) => {
                    Process.Start(((Hyperlink) sender).NavigateUri.ToString());
                };
                textBlock_ValueDescription.Inlines.Add(hp);
            }

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
