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
using System.Threading;
using TwitchLib.Api;
using System.Threading.Tasks;
using System.Windows;

namespace TwitchVRNotifications
{
    class MainController
    {
        Properties.Settings p = Properties.Settings.Default;
        TwitchClient client = new TwitchClient();
        TwitchAPI api = new TwitchAPI();
        EasyOpenVRSingleton ovr = EasyOpenVRSingleton.Instance;
        ulong overlayHandle = 0;
        Dictionary<string, Bitmap> userLogos = new Dictionary<string, Bitmap>();
        public bool OpenVR_Initiated = false;
        private int connectionAttempts = 0;
        private bool isConnectingToChat = false;
        private Thread ovrThread;
        private Thread chatThread;
        private String[] ignoredUsers = new string[0];

        public Action<bool, string, string> openVRStatusEvent;
        public Action<bool, string, string> chatBotStatusEvent;

        public MainController()
        {
            ovrThread = new Thread(OpenVRWorker);
            if (!ovrThread.IsAlive) ovrThread.Start();
            chatThread = new Thread(ChatWorker);
            if (!chatThread.IsAlive) chatThread.Start();
            ReloadIgnoredUsers();

            var task = HelixFollows("","");
            var task2 = HelixUsers("");
            var task3 = Refresh();
        }

        #region TwitchLib
        private void ChatWorker()
        {
            Thread.CurrentThread.IsBackground = true;
            while (true)
            {
                if(!IsChatConnected() && !isConnectingToChat)
                {
                    Debug.WriteLine("Connecting to chat");
                    ConnectChat();
                }
                Thread.Sleep(5000+1000*connectionAttempts*5);
            }
        }

        private void ChatStatus(bool status, string label, string toolTip)
        {
            Application.Current.Dispatcher.Invoke(() =>
                {
                    chatBotStatusEvent?.Invoke(status, label, toolTip);
                }
            );
        }

        public void ConnectChat()
        {
            if (IsChatConnected()) { client.Disconnect(); client = null; }
            if (p.BotChannel.Length == 0) {
                ChatStatus(false, "Bot channel not set.", "Please set a channel to connect to.");
                return;
            }
            if (p.BotUsername.Length == 0) {
                ChatStatus(false, "Bot username not set.", "Please set a username for the chat bot.");
                return;
            }
            if (p.BotChatToken.Length == 0)
            {
                ChatStatus(false, "Chat token not set.", "Please acquire an OAuth chat token to connect to chat.");
                return;
            }
            isConnectingToChat = true;
            ConnectionCredentials credentials = new ConnectionCredentials(p.BotUsername, Utils.DecryptStringFromBase64(p.BotChatToken, p.Entropy));
            if (client == null) client = new TwitchClient();
            client.Initialize(credentials, p.BotChannel);
            client.OnMessageReceived += OnMessageReceived;
            client.OnChannelStateChanged += OnChannelStateChanged;
            client.OnDisconnected += OnDisconnected;
            client.OnConnectionError += OnConnectionError;
            client.OnConnected += OnConnected;
            client.OnBeingHosted += OnBeingHosted;
            client.OnReSubscriber += OnReSubscriber;
            client.OnNewSubscriber += OnNewSubscriber;
            client.OnGiftedSubscription += OnGiftedSubscription;
            client.OnRaidNotification += OnRaidNotification;
            connectionAttempts++;
            try
            {
                client.Connect();
            }
            catch (Exception e)
            {
                isConnectingToChat = false;
                Debug.WriteLine($"Error connecting to chat: {e.Message}");
            }
        }

        // WIP
        private async Task Refresh()
        {
            api.Settings.ClientId = Utils.DecryptStringFromBase64(p.AppClientId, p.Entropy);
            api.Settings.Secret = Utils.DecryptStringFromBase64(p.AppSecret, p.Entropy);
            if (p.AccessTokenExpiration < DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 45 * 24 * 60 * 60) // Access token usually lasts 60 days, refresh a bit earlier.
            {
                var at = api.Undocumented.GetAccessToken();
                p.AccessToken = Utils.EncryptStringToBase64(at, p.Entropy);
                p.Save();
                Debug.WriteLine("Fetched new access token.");
            }
        }

        // WIP
        private async Task HelixFollows(string user1, string user2)
        {
            api.Settings.AccessToken = Utils.DecryptStringFromBase64(p.AccessToken, p.Entropy);
            TwitchLib.Api.Helix.Models.Users.GetUsersFollowsResponse follows = await api.Helix.Users.GetUsersFollowsAsync(null, null, 20, user1, user2);
            Debug.WriteLine($"Total: {follows.TotalFollows}");
        }

        // WIP
        private async Task HelixUsers(string userId)
        {
            api.Settings.AccessToken = Utils.DecryptStringFromBase64(p.AccessToken, p.Entropy);
            TwitchLib.Api.Helix.Models.Users.GetUsersResponse users = await api.Helix.Users.GetUsersAsync(new List<string> { userId });
            Debug.WriteLine($"Avatar URI: {users.Users[0].ProfileImageUrl}");
        }

        public bool IsChatConnected()
        {
            return client != null && client.IsConnected;
        }

        public void ReloadIgnoredUsers()
        {
            ignoredUsers = p.IgnoreUsers.Split(',');
            for(var i=0; i<ignoredUsers.Length; i++)
            {
                ignoredUsers[i] = ignoredUsers[i].Trim().ToLower();
            }
        }

        private void OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            Debug.WriteLine("Message from "+e.ChatMessage.Username+": " + e.ChatMessage.Message);
            string prefix = p.MessagePrefix;
            if (p.MessagePrefixOn && prefix.Length > 0 && e.ChatMessage.Message.IndexOf(prefix) != 0) return;
            if (p.IgnoreBroadcaster && e.ChatMessage.IsBroadcaster) return;
            if (ignoredUsers.Length > 0 && Array.Exists(ignoredUsers, s => s.Equals(e.ChatMessage.Username.ToLower())))
            {
                Debug.WriteLine($"Users {e.ChatMessage.DisplayName} existed in ignore list and was ignored.");
                return;
            }

            var limitedAccess = p.AllowFollower || p.AllowSubscriber || p.AllowModerator || p.AllowVIP;
            if(limitedAccess)
            {
                if (p.AllowFollower && !true) return; // TODO: Need to do API request here.
                if (p.AllowSubscriber && !e.ChatMessage.IsSubscriber) return;
                if (p.AllowModerator && !e.ChatMessage.IsModerator) return;
                if (p.AllowVIP && !true) return; // TODO: Need to look through badges here.
            }

            // if (e.ChatMessage.Badges.Find())
            Debug.WriteLine("Broadcasting notifiction...");
            Debug.WriteLine(e.ChatMessage.RawIrcMessage);
            var sub = e.ChatMessage.IsSubscriber;
            var mod = e.ChatMessage.IsModerator;
            string message = e.ChatMessage.DisplayName + ": " + ((p.MessagePrefixOn && prefix.Length > 0) ? e.ChatMessage.Message.Substring(prefix.Length).Trim() : e.ChatMessage.Message.Trim());
            BroadcastNotification(e.ChatMessage.Username, message, e.ChatMessage.Color);
        }

        private void OnBeingHosted(object sender, OnBeingHostedArgs e)
        {
            var n = e.BeingHostedNotification;
            var host = n.HostedByChannel;
            var viewers = n.Viewers;
            var message = $"Hosted by: {host} with {viewers} viewers.";
            Debug.WriteLine(message);
            if (!p.NotifyHosted) return;
            if (viewers > 0) BroadcastNotification(p.BotUsername, message);
            else BroadcastNotification(p.BotUsername, "Hosted by: "+host);
        }

        private void OnChannelStateChanged(object sender, OnChannelStateChangedArgs e)
        {
            var message = "Bot: Channel state: " + e.ChannelState.GetType().ToString();
            Debug.WriteLine(message);
            // broadcastNotification(p.UserName, message);
        }

        private void OnDisconnected(object sender, OnDisconnectedEventArgs e)
        {
            isConnectingToChat = false;
            var message = "Bot: Disconnected, reconnecting...";
            Debug.WriteLine(message);
            if(p.NotifyConnectivity) BroadcastNotification(p.BotUsername, message);
            Application.Current.Dispatcher.Invoke(() => {
                chatBotStatusEvent?.Invoke(false, "Bot disconnected!", "The chat bot was disconnected from the server, will reconnect soon.");
            });
        }

        private void OnConnectionError(object sender, OnConnectionErrorArgs e)
        {
            isConnectingToChat = false;
            var message = "Bot: Connection Error: "+e.Error.Message;
            Debug.WriteLine(message);
            if(p.NotifyConnectivity) BroadcastNotification(p.BotUsername, message);
            Application.Current.Dispatcher.Invoke(() => {
                chatBotStatusEvent?.Invoke(false, $"Bot connection error:\n{e.Error.Message}", "The chat bot was unable to connect to the server, will retry.");
            });
        }

        private void OnConnected(object sender, OnConnectedArgs e)
        {
            isConnectingToChat = false;
            var message = "Bot: Connected";
            Debug.WriteLine(message);
            if(p.NotifyConnectivity) BroadcastNotification(p.BotUsername, message);
            Application.Current.Dispatcher.Invoke(() => {
                chatBotStatusEvent?.Invoke(true, "Bot connected to chat", "The chat bot is connected to the server and listening for messages.");
            });
            connectionAttempts = 0;
        }

        public void OnReSubscriber(object sender, OnReSubscriberArgs e)
        {
            var sysMessage = $"{e.ReSubscriber.DisplayName} resubscribed for {e.ReSubscriber.Months} month(s) using {e.ReSubscriber.SubscriptionPlanName}!";
            var subMessage = $"{e.ReSubscriber.DisplayName} SubMsg: {e.ReSubscriber.ResubMessage.Trim()}";
            Debug.WriteLine($"Resubscription: {sysMessage}, {subMessage}");
            if (!p.NotifySubscribed) return;
            // TODO: Have these last longer because they're important.
            // TODO: Should I make new broadcasts that supports using the User ID?
            BroadcastNotification(e.ReSubscriber.DisplayName, sysMessage);
            BroadcastNotification(e.ReSubscriber.DisplayName, subMessage);
        }
        public void OnNewSubscriber(object sender, OnNewSubscriberArgs e)
        {
            var sysMessage = $"{e.Subscriber.DisplayName} subscribed using {e.Subscriber.SubscriptionPlanName}!";
            var subMessage = $"{e.Subscriber.DisplayName} SubMsg: {e.Subscriber.ResubMessage.Trim()}";
            Debug.WriteLine($"Subscription: {sysMessage}, {subMessage}");
            if (!p.NotifySubscribed) return;
            // TODO: Have these last longer because they're important.
            // TODO: Should I make new broadcasts that supports using the User ID?
            BroadcastNotification(e.Subscriber.DisplayName, sysMessage);
            BroadcastNotification(e.Subscriber.DisplayName, subMessage);
        }
        public void OnGiftedSubscription(object sender, OnGiftedSubscriptionArgs e)
        {
            var message = $"{e.GiftedSubscription.DisplayName} gifted a subscription to {e.GiftedSubscription.MsgParamRecipientDisplayName} for {e.GiftedSubscription.MsgParamMonths} month(s) using {e.GiftedSubscription.MsgParamSubPlanName}!";
            Debug.WriteLine(message);
            if (!p.NotifySubscribed) return;
            // TODO: Have these last longer because they're important.
            // TODO: Should I make new broadcasts that supports using the User ID?
            BroadcastNotification(e.GiftedSubscription.DisplayName, message);
        }
        public void OnRaidNotification(object sender, OnRaidNotificationArgs e)
        {
            var message = $"{e.RaidNotificaiton.MsgParamDisplayName} is raiding you with {e.RaidNotificaiton.MsgParamViewerCount} viewers!";
            Debug.WriteLine(message);
            if (!p.NotifyRaided) return;
            // TODO: Have these last longer because they're important.
            // TODO: Should I make new broadcasts that supports using the User ID?
            BroadcastNotification(e.RaidNotificaiton.MsgParamDisplayName, message);
        }
        #endregion

        #region OpenVR
        private void OpenVRWorker()
        {
            Thread.CurrentThread.IsBackground = true;
            while (true)
            {
                if (OpenVR_Initiated)
                {
                    var newEvents = new List<VREvent_t>(ovr.GetNewEvents());
                    var shouldEnd = LookForSystemEvents(newEvents.ToArray());
                    if (shouldEnd)
                    {
                        overlayHandle = 0;
                        continue;
                    }
                    Thread.Sleep(250);
                }
                else
                {
                    if (!OpenVR_Initiated)
                    {
                        Debug.WriteLine("Initializing OpenVR");
                        OpenVR_Initiated = InitVr();
                    }
                    Thread.Sleep(5000);
                }
            }
        }

        public bool InitVr()
        {
            try
            {
                var success = ovr.Init();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var message = success ? "OpenVR Connected" : "OpenVR Disconnected";
                    var toolTip = success ? "Successfully connected to OpenVR." : "Could not connect to any compatible OpenVR service.";
                    openVRStatusEvent?.Invoke(success, message, toolTip);
                });
                return success;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to init VR: " + e.Message);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    openVRStatusEvent?.Invoke(false, $"OpenVR Error: {e.Message}", "An error occured while connecting to an OpenVR service.");
                });
                return false;
            }
        }

        private bool LookForSystemEvents(VREvent_t[] events)
        {
            foreach (var e in events)
            {
                String name = Enum.GetName(typeof(EVREventType), e.eventType);
                var age = e.eventAgeSeconds;
                // Debug.WriteLine($"EVENT: {name} ({age}s, i:{e.trackedDeviceIndex})");

                switch ((EVREventType)e.eventType)
                {
                    case EVREventType.VREvent_Quit:
                        OpenVR_Initiated = false;
                        ovr.AcknowledgeShutdown();
                        ovr.Shutdown();
                        return true;
                }
            }
            return false;
        }

        public void BroadcastNotification(string username, string message)
        {
            BroadcastNotification(username, message, System.Drawing.Color.Transparent);
        }

        public void BroadcastNotification(string username, string message, System.Drawing.Color color)
        {
            if(!ovr.IsInitialized()) {
                if (!ovr.Init())
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
                    color = System.Drawing.Color.FromArgb(colorDec);
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
                    NotificationBitmap_t icon = EasyOpenVRSingleton.BitmapUtils.NotificationBitmapFromBitmap(bmp);
                    BroadcastNotification(message, icon);
                    return;
                }
            }

            RGBtoBGR(ref color); // Fix color
            /*
             * Will skip web requests if no Kraken access
             */
            if(p.AppClientId.Length == 0)
            {
                Debug.WriteLine("No API access, broadcasting without user portrait.");
                BroadcastNotification(message, EasyOpenVRSingleton.BitmapUtils.NotificationBitmapFromBitmapData(GeneratePlaceholderBitmapData(color, username, b64name)));
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
                // TODO: Replace Kraken with Helix API call here.
                WebRequest request = WebRequest.Create("https://api.twitch.tv/kraken/channels/" + username);
                request.Headers.Add("Client-ID: " + Utils.DecryptStringFromBase64(p.AppClientId, p.Entropy));

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
                        Rectangle rect = new Rectangle(System.Drawing.Point.Empty, bmp.Size);
                        gfx.Clear(color); // Background
                        gfx.DrawImageUnscaledAndClipped(bmp, rect);
                        System.Drawing.Pen pen = new System.Drawing.Pen(color, 32f);
                        gfx.DrawRectangle(pen, rect); // Outline
                        gfx.Flush();
                        userLogos.Add(b64name, bmpEdit); // Cache
                        BitmapData TextureData = EasyOpenVRSingleton.BitmapUtils.BitmapDataFromBitmap(bmpEdit); // Allocate
                        Debug.WriteLine("Bitmap acquisition successful, broadcasting.");
                        BroadcastNotification(message, EasyOpenVRSingleton.BitmapUtils.NotificationBitmapFromBitmapData(TextureData)); // Submit
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error broadcasting notification: "+e.Message);
                BroadcastNotification(message, EasyOpenVRSingleton.BitmapUtils.NotificationBitmapFromBitmapData(GeneratePlaceholderBitmapData(color, username, b64name)));
                return;
            }
        }

        private void BroadcastNotification(string message, NotificationBitmap_t icon)
        {
            if (overlayHandle == 0)
            {
                // TODO: Add more overlays for the various types of messages.
                overlayHandle = ovr.InitNotificationOverlay($"Twitch Chat");
            }

            if (OpenVR_Initiated)
            {
                ovr.EnqueueNotification(overlayHandle, message, icon);
            }
        }
        #endregion

        #region Utility
        private BitmapData GeneratePlaceholderBitmapData(System.Drawing.Color color, String username, String b64name)
        {
            Bitmap bmp = new Bitmap(300, 300);
            Rectangle rect = new Rectangle(System.Drawing.Point.Empty, bmp.Size);

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
                gfx.DrawString(letter, new Font("Tahoma", 150), System.Drawing.Brushes.White, rect, sf);
                gfx.Flush();
            }
            if(!userLogos.ContainsKey(b64name)) userLogos.Add(b64name, bmp); // Cache
            return EasyOpenVRSingleton.BitmapUtils.BitmapDataFromBitmap(bmp);
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
        #endregion
    }
}
