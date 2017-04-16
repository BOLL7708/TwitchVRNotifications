using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Valve.VR;
using TwitchLib;
using TwitchLib.Models.Client;
using TwitchLib.Events.Client;
using SteamVR_HUDCenter;
using SteamVR_HUDCenter.Elements;
using System.Net;
using System.IO;
using System.Web.Script.Serialization;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using TwitchVRNotifications.Properties;
using System.Collections.Generic;

namespace TwitchVRNotifications
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Properties.Settings p = Properties.Settings.Default;
        TwitchClient client;
        HUDCenterController VRController = new HUDCenterController();
        Overlay overlay;
        Dictionary<string, NotificationBitmap_t> userLogos = new Dictionary<string, NotificationBitmap_t>();
        // TODO: Cache user images here.

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

            // Init overlay
            VRController.Init();
            overlay = new Overlay("Twitch Chat", 0);
            VRController.RegisterNewItem(overlay);

            // Connect to chat
            if (p.AutoConnectChat) connectChat();
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            loadImageFromWeb("woboloko", "this is at test");
            // broadcastNotification("This is a test.", new NotificationBitmap_t());
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
            bool result = connectChat();
            Debug.WriteLine("Are we connected? : " + result.ToString());
        }

        private bool connectChat()
        {
            if (client != null && client.IsConnected) { client.Disconnect(); client = null; }
            ConnectionCredentials credentials = new ConnectionCredentials(p.UserName, p.AuthToken);
            client = new TwitchClient(credentials, p.UserName);
            client.OnMessageReceived += onMessageReceived;
            client.Connect();
            return client.IsConnected;
        }

        private void onMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            string needle = p.Needle;
            if(needle.Length == 0 || e.ChatMessage.Message.Contains(needle))
            {
                string message = e.ChatMessage.DisplayName + ": " + (needle.Length > 0 ? e.ChatMessage.Message.Replace(needle, "") : e.ChatMessage.Message);
                Debug.WriteLine(message+", "+e.ChatMessage.ColorHex);
                // broadcastNotification(message, new NotificationBitmap_t());
                loadImageFromWeb(e.ChatMessage.Username, message);
            }
        }

        private void broadcastNotification(string message, NotificationBitmap_t icon)
        {           
            VRController.DisplayNotification(message, overlay, EVRNotificationType.Transient, EVRNotificationStyle.Application, icon);
        }

        private void loadImageFromWeb(string username, string message)
        {
            WebRequest  request = WebRequest.Create("https://api.twitch.tv/kraken/channels/"+username);
                        request.Headers.Add("Client-ID: "+p.ClientID);
            using (var response = request.GetResponse())
            using (var stream = response.GetResponseStream())
            {
                StreamReader reader = new StreamReader(stream);
                string json = reader.ReadToEnd();
                stream.Close();

                var jsonObj = new JavaScriptSerializer().Deserialize<dynamic>(json);
                String logoUrl = jsonObj["logo"];
                if (logoUrl == null) logoUrl = p.PlaceholderLogo; // "D:\\Google Drive\\-= BOLL7708 =-\\-= WWW Root =-\\twitch_chat\\twitch.jpg"; // "http://localhost/boll/twitch_chat/twitch.jpg";

                Debug.WriteLine(logoUrl);

                // IMAGE              
                WebRequest imgRequest = WebRequest.Create(logoUrl); // TODO: Load default image here.
                using (var imgResponse = imgRequest.GetResponse())
                using (var imgStream = imgResponse.GetResponseStream())
                {
                    Bitmap notification_bitmap = new Bitmap(imgStream); // new Bitmap(@"D:\Dropbox\BOLL_Vive_150px.jpg");

                    // TODO: Use transparent logo and user color to make a custom Twitch logo? Maybe? Or write name in logo?

                    NotificationBitmap_t notification_icon = new NotificationBitmap_t();

                    System.Drawing.Imaging.BitmapData TextureData = notification_bitmap.LockBits(
                            new Rectangle(0, 0, notification_bitmap.Width, notification_bitmap.Height),
                            System.Drawing.Imaging.ImageLockMode.ReadOnly,
                            System.Drawing.Imaging.PixelFormat.Format32bppRgb
                        );


                    notification_icon.m_pImageData = TextureData.Scan0;
                    notification_icon.m_nWidth = TextureData.Width;
                    notification_icon.m_nHeight = TextureData.Height;
                    notification_icon.m_nBytesPerPixel = 4;

                    broadcastNotification(message, notification_icon);
                }
                
            }
        }

        private void button_Browse_Click(object sender, RoutedEventArgs e)
        {
            // http://stackoverflow.com/a/10315283

            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".jpg";
            dlg.Filter = "JPG Files (*.jpg)|*.jpg|JPEG Files (*.jpeg)|*.jpeg";
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                string filename = dlg.FileName;
                textBox_PlaceholderLogo.Text = filename;
            }
        }
    }
}
