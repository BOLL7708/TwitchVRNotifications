using System.Diagnostics;
using System.Windows;


namespace TwitchVRNotifications
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Properties.Settings p = Properties.Settings.Default;
        MainController controller = new MainController();

        public MainWindow()
        {
            InitializeComponent();

            // Load settings
            textBox_UserName.Text = p.UserName;
            textBox_AuthToken.Text = p.AuthToken;
            textBox_Needle.Text = p.Needle;
            textBox_ClientID.Text = p.ClientID;
            checkBox_AutoConnectChat.IsChecked = p.AutoConnectChat;
            textBox_PlaceholderLogo.Text = p.PlaceholderLogo;

            // Connect to chat
            if (p.AutoConnectChat) controller.connectChat();
        }

        private void button_Save_Click(object sender, RoutedEventArgs e)
        {
            p.UserName = textBox_UserName.Text;
            p.AuthToken = textBox_AuthToken.Text;
            p.Needle = textBox_Needle.Text;
            p.ClientID = textBox_ClientID.Text;
            p.AutoConnectChat = (bool) checkBox_AutoConnectChat.IsChecked;
            p.PlaceholderLogo = textBox_PlaceholderLogo.Text;
            p.Save();
        }

        private void button_Connect_Click(object sender, RoutedEventArgs e)
        {
            controller.connectChat();
        }

        private void button_Browse_Click(object sender, RoutedEventArgs e)
        {
            // http://stackoverflow.com/a/10315283

            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".jpg";
            dlg.Filter = "JPG Files (*.jpg)|*.jpg|JPEG Files (*.jpeg)|*.jpeg";
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                string filename = dlg.FileName;
                textBox_PlaceholderLogo.Text = filename;
            }
        }

        private void button_Test_Click(object sender, RoutedEventArgs e)
        {
            controller.broadcastNotification("woboloko", "this is at test");
        }

        private void button_InitOpenVR_Click(object sender, RoutedEventArgs e)
        {
            controller.initVr();
        }
    }
}
