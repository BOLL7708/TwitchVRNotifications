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
        long notificationCounter = 0;

        public MainWindow()
        {
            InitializeComponent();

            textBox_UserName.Text = p.UserName;
            textBox_AuthToken.Text = p.AuthToken;
            textBox_Needle.Text = p.Needle;

            VRController.Init();
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("We clicked ze bytton.");
            broadcastNotification("Test Notification", "This is a test.");
        }

        private void button_Save_Click(object sender, RoutedEventArgs e)
        {
            p.UserName = textBox_UserName.Text;
            p.AuthToken = textBox_AuthToken.Text;
            p.Needle = textBox_Needle.Text;
            p.Save();
        }

        private void button_Connect_Click(object sender, RoutedEventArgs e)
        {
            if (client != null && client.IsConnected) { client.Disconnect(); client = null; }
            ConnectionCredentials credentials = new ConnectionCredentials(p.UserName, p.AuthToken);
            client = new TwitchClient(credentials, p.UserName);
            client.OnMessageReceived += onMessageReceived;
            client.Connect();
            Debug.WriteLine("Are we connected? : " + client.IsConnected.ToString());
        }

        private void onMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            Debug.WriteLine(e.ChatMessage.Username+": "+e.ChatMessage.Message);
            String needle = p.Needle;
            if(needle.Length == 0 || e.ChatMessage.Message.Contains(needle))
            {
                String title = e.ChatMessage.DisplayName+" says...";
                String message = e.ChatMessage.Message.Replace(needle, "");
                Debug.WriteLine("VR Message received: " + message);
                broadcastNotification(title, message);
            }
        }

        private void broadcastNotification(String title, String message)
        {
            notificationCounter++;
            // String UUID = Guid.NewGuid().ToString();
            Overlay overlay = new Overlay(title + " (" + notificationCounter + ")", 0);
            DrawingImage image = new DrawingImage();
            NotificationBitmap_t bitmap = new NotificationBitmap_t();
            VRController.RegisterNewItem(overlay);
            VRController.DisplayNotification(message, overlay, EVRNotificationType.Transient, EVRNotificationStyle.Application, bitmap);
        }
    }
}
