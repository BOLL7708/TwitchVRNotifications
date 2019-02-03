using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;
using Valve.VR;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Events;
using BOLL7708;

namespace TwitchVRNotifications
{
    class MainController
    {
        Properties.Settings p = Properties.Settings.Default;
        TwitchClient client;
        EasyOpenVRSingleton VRController = EasyOpenVRSingleton.Instance;
        ulong overlayHandle = 0;
        Dictionary<string, Bitmap> userLogos = new Dictionary<string, Bitmap>();
        public bool OpenVR_Initiated = false;
        private int connectionAttempts = 0;

        public MainController()
        {
            OpenVR_Initiated = initVr(); // Init OpenVR
            if (p.AutoConnectChat) connectChat(); // Connect to chat
        }

        public bool initVr()
        {
            try
            {
                return VRController.Init();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to init VR: " + e.Message);
                return false;
            }
            
        }

        private void onMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            Debug.WriteLine("Message from "+e.ChatMessage.Username+": " + e.ChatMessage.Message);
            string needle = p.Needle;
            if (!p.FilterOn || needle.Length == 0 || e.ChatMessage.Message.IndexOf(needle) == 0)
            {
                Debug.WriteLine("Broadcasting notifiction...");
                string message = e.ChatMessage.DisplayName + ": " + ((p.FilterOn && needle.Length > 0) ? e.ChatMessage.Message.Substring(needle.Length).Trim() : e.ChatMessage.Message.Trim());
                broadcastNotification(e.ChatMessage.Username, message, e.ChatMessage.Color);
            }
        }

        private void onBeingHosted(object sender, OnBeingHostedArgs e)
        {
            var n = e.BeingHostedNotification;
            var host = n.HostedByChannel;
            var viewers = n.Viewers;
            if(viewers > 0)
            {
                broadcastNotification(p.UserName, "Hosted by: " + host + " with " + viewers + " viewers.");
            }
            else
            {
                broadcastNotification(p.UserName, "Hosted by: "+host);
            }
        }

        private void onChannelStateChanged(object sender, OnChannelStateChangedArgs e)
        {
            var message = "Bot: Channel state: " + e.ChannelState.GetType().ToString();
            Debug.WriteLine(message);
            // broadcastNotification(p.UserName, message);
        }

        private void onDisconnected(object sender, OnDisconnectedEventArgs e)
        {
            var message = "Bot: Disconnected, reconnecting...";
            Debug.WriteLine(message);
            broadcastNotification(p.UserName, message);
            if (p.AutoConnectChat) connectChat();
        }

        private void onConnectionError(object sender, OnConnectionErrorArgs e)
        {
            var message = "Bot: Connection Error: "+e.Error.Message;
            Debug.WriteLine(message);
            broadcastNotification(p.UserName, message);
            if (p.AutoConnectChat) connectChat();
        }

        private void onConnected(object sender, OnConnectedArgs e)
        {
            var message = "Bot: Connected";
            Debug.WriteLine(message);
            broadcastNotification(p.UserName, message);
            connectionAttempts = 0;
        }

        public void broadcastNotification(string username, string message)
        {
            broadcastNotification(username, message, Color.Purple);
        }

        public void broadcastNotification(string username, string message, Color color)
        {
            if(!VRController.IsInitialized()) {
                if (!VRController.Init())
                {
                    Debug.WriteLine("VR controller is not running...");
                    return;
                }
            }

            // Fix because the color coming out of TwitchLib appears broken?!
            // My issue: https://github.com/TwitchLib/TwitchLib/issues/438
            if (!color.IsKnownColor)
            {
                var colorDec = color.ToArgb();
                var colorHex = colorDec.ToString("X");
                if(colorHex.Length == 7)
                {
                    colorHex = "ff"+colorHex.Substring(0, colorHex.Length-1);
                    colorDec = int.Parse(colorHex, System.Globalization.NumberStyles.HexNumber);
                    color = Color.FromArgb(colorDec);
                }
            }

            string b64name = Base64Encode(username);
            /*
             * If we have a cached bitmap we can skip the rest
             */
            if (userLogos.ContainsKey(b64name))
            {
                Debug.WriteLine("User cached, broadcasting cached.");
                Bitmap bmp;
                if (userLogos.TryGetValue(b64name, out bmp))
                {
                    NotificationBitmap_t icon = iconFromBitmapData(bitmapDataFromBitmap(bmp));
                    broadcastNotification(message, icon);
                    return;
                }
            }

            RGBtoBGR(ref color); // Fix color
            /*
             * Will skip web requests if no Kraken access
             */
            if(p.ClientID.Length == 0)
            {
                Debug.WriteLine("No API access, broadcasting without user portrait.");
                broadcastNotification(message, iconFromBitmapData(generatePlaceholderBitmapData(color, username, b64name)));
                return;
            }

            /* 
             * Load user data from Kraken
             * Load user icon if available, else placeholder
             * If icon draw border
             * Display notification
             */
            try
            {
                WebRequest request = WebRequest.Create("https://api.twitch.tv/kraken/channels/" + username);
                request.Headers.Add("Client-ID: " + Utils.DecryptStringFromBase64(p.ClientID, p.Entropy));
                using (var response = request.GetResponse())
                using (var stream = response.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(stream);
                    string json = reader.ReadToEnd();
                    stream.Close();

                    var jsonObj = new JavaScriptSerializer().Deserialize<dynamic>(json);
                    string logoUrl = jsonObj["logo"];

                    // Load image

                    // SSL issue: https://stackoverflow.com/a/2904963
                    ServicePointManager.Expect100Continue = true;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    // Use SecurityProtocolType.Ssl3 if needed for compatibility reasons

                    WebRequest imgRequest = WebRequest.Create(logoUrl); // Will break if no url
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
                        Pen pen = new Pen(color, 32f);
                        gfx.DrawRectangle(pen, rect); // Outline
                        gfx.Flush();
                        userLogos.Add(b64name, bmpEdit); // Cache
                        BitmapData TextureData = bitmapDataFromBitmap(bmpEdit); // Allocate
                        Debug.WriteLine("Bitmap acquisition successful, broadcasting.");
                        broadcastNotification(message, iconFromBitmapData(TextureData)); // Submit
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error broadcasting notification: "+e.Message);
                broadcastNotification(message, iconFromBitmapData(generatePlaceholderBitmapData(color, username, b64name)));
                return;
            }
        }

        private BitmapData generatePlaceholderBitmapData(Color color, String username, String b64name)
        {
            Bitmap bmp = new Bitmap(300, 300);
            Rectangle rect = new Rectangle(Point.Empty, bmp.Size);

            Graphics gfx = Graphics.FromImage(bmp);
            gfx.Clear(color); // Background

            if (username.Length > 0)
            {
                string letter = username.Substring(0, 1).ToUpper();
                gfx.SmoothingMode = SmoothingMode.AntiAlias;
                gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
                gfx.PixelOffsetMode = PixelOffsetMode.HighQuality;
                gfx.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                gfx.DrawString(letter, new Font("Tahoma", 150), Brushes.White, rect, sf);
                gfx.Flush();
            }
            if(!userLogos.ContainsKey(b64name)) userLogos.Add(b64name, bmp); // Cache
            return bitmapDataFromBitmap(bmp);
        }

        private void broadcastNotification(string message, NotificationBitmap_t icon)
        {
            if(overlayHandle == 0)
            {
                overlayHandle = VRController.InitNotificationOverlay("Twitch Chat");
            }

            if(OpenVR_Initiated)
            {
                VRController.EnqueueNotification(overlayHandle, message, icon);
            }
        }

        private BitmapData bitmapDataFromBitmap(Bitmap bmpIn)
        {
            // https://github.com/artumino/SteamVR_HUDCenter/blob/a8e306ba9c6fbe0e9834cd8d49365df42b06fa2e/VRTestApplication/TestForm.cs#L59-L70

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

        public void connectChat()
        {
            if (isChatConnected()) { client.Disconnect(); client = null; }
            ConnectionCredentials credentials = new ConnectionCredentials(p.UserName, Utils.DecryptStringFromBase64(p.AuthToken, p.Entropy));
            client = new TwitchClient();
            client.Initialize(credentials, p.UserName);
            client.OnMessageReceived += onMessageReceived;
            client.OnChannelStateChanged += onChannelStateChanged;
            client.OnDisconnected += onDisconnected;
            client.OnConnectionError += onConnectionError;
            client.OnConnected += onConnected;
            client.OnBeingHosted += onBeingHosted;
            connectionAttempts++;
            client.Connect();
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
