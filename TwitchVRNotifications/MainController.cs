using SteamVR_HUDCenter;
using SteamVR_HUDCenter.Elements;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;
using TwitchLib;
using TwitchLib.Events.Client;
using TwitchLib.Models.Client;
using Valve.VR;
using System.Diagnostics;
using System.Text;

namespace TwitchVRNotifications
{
    class MainController
    {
        Properties.Settings p = Properties.Settings.Default;
        TwitchClient client;
        HUDCenterController VRController = new HUDCenterController();
        Overlay overlay;
        Dictionary<string, Bitmap> userLogos = new Dictionary<string, Bitmap>();
        public bool OpenVR_Initiated = false;

        public MainController()
        {
            OpenVR_Initiated = initVr(); // Init OpenVR
            if (p.AutoConnectChat) connectChat(); // Connect to chat
        }

        public bool initVr()
        {
            try
            {
                VRController.Init(EVRApplicationType.VRApplication_Background);
                overlay = new Overlay("Twitch Chat", 0);
                VRController.RegisterNewItem(overlay);
                return true;
            } catch (Exception e)
            {
                return false;
            }
            
        }

        private void onMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            string needle = p.Needle;
            if (needle.Length == 0 || e.ChatMessage.Message.IndexOf(needle) == 0)
            {
                string message = e.ChatMessage.DisplayName + ": " + (needle.Length > 0 ? e.ChatMessage.Message.Substring(needle.Length).Trim() : e.ChatMessage.Message.Trim());
                broadcastNotification(e.ChatMessage.Username, message, e.ChatMessage.Color);
            }
        }

        public void broadcastNotification(string username, string message)
        {
            broadcastNotification(username, message, System.Drawing.Color.Purple);
        }

        public void broadcastNotification(string username, string message, System.Drawing.Color color)
        {
            string b64name = Base64Encode(username);
            if (userLogos.ContainsKey(b64name))
            {
                Bitmap bmp;
                if (userLogos.TryGetValue(b64name, out bmp))
                {
                    NotificationBitmap_t icon = iconFromBitmapData(bitmapDataFromBitmap(bmp));
                    broadcastNotification(message, icon);
                    return;
                }
            }

            RGBtoBGR(ref color); // Fix color
            if(p.ClientID.Length == 0 || p.PlaceholderLogo.Length == 0)
            {
                
                var bmp = new Bitmap(1, 1);
                bmp.SetPixel(0, 0, color);              
                BitmapData TextureData = bitmapDataFromBitmap(bmp); // Allocate
                broadcastNotification(message, iconFromBitmapData(TextureData)); // Submit
                return;
            }

            WebRequest request = WebRequest.Create("https://api.twitch.tv/kraken/channels/" + username);
            request.Headers.Add("Client-ID: " + p.ClientID);
            using (var response = request.GetResponse())
            using (var stream = response.GetResponseStream())
            {
                StreamReader reader = new StreamReader(stream);
                string json = reader.ReadToEnd();
                stream.Close();

                var jsonObj = new JavaScriptSerializer().Deserialize<dynamic>(json);
                string logoUrl = jsonObj["logo"];
                bool userHasLogo = logoUrl != null;
                if (!userHasLogo) logoUrl = p.PlaceholderLogo;

                // Load image
                WebRequest imgRequest = WebRequest.Create(logoUrl);
                using (var imgResponse = imgRequest.GetResponse())
                using (var imgStream = imgResponse.GetResponseStream())
                {
                    Bitmap bmp = new Bitmap(imgStream);
                    RGBtoBGR(bmp); // Fix color

                    // http://stackoverflow.com/a/27318979

                    Bitmap bmpEdit = new Bitmap(bmp.Width, bmp.Height);
                    Graphics gfx = Graphics.FromImage(bmpEdit);
                    Rectangle rect = new Rectangle(Point.Empty, bmp.Size);
                    gfx.Clear(color); // Background
                    gfx.DrawImageUnscaledAndClipped(bmp, rect);
                    System.Drawing.Pen pen = new System.Drawing.Pen(color, 32f);
                    if (userHasLogo) gfx.DrawRectangle(pen, rect); // Outline
                    userLogos.Add(b64name, bmpEdit); // Cache
                    BitmapData TextureData = bitmapDataFromBitmap(bmpEdit); // Allocate
                    broadcastNotification(message, iconFromBitmapData(TextureData)); // Submit
                }

            }
        }

        private void broadcastNotification(string message, NotificationBitmap_t icon)
        {
            // http://stackoverflow.com/a/14057684

            byte[] bytes = Encoding.Default.GetBytes(message);
            message = Encoding.UTF8.GetString(bytes); // Still does not fix ÅÄÖ turning to ???
            if(OpenVR_Initiated)
            {
                VRController.DisplayNotification(message, overlay, EVRNotificationType.Transient, EVRNotificationStyle.Application, icon);
            }
        }

        private BitmapData bitmapDataFromBitmap(Bitmap bmpIn)
        {
            // https://github.com/artumino/SteamVR_HUDCenter/blob/a8e306ba9c6fbe0e9834cd8d49365df42b06fa2e/VRTestApplication/TestForm.cs#L59-L70

            Bitmap bmp = (Bitmap)bmpIn.Clone();
            BitmapData texData = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb
            );
            return texData;
        }

        private NotificationBitmap_t iconFromBitmapData(BitmapData TextureData)
        {
            NotificationBitmap_t notification_icon = new NotificationBitmap_t();
            notification_icon.m_pImageData = TextureData.Scan0;
            notification_icon.m_nWidth = TextureData.Width;
            notification_icon.m_nHeight = TextureData.Height;
            notification_icon.m_nBytesPerPixel = 4;
            return notification_icon;
        }

        public bool connectChat()
        {
            if (client != null && client.IsConnected) { client.Disconnect(); client = null; }
            ConnectionCredentials credentials = new ConnectionCredentials(p.UserName, p.AuthToken);
            client = new TwitchClient(credentials, p.UserName);
            client.OnMessageReceived += onMessageReceived;
            client.Connect();
            return client.IsConnected;
        }

        public bool isChatConnected()
        {
            return client != null && client.IsConnected;
        }

        private void RGBtoBGR(Bitmap bmp)
        {
            // based on http://stackoverflow.com/a/19189660

            int bytesPerPixel = Bitmap.GetPixelFormatSize(bmp.PixelFormat)/8;
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);
            int length = Math.Abs(data.Stride) * bmp.Height;
            unsafe
            {
                byte* rgbValues = (byte*)data.Scan0.ToPointer();
                for (int i = 0; i < length; i += bytesPerPixel)
                {
                    byte dummy = rgbValues[i];
                    rgbValues[i] = rgbValues[i + 2];
                    rgbValues[i + 2] = dummy;
                }
            }
            bmp.UnlockBits(data);            
        }

        private void RGBtoBGR(ref System.Drawing.Color color)
        {
            int argb = color.ToArgb();
            byte[] bytes = BitConverter.GetBytes(argb);
            byte a = bytes[0];
            byte b = bytes[2];
            bytes[0] = b;
            bytes[2] = a;
            argb = BitConverter.ToInt32(bytes, 0);
            color = System.Drawing.Color.FromArgb(argb);
        }

        public static string Base64Encode(string plainText)
        {
            // http://stackoverflow.com/a/11743162

            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }
    }
}
