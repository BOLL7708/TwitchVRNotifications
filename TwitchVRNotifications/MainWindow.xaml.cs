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
            textBox_ClientID.Text = p.ClientID;
            VRController.Init();
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("We clicked ze bytton.");
            broadcastNotification("Test Notification", "This is a test.", null);
        }

        private void button_Save_Click(object sender, RoutedEventArgs e)
        {
            p.UserName = textBox_UserName.Text;
            p.AuthToken = textBox_AuthToken.Text;
            p.Needle = textBox_Needle.Text;
            p.ClientID = textBox_ClientID.Text;
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
            // loadImageFromWeb(e.ChatMessage.Username); // TODO: This is not working yet, skip it.
            Debug.WriteLine(e.ChatMessage.Username+": "+e.ChatMessage.Message);
            String needle = p.Needle;
            if(needle.Length == 0 || e.ChatMessage.Message.Contains(needle))
            {
                String title = e.ChatMessage.DisplayName+" says...";
                String message = needle.Length > 0 ? e.ChatMessage.Message.Replace(needle, "") : e.ChatMessage.Message;
                Debug.WriteLine("Message received: " + title + " - " + message);
                broadcastNotification(title, message, null);
            }
        }

        private void broadcastNotification(String title, String message, Bitmap bmp)
        {
            // IntPtr ptr = Marshal.AllocHGlobal(bmp.leng);
            GCHandle handle1 = GCHandle.Alloc(bmp);
            IntPtr ptr = (IntPtr)handle1;
            notificationCounter++;
            Overlay overlay = new Overlay(title + " (" + notificationCounter + ")", 0);
            NotificationBitmap_t bitmap = new NotificationBitmap_t();
            if(bmp != null)
            {
                bitmap.m_nBytesPerPixel = 24;
                bitmap.m_nHeight = 50;
                bitmap.m_nWidth = 50;
                bitmap.m_pImageData = ptr;
            }
            VRController.RegisterNewItem(overlay);
            VRController.DisplayNotification(message, overlay, EVRNotificationType.Transient, EVRNotificationStyle.Application, bitmap);
        }

        private void loadImageFromWeb(String username)
        {
            WebRequest request = WebRequest.Create("https://api.twitch.tv/kraken/channels/"+username);
            request.Headers.Add("Client-ID: "+p.ClientID);
            using (var response = request.GetResponse())
            using (var stream = response.GetResponseStream())
            {
                StreamReader reader = new StreamReader(stream);
                string json = reader.ReadToEnd();

                JavaScriptSerializer js = new JavaScriptSerializer();
                var obj = js.Deserialize<dynamic>(json);
                String logoUrl = obj["logo"];

                Debug.WriteLine(logoUrl);

                // IMAGE
                var imgRequest = WebRequest.Create(logoUrl.Replace("300x300", "50x50"));
                Debug.WriteLine(imgRequest.RequestUri);
                using (var imgResponse = request.GetResponse())
                using (var imgStream = imgResponse.GetResponseStream())
                {
                    // Image img = Image.FromStream(imgStream);
                    // Debug.WriteLine("IMG Type: " + img.GetType());
                    // Bitmap bmp = new Bitmap(imgStream);
                    Bitmap bmp = new Bitmap("C:\\Temp\\boll7708.jpeg"); // TODO: This seems to work, or something, but not loading from the web, what's up with that? Missing header?
                    // broadcastNotification("ImageTest", "Testing to load an image yo...", bmp);
                }


            }
            /*
            using (var stream = response.GetResponseStream())
            {
                Bitmap.FromStream(stream);
            }
            */
        }
    }
}
