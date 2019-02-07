using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;

namespace TwitchVRNotifications
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Properties.Settings p;
        MainController controller;
        string garbage = "12345678901234567890";
        private SolidColorBrush red = new SolidColorBrush(Colors.Tomato);
        private SolidColorBrush green = new SolidColorBrush(Colors.OliveDrab);

        public MainWindow()
        {
            InitializeComponent();

            controller = new MainController();
            controller.openVRStatusEvent += OnOpenVRStatus;
            controller.chatBotStatusEvent += OnChatBotStatus;
            
            p = Properties.Settings.Default;
            if (p.Entropy.Length == 0)
            {
                p.Entropy = Utils.BytesToBase64String(Utils.GetRandomBytes(16));
                p.Save();
            }
            LoadSettings();
        }

        private void OnOpenVRStatus(bool ok, string message, string toolTip)
        {
            label_OpenVRStatus.Background = ok ? green : red;
            label_OpenVRStatus.Content = message;
            label_OpenVRStatus.ToolTip = toolTip;
        }

        private void OnChatBotStatus(bool ok, string message, string toolTip)
        {
            label_ChatBotStatus.Background = ok ? green : red;
            label_ChatBotStatus.Content = message;
            label_ChatBotStatus.ToolTip = toolTip;
        }

        private void LoadSettings()
        {
            // Load settings
            textBox_UserName.Text = p.BotUsername;
            passwordBox_ChatToken.Password = p.BotChatToken.Trim().Length > 0 ? garbage : "";
            passwordBox_ClientId.Password = p.AppClientId.Trim().Length > 0 ? garbage : "";
            passwordBox_Secret.Password = p.AppSecret.Trim().Length > 0 ? garbage : "";

            textBox_Needle.Text = p.MessagePrefix;
            checkBox_FilterOn.IsChecked = p.MessagePrefixOn;

            textBox_TestUsername.Text = p.TestUsername;
            textBox_TestMessage.Text = p.TestMessage;
        }

        private void Window_Deactivated(object sender, System.EventArgs e)
        {
            // Saved here before.
        }

        private void ClickedURL(object sender, RoutedEventArgs e)
        {
            var link = (Hyperlink)sender;
            Process.Start(link.NavigateUri.ToString());
        }

        private void Button_Test_Click(object sender, RoutedEventArgs e)
        {
            controller.BroadcastNotification(p.TestUsername, p.TestMessage);
        }
        
        private void CheckBox_FilterOn_Checked(object sender, RoutedEventArgs e)
        {
            p.MessagePrefixOn = true;
            p.Save();
        }

        private void CheckBox_FilterOn_Unchecked(object sender, RoutedEventArgs e)
        {
            p.MessagePrefixOn = false;
            p.Save();
        }

        private string ShowInputDialog(string value, string label, string description, string link = "")
        {
            InputDialog dlg = new InputDialog(value, label, description, link);
            dlg.Owner = this;
            dlg.ShowDialog();
            return dlg.DialogResult == true ? dlg.value.Trim() : null;
        }

        private void Button_EditUsername_Click(object sender, RoutedEventArgs e)
        {
            var username = ShowInputDialog(
                p.BotUsername,
                "Bot username:",
                "The username for the account that will connect to chat.\n" +
                "Common practice is to use a separate account for your bot."
            );
            if(username != null)
            {
                p.BotUsername = username;
                p.Save();
                textBox_UserName.Text = username.Length == 0 ? "" : username;
            }
        }

        private void Button_EditChatToken_Click(object sender, RoutedEventArgs e)
        {
            var token = ShowInputDialog(
                "",
                "Chat token:",
                "The OAuth token for connecting to the chat.\n" +
                "Get the token at this address:",
                "twitchapps.com/tmi/"
            );
            if(token != null)
            {
                if (token.Length > 0) token = Utils.EncryptStringToBase64(token, p.Entropy);
                p.BotChatToken = token;
                p.Save();
                passwordBox_ChatToken.Password = token.Length == 0 ? "" : garbage;
            }
        }

        private void Button_EditClientId_Click(object sender, RoutedEventArgs e)
        {
            var clientId = ShowInputDialog(
                "",
                "App client ID:",
                "The client ID you find if you manage your app on the dev site.\n" +
                "Used for backend APIs. Go here:",
                "dev.twitch.tv/dashboard"
            );
            if(clientId != null)
            {
                if (clientId.Length > 0) clientId = Utils.EncryptStringToBase64(clientId, p.Entropy);
                p.AppClientId = clientId;
                p.Save();
                passwordBox_ClientId.Password = clientId.Length == 0 ? "" : garbage;
            }
        }

        private void Button_EditSecret_Click(object sender, RoutedEventArgs e)
        {
            var secret = ShowInputDialog(
                "",
                "App secret:",
                "The client secret which you can create where you manage the app.\n" +
                "Used to get the access token avatars and follow status."
            );
            if(secret != null)
            {
                if(secret.Length > 0) secret= Utils.EncryptStringToBase64(secret, p.Entropy);
                p.AppSecret = secret;
                p.Save();
                passwordBox_Secret.Password = secret.Length == 0 ? "" : garbage;
            }
        }
    }
}
