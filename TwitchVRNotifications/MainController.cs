﻿using SteamVR_HUDCenter;
using SteamVR_HUDCenter.Elements;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            Debug.WriteLine("Initiating main controller.");
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
            Debug.WriteLine("Message received: " + e.ChatMessage.Message);
            string needle = p.Needle;
            if (needle.Length == 0 || e.ChatMessage.Message.IndexOf(needle) == 0)
            {
                string message = e.ChatMessage.DisplayName + ": " + (needle.Length > 0 ? e.ChatMessage.Message.Substring(needle.Length).Trim() : e.ChatMessage.Message.Trim());
                broadcastNotification(e.ChatMessage.Username, message);
            }
        }

        private void broadcastNotification(string message, NotificationBitmap_t icon)
        {
            VRController.DisplayNotification(message, overlay, EVRNotificationType.Transient, EVRNotificationStyle.Application, icon);
        }

        public void broadcastNotification(string username, string message)
        {
            if (userLogos.ContainsKey(username))
            {
                Bitmap bmp;
                if (userLogos.TryGetValue(username, out bmp))
                {
                    Debug.WriteLine("Bitmap from cached.");
                    NotificationBitmap_t icon = iconFromBitmapData(bitmapDataFromBitmap(bmp));
                    broadcastNotification(message, icon);
                    return;
                }
            }

            Debug.WriteLine("Loading bitmap.");
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
                if (logoUrl == null) logoUrl = p.PlaceholderLogo;

                Debug.WriteLine(logoUrl);

                // IMAGE              
                WebRequest imgRequest = WebRequest.Create(logoUrl);
                using (var imgResponse = imgRequest.GetResponse())
                using (var imgStream = imgResponse.GetResponseStream())
                {
                    Bitmap notification_bitmap = new Bitmap(imgStream);
                    RGBtoBGR(notification_bitmap); // Fix colors
                    userLogos.Add(username, notification_bitmap); // Cache
                    BitmapData TextureData = bitmapDataFromBitmap(notification_bitmap); // Allocate
                    broadcastNotification(message, iconFromBitmapData(TextureData)); // Submit
                }

            }
        }

        private BitmapData bitmapDataFromBitmap(Bitmap bmpIn)
        {
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
    }
}
