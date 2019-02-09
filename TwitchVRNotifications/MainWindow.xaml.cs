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
            p = Properties.Settings.Default;
            if (p.UpgradeNeeded) { // If we move the application we should fetch old settings
                p.Upgrade();
                p.Save();
                p.Reload();
                p.UpgradeNeeded = false;
                p.Save();
            }

            controller = new MainController();
            controller.openVRStatusEvent += OnOpenVRStatus;
            controller.chatBotStatusEvent += OnChatBotStatus;
            
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
            textBox_Channel.Text = p.BotChannel;
            textBox_UserName.Text = p.BotUsername;
            passwordBox_ChatToken.Password = p.BotChatToken.Trim().Length > 0 ? garbage : "";

            passwordBox_ClientId.Password = p.AppClientId.Trim().Length > 0 ? garbage : "";
            passwordBox_Secret.Password = p.AppSecret.Trim().Length > 0 ? garbage : "";

            textBox_Needle.Text = p.MessagePrefix;
            checkBox_FilterOn.IsChecked = p.MessagePrefixOn;
            checkBox_AllowFollower.IsChecked = p.AllowFollower;
            checkBox_AllowSubscriber.IsChecked = p.AllowSubscriber;
            checkBox_AllowModerator.IsChecked = p.AllowModerator;
            checkBox_AllowVIP.IsChecked = p.AllowVIP;

            checkBox_NotifyConnectivity.IsChecked = p.NotifyConnectivity;
            checkBox_NotifySubscribed.IsChecked = p.NotifySubscribed;
            checkBox_NotifyHosted.IsChecked = p.NotifyHosted;
            checkBox_NotifyRaided.IsChecked = p.NotifyRaided;

            checkBox_IgnoreBroadcaster.IsChecked = p.IgnoreBroadcaster;
            checkBox_IgnoreBots.IsChecked = p.IgnoreBots;
            textBox_IgnoreUsers.Text = p.IgnoreUsers;

            checkBox_AvatarEnabled.IsChecked = p.AvatarEnabled;
            checkBox_AvatarFrameEnabled.IsChecked = p.AvatarFrameEnabled;
            checkBox_AvatarBadgesEnabled.IsChecked = p.AvatarBadgesEnabled;

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
    
        private string ShowInputDialogLarge(string value, string label, string description, string link = "")
        {
            InputDialogLarge dlg = new InputDialogLarge(value, label, description, link);
            dlg.Owner = this;
            dlg.ShowDialog();
            return dlg.DialogResult == true ? dlg.value.Trim() : null;
        }

        private string ShowInputDialogSmall(string value, string label)
        {
            InputDialogSmall dlg = new InputDialogSmall(value, label);
            dlg.Owner = this;
            dlg.ShowDialog();
            return dlg.DialogResult == true ? dlg.value.Trim() : null;
        }

        #region Chat settings
        private void Button_EditChannel_Click(object sender, RoutedEventArgs e)
        {
            var channel = ShowInputDialogLarge(
                p.BotChannel,
                "Channel:",
                "The channel the bot will connect to and monitor.\n" +
                "The bot does not need any kind of extra privileges."
            );
            if (channel != null)
            {
                p.BotChannel = channel;
                p.Save();
                textBox_Channel.Text = channel.Length == 0 ? "" : channel;
                controller.ConnectChat();
            }
        }

        private void Button_EditUsername_Click(object sender, RoutedEventArgs e)
        {
            var username = ShowInputDialogLarge(
                p.BotUsername,
                "Bot username:",
                "The username for the account that will connect to chat.\n" +
                "Common practice is to use a separate account for your bot."
            );
            if(username != null)
            {
                p.BotUsername = username;
                if (p.TestUsername.Length == 0)
                {
                    p.TestUsername = username;
                    textBox_TestUsername.Text = username;
                }
                p.Save();
                textBox_UserName.Text = username;
                controller.ConnectChat();
            }
        }

        private void Button_EditChatToken_Click(object sender, RoutedEventArgs e)
        {
            var token = ShowInputDialogLarge(
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
        #endregion

        #region App auth settings
        private void Button_EditClientId_Click(object sender, RoutedEventArgs e)
        {
            var clientId = ShowInputDialogLarge(
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
            var secret = ShowInputDialogLarge(
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
        #endregion

        #region Prefix
        private void Button_EditPrefix_Click(object sender, RoutedEventArgs e)
        {
            var prefix = ShowInputDialogSmall(
                p.MessagePrefix,
                "Prefix"
            );
            if(prefix != null)
            {
                p.MessagePrefix = prefix;
                p.Save();
                textBox_Needle.Text = prefix;
            }
        }

        private void CheckBox_PrefixOn_Checked(object sender, RoutedEventArgs e)
        {
            p.MessagePrefixOn = true;
            p.Save();
        }
        private void CheckBox_PrefixOn_Unchecked(object sender, RoutedEventArgs e)
        {
            p.MessagePrefixOn = false;
            p.Save();
        }
        #endregion

        #region Allow
        private void CheckBox_AllowFollower_Checked(object sender, RoutedEventArgs e)
        {
            p.AllowFollower = true;
            p.Save();
        }
        private void CheckBox_AllowFollower_Unchecked(object sender, RoutedEventArgs e)
        {
            p.AllowFollower = false;
            p.Save();
        }

        private void CheckBox_AllowSubscriber_Checked(object sender, RoutedEventArgs e)
        {
            p.AllowSubscriber = true;
            p.Save();
        }
        private void CheckBox_AllowSubscriber_Unchecked(object sender, RoutedEventArgs e)
        {
            p.AllowSubscriber = false;
            p.Save();
        }

        private void CheckBox_AllowModerator_Checked(object sender, RoutedEventArgs e)
        {
            p.AllowModerator = true;
            p.Save();
        }
        private void CheckBox_AllowModerator_Unchecked(object sender, RoutedEventArgs e)
        {
            p.AllowModerator = false;
            p.Save();
        }

        private void CheckBox_AllowVIP_Checked(object sender, RoutedEventArgs e)
        {
            p.AllowVIP = true;
            p.Save();
        }
        private void CheckBox_AllowVIP_Unchecked(object sender, RoutedEventArgs e)
        {
            p.AllowVIP = false;
            p.Save();
        }
        #endregion

        #region Notify
        private void CheckBox_NotifyConnectivity_Checked(object sender, RoutedEventArgs e)
        {
            p.NotifyConnectivity = true;
            p.Save();
        }
        private void CheckBox_NotifyConnectivity_Unchecked(object sender, RoutedEventArgs e)
        {
            p.NotifyConnectivity = false;
            p.Save();
        }

        private void CheckBox_NotifySubscribed_Checked(object sender, RoutedEventArgs e)
        {
            p.NotifySubscribed = true;
            p.Save();
        }
        private void CheckBox_NotifySubscribed_Unchecked(object sender, RoutedEventArgs e)
        {
            p.NotifySubscribed = false;
            p.Save();
        }

        private void CheckBox_NotifyHosted_Checked(object sender, RoutedEventArgs e)
        {
            p.NotifyHosted = true;
            p.Save();
        }
        private void CheckBox_NotifyHosted_Unchecked(object sender, RoutedEventArgs e)
        {
            p.NotifyHosted = false;
            p.Save();
        }

        private void CheckBox_NotifyRaided_Checked(object sender, RoutedEventArgs e)
        {
            p.NotifyRaided = true;
            p.Save();
        }
        private void CheckBox_NotifyRaided_Unchecked(object sender, RoutedEventArgs e)
        {
            p.NotifyRaided = false;
            p.Save();
        }
        #endregion

        #region Ignore
        private void CheckBox_IgnoreBroadcaster_Checked(object sender, RoutedEventArgs e)
        {
            p.IgnoreBroadcaster = true;
            p.Save();
        }
        private void CheckBox_IgnoreBroadcaster_Unchecked(object sender, RoutedEventArgs e)
        {
            p.IgnoreBroadcaster = false;
            p.Save();
        }

        private void CheckBox_IgnoreBots_Checked(object sender, RoutedEventArgs e)
        {
            p.IgnoreBots = true;
            p.Save();
        }
        private void CheckBox_IgnoreBots_Unchecked(object sender, RoutedEventArgs e)
        {
            p.IgnoreBots = false;
            p.Save();
        }

        private void Button_EditIgnoreUsers_Click(object sender, RoutedEventArgs e)
        {
            var ignoreUsers = ShowInputDialogLarge(
                p.IgnoreUsers,
                "Username:",
                "Here you can list users you want to ignore, like bots.\n" +
                "Separate multiple names with commas."
            );
            if (ignoreUsers != null)
            {
                p.IgnoreUsers = ignoreUsers;
                p.Save();
                textBox_IgnoreUsers.Text = ignoreUsers;
                controller.ReloadIgnoredUsers();
            }
        }
        #endregion

        #region Avatar settings
        private void CheckBox_AvatarEnabled_Checked(object sender, RoutedEventArgs e)
        {
            p.AvatarEnabled = true;
            p.Save();
        }
        private void CheckBox_AvatarEnabled_Unchecked(object sender, RoutedEventArgs e)
        {
            p.AvatarEnabled = false;
            p.Save();
        }

        private void CheckBox_AvatarFrameEnabled_Checked(object sender, RoutedEventArgs e)
        {
            p.AvatarFrameEnabled = true;
            p.Save();
        }
        private void CheckBox_AvatarFrameEnabled_Unchecked(object sender, RoutedEventArgs e)
        {
            p.AvatarFrameEnabled = false;
            p.Save();
        }

        private void CheckBox_AvatarBadgesEnabled_Checked(object sender, RoutedEventArgs e)
        {
            p.AvatarBadgesEnabled = true;
            p.Save();
        }
        private void CheckBox_AvatarBadgesEnabled_Unchecked(object sender, RoutedEventArgs e)
        {
            p.AvatarBadgesEnabled = false;
            p.Save();
        }
        #endregion

        #region Test
        private void Button_EditTestUsername_Click(object sender, RoutedEventArgs e)
        {
            var testUsername = ShowInputDialogSmall(
                p.TestUsername,
                "Test username"
            );
            if (testUsername != null)
            {
                p.TestUsername = testUsername;
                p.Save();
                textBox_TestUsername.Text = testUsername;
            }
        }

        private void TextBox_TestMessage_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                p.TestMessage = textBox_TestMessage.Text;
                p.Save();
                controller.BroadcastNotification(p.TestUsername, p.TestMessage);
            }
        }

        private void TextBox_TestMessage_LostFocus(object sender, RoutedEventArgs e)
        {
            p.TestMessage = textBox_TestMessage.Text;
            p.Save();
        }

        private void Button_Test_Click(object sender, RoutedEventArgs e)
        {
            controller.BroadcastNotification(p.TestUsername, p.TestMessage);
        }
        #endregion
    }
}
