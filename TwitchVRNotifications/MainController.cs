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

namespace TwitchVRNotifications
{
    class MainController
    {
        Properties.Settings p = Properties.Settings.Default;
        TwitchClient client;
        HUDCenterController VRController = new HUDCenterController();
        Overlay overlay;
        Dictionary<string, Bitmap> userLogos = new Dictionary<string, Bitmap>();

        public MainController()
        {
            initVr();
            overlay = new Overlay("Twitch Chat", 0);
            VRController.RegisterNewItem(overlay);
        }

        public void initVr()
        {
            VRController.Init(EVRApplicationType.VRApplication_Background);
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
            broadcastNotification(username, message, Color.Purple);
        }

        public void broadcastNotification(string username, string message, Color color)
        {
            String b64name = Base64Encode(username);
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

            WebRequest request = WebRequest.Create("https://api.twitch.tv/kraken/channels/" + username);
            request.Headers.Add("Client-ID: " + p.ClientID);
            using (var response = request.GetResponse())
            using (var stream = response.GetResponseStream())
            {
                StreamReader reader = new StreamReader(stream);
                string json = reader.ReadToEnd();
                stream.Close();

                var jsonObj = new JavaScriptSerializer().Deserialize<dynamic>(json);
                String logoUrl = jsonObj["logo"];
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
                    RGBtoBGR(ref color); // Fix color
                    Rectangle rect = new Rectangle(Point.Empty, bmp.Size);
                    gfx.Clear(color); // Background
                    gfx.DrawImageUnscaledAndClipped(bmp, rect);
                    if (userHasLogo) // Outline
                    {
                        Pen pen = new Pen(color, 32f);
                        gfx.DrawRectangle(pen, rect);
                    }
                    userLogos.Add(Base64Encode(b64name), bmpEdit); // Cache
                    BitmapData TextureData = bitmapDataFromBitmap(bmpEdit); // Allocate
                    broadcastNotification(message, iconFromBitmapData(TextureData)); // Submit
                }

            }
        }

        private void broadcastNotification(string message, NotificationBitmap_t icon)
        {
            VRController.DisplayNotification(message, overlay, EVRNotificationType.Transient, EVRNotificationStyle.Application, icon);
        }

        private BitmapData bitmapDataFromBitmap(Bitmap bmpIn)
        {
            Bitmap bmp = (Bitmap)bmpIn.Clone();
            BitmapData texData = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb
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

        private void RGBtoBGR(Bitmap bmp)
        {
            // http://stackoverflow.com/a/19189660

            BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);
            int length = Math.Abs(data.Stride) * bmp.Height;
            unsafe
            {
                byte* rgbValues = (byte*)data.Scan0.ToPointer();
                for (int i = 0; i < length; i += 3)
                {
                    byte dummy = rgbValues[i];
                    rgbValues[i] = rgbValues[i + 2];
                    rgbValues[i + 2] = dummy;
                }
            }
            bmp.UnlockBits(data);
        }

        private void RGBtoBGR(ref Color color)
        {
            int argb = color.ToArgb();
            byte[] bytes = BitConverter.GetBytes(argb);
            byte a = bytes[0];
            byte b = bytes[2];
            bytes[0] = b;
            bytes[2] = a;
            argb = BitConverter.ToInt32(bytes, 0);
            color = Color.FromArgb(argb);
        }

        public static string Base64Encode(string plainText)
        {
            // http://stackoverflow.com/a/11743162

            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }
    }
}
