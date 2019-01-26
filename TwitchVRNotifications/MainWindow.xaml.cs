using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;

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
            if (p.Entropy.Length == 0)
            {
                p.Entropy = Utils.BytesToBase64String(Utils.GetRandomBytes(16));
                p.Save();
            }
            loadSettings();
            if (controller.OpenVR_Initiated) button_InitOpenVR.IsEnabled = false;
            if (controller.isChatConnected()) button_Connect.IsEnabled = false;
        }

        private void button_Save_Click(object sender, RoutedEventArgs e)
        {
            saveSettings();
        }

        private void loadSettings()
        {
            // Load settings
            textBox_UserName.Text = p.UserName;
            passwordBox_AuthToken.Password = Utils.DecryptStringFromBase64(p.AuthToken, p.Entropy);
            textBox_Needle.Text = p.Needle;
            passwordBox_ClientID.Password = Utils.DecryptStringFromBase64(p.ClientID, p.Entropy);
            checkBox_AutoConnectChat.IsChecked = p.AutoConnectChat;
            textBox_TestUsername.Text = p.TestUsername;
            textBox_TestMessage.Text = p.TestMessage;
            checkBox_AutoSave.IsChecked = p.AutoSave;
            checkBox_FilterOn.IsChecked = p.FilterOn;
        }

        private void saveSettings()
        {
            // Save settings
            p.UserName = textBox_UserName.Text;
            p.AuthToken = Utils.EncryptStringToBase64(passwordBox_AuthToken.Password, p.Entropy);
            p.Needle = textBox_Needle.Text;
            p.ClientID = Utils.EncryptStringToBase64(passwordBox_ClientID.Password, p.Entropy);
            p.AutoConnectChat = (bool)checkBox_AutoConnectChat.IsChecked;
            p.TestUsername = textBox_TestUsername.Text;
            p.TestMessage = textBox_TestMessage.Text;
            p.AutoSave = (bool)checkBox_AutoSave.IsChecked;
            p.FilterOn = (bool)checkBox_FilterOn.IsChecked;
            p.Save();
        }

        private void Window_Deactivated(object sender, System.EventArgs e)
        {
            if(p.AutoSave) saveSettings();
        }

        private void ClickedURL(object sender, RoutedEventArgs e)
        {
            var link = (Hyperlink)sender;
            Process.Start(link.NavigateUri.ToString());
        }

        private void button_Connect_Click(object sender, RoutedEventArgs e)
        {
            if (controller.isChatConnected()) button_Connect.IsEnabled = false;
        }

        private void button_Test_Click(object sender, RoutedEventArgs e)
        {
            controller.broadcastNotification(p.TestUsername, p.TestMessage);
        }

        private void button_InitOpenVR_Click(object sender, RoutedEventArgs e)
        {
            if (controller.initVr()) button_InitOpenVR.IsEnabled = false;
        }

        private void checkBox_AutoSave_Checked(object sender, RoutedEventArgs e)
        {
            saveSettings();
        }

        private void textBox_UserName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (p.AutoSave)
            {
                p.UserName = textBox_UserName.Text;
                p.Save();
            }
        }

        private void passwordBox_AuthToken_LostFocus(object sender, RoutedEventArgs e)
        {
            if (p.AutoSave)
            {
                p.AuthToken = Utils.EncryptStringToBase64(passwordBox_AuthToken.Password, p.Entropy);
                p.Save();
            }
        }

        private void textBox_Needle_LostFocus(object sender, RoutedEventArgs e)
        {
            if (p.AutoSave)
            {
                p.Needle = textBox_Needle.Text;
                p.Save();
            }
        }

        private void passwordBox_ClientID_LostFocus(object sender, RoutedEventArgs e)
        {
            if (p.AutoSave)
            {
                p.ClientID = Utils.EncryptStringToBase64(passwordBox_ClientID.Password, p.Entropy);
                p.Save();
            }
        }

        private void textBox_TestUsername_LostFocus(object sender, RoutedEventArgs e)
        {
            if (p.AutoSave)
            {
                p.TestUsername = textBox_TestUsername.Text;
                p.Save();
            }
        }

        private void textBox_TestMessage_LostFocus(object sender, RoutedEventArgs e)
        {
            if (p.AutoSave)
            {
                p.TestMessage = textBox_TestMessage.Text;
                p.Save();
            }
        }

        private void checkBox_AutoConnectChat_Checked(object sender, RoutedEventArgs e)
        {
            if (p.AutoSave)
            {
                p.AutoConnectChat = true;
                p.Save();
            }
        }

        private void checkBox_AutoConnectChat_Unchecked(object sender, RoutedEventArgs e)
        {
            if (p.AutoSave)
            {
                p.AutoConnectChat = false;
                p.Save();
            }
        }

        private void checkBox_FilterOn_Checked(object sender, RoutedEventArgs e)
        {
            if (p.AutoSave)
            {
                p.FilterOn = true;
                p.Save();
            }
        }

        private void checkBox_FilterOn_Unchecked(object sender, RoutedEventArgs e)
        {
            if (p.AutoSave)
            {
                p.FilterOn = false;
                p.Save();
            }
        }
    }
}
