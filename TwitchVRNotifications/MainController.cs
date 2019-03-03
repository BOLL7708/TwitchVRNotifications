using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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
using TwitchLib.Api.Helix.Models.Users;
using System.Net.Http;

namespace TwitchVRNotifications
{
    class MainController
    {
        Properties.Settings p = Properties.Settings.Default;
        TwitchClient client = new TwitchClient();
        TwitchAPI api = new TwitchAPI();
        EasyOpenVRSingleton ovr = EasyOpenVRSingleton.Instance;
        ulong overlayHandle = 0;

        public bool OpenVR_Initiated = false;
        private int connectionAttempts = 0;
        private bool isConnectingToChat = false;

        private Thread ovrThread;
        private Thread chatThread;

        Dictionary<string, Bitmap> userLogos = new Dictionary<string, Bitmap>();
        private String[] ignoredUsers = new string[0];
        private readonly object
            userLogosLock = new object(),
            ignoredUsersLock = new object(),
            accessTokenLock = new object();

        public Action<bool, string, string> openVRStatusEvent;
        public Action<bool, string, string> chatBotStatusEvent;
        public Action<bool, string, string> accessTokenEvent;

        public MainController()
        {
            ovrThread = new Thread(OpenVRWorker);
            if (!ovrThread.IsAlive) ovrThread.Start();
            chatThread = new Thread(ChatWorker);
            if (!chatThread.IsAlive) chatThread.Start();
            ReloadIgnoredUsers();
        }

        public async Task<string> CheckVersion()
        {
            var url = "https://api.github.com/repos/BOLL7708/TwitchVRNotifications/releases/latest";
            string result = null;
            var headers = new Dictionary<string, string> {
                {"Accept","application/vnd.github.v3+json"},
                {"User-Agent", "BOLL7708/TwitchVRNotifications"}
            };
            try
            {
                var release = await DoWebRequest(url, null, headers);
                string currentTag = release["tag_name"];
                Version.TryParse(currentTag.TrimStart('v'), out Version currentVersion);
                Version.TryParse(p.Version.TrimStart('v'), out Version thisVersion);
                if (currentVersion.CompareTo(thisVersion) > 0) return release["tag_name"];
                else return "";
            } catch(Exception e)
            {
                Debug.WriteLine($"Error getting version: {e.Message} -> {e.StackTrace}");
            }
            return result;
        }

        #region TwitchLib
        private void ChatWorker()
        {
            Thread.CurrentThread.IsBackground = true;
            while (true)
            {
                if(!IsChatConnected() && !isConnectingToChat)
                {
                    ChatStatus(true, "Connecting to chat", "Currently attempting to connect to the chat channel...");
                    ConnectChat();
                }
                Thread.Sleep(5000+1000*connectionAttempts*5);
            }
        }

        private void ChatStatus(bool status, string label, string toolTip)
        {
            Application.Current.Dispatcher.Invoke(() =>
                {
                    Debug.WriteLine($"{status}, {label} - {toolTip}");
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

        // There exists an undocumented TwitchAPI for this but I had issues with it so this is my own implementation.
        public async void RefreshAccessToken(bool force = false)
        {
            Application.Current.Dispatcher.Invoke(() => {
                accessTokenEvent?.Invoke(false, "Refreshing token...", $"Currently attempting to retrieve a new access token.");
            });
            if (p.AppClientId.Length == 0)
            {
                accessTokenEvent?.Invoke(false, "Client ID not set", $"Unable to request a token unless client ID is set.");
                return;
            }
            if (p.AppSecret.Length == 0)
            {
                accessTokenEvent?.Invoke(false, "Secret not set", $"Unable to request a token unless secret is set.");
                return;
            }
            var oldAccessToken = "";
            lock(accessTokenLock)
            {
                oldAccessToken = Utils.DecryptStringFromBase64(p.AccessToken, p.Entropy);
                var expireTime = 45 * 24*60*60; // Access token usually lasts 60 days, refresh a bit earlier.
                if (!(force || (p.AccessTokenCreated + expireTime) < DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
                {
                    var message = "No need to refresh access token.";
                    Debug.WriteLine(message);
                    accessTokenEvent?.Invoke(true, "Token should still be valid", message);
                    return;
                }
            }
            try
            {
                var body = new Dictionary<string, string>
                {
                    {"client_id", Utils.DecryptStringFromBase64(p.AppClientId, p.Entropy)},
                    {"client_secret", Utils.DecryptStringFromBase64(p.AppSecret, p.Entropy)},
                    {"grant_type", "client_credentials"}
                };
                var jsonObj = await DoWebRequest("https://id.twitch.tv/oauth2/token", body);
                string token = jsonObj["access_token"];
                lock(accessTokenLock)
                {
                    p.AccessToken = Utils.EncryptStringToBase64(token, p.Entropy);
                    p.AccessTokenCreated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    p.Save();
                    Debug.WriteLine("Access token was refreshed."+token);
                    Application.Current.Dispatcher.Invoke(() => {
                        accessTokenEvent?.Invoke(true, "Successfully refreshed token", $"Token retrieval was successful.");
                    });
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to refresh access token: {e.Message}");
                Application.Current.Dispatcher.Invoke(() => {
                    accessTokenEvent?.Invoke(false, "Unable to refresh token", $"Unable to retrieve new token: {e.Message}");
                });
                return;
            }
            try
            {
                var body = new Dictionary<string, string>
                {
                    {"client_id", Utils.DecryptStringFromBase64(p.AppClientId, p.Entropy)},
                    {"token", oldAccessToken}
                };
                await DoWebRequest("https://id.twitch.tv/oauth2/revoke", body);
                Debug.WriteLine($"Successfully revoked old token");
            } catch(Exception e)
            {
                Debug.WriteLine($"Revoking old token was unsuccessful: {e.Message}");
            }
        }

        private async Task<dynamic> DoWebRequest(string url, Dictionary<string, string> values = null, Dictionary<string, string> headers = null)
        {
            HttpResponseMessage response;
            if(headers != null)
            {
                foreach(KeyValuePair<string, string> entry in headers)
                {
                    MainWindow.http.DefaultRequestHeaders.Add(entry.Key, entry.Value);
                }
            }
            else
            {
                MainWindow.http.DefaultRequestHeaders.Clear();
            }
            if(values != null)
            {
                var content = new FormUrlEncodedContent(values);
                response = await MainWindow.http.PostAsync(url, content);
            }
            else
            {
                response = await MainWindow.http.GetAsync(url);
            }
            var responseString = await response.Content.ReadAsStringAsync();
            return new JavaScriptSerializer().Deserialize<dynamic>(responseString);            
        }

        public bool IsChatConnected()
        {
            return client != null && client.IsConnected;
        }

        public void ReloadIgnoredUsers()
        {
            lock(ignoredUsersLock)
            {
                ignoredUsers = p.IgnoreUsers.Split(',');
                for(var i=0; i<ignoredUsers.Length; i++)
                {
                    ignoredUsers[i] = ignoredUsers[i].Trim().ToLower();
                }
            }
        }

        private async void OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            Debug.WriteLine("Message from "+e.ChatMessage.Username+": " + e.ChatMessage.Message);
            string prefix = p.MessagePrefix;
            if (p.MessagePrefixOn && prefix.Length > 0 && e.ChatMessage.Message.IndexOf(prefix) != 0) return;
            if (p.IgnoreBroadcaster && e.ChatMessage.IsBroadcaster) return;
            lock(ignoredUsersLock)
            {
                if (ignoredUsers.Length > 0 && Array.Exists(ignoredUsers, s => s.Equals(e.ChatMessage.Username.ToLower())))
                {
                    Debug.WriteLine($"Users {e.ChatMessage.DisplayName} existed in ignore list and was ignored.");
                    return;
                }
            }

            var limitedAccess = p.AllowFollower || p.AllowSubscriber || p.AllowModerator || p.AllowVIP;
            var allow = false;
            if(limitedAccess && !e.ChatMessage.IsBroadcaster)
            {
                if (p.AllowFollower)
                {
                    lock(accessTokenLock)
                    {
                        api.Settings.ClientId = Utils.DecryptStringFromBase64(p.AccessToken, p.Entropy);
                        api.Settings.AccessToken = Utils.DecryptStringFromBase64(p.AccessToken, p.Entropy);
                    }
                    try
                    {
                        var followResponse = await api.Helix.Users.GetUsersFollowsAsync(null, null, 1, e.ChatMessage.UserId, e.ChatMessage.RoomId);
                        Debug.WriteLine($"Follow count: {followResponse.TotalFollows}");
                        if (followResponse.TotalFollows > 0) allow = true;
                    }
                    catch (Exception exception)
                    {
                        Debug.WriteLine($"Unable to load follower count: {exception.Message}");
                        allow = true;
                    }
                }
                if (p.AllowSubscriber && e.ChatMessage.IsSubscriber) allow = true;
                if (p.AllowModerator && e.ChatMessage.IsModerator) allow = true;
                // if (p.AllowVIP && !true) return; // TODO: Need to look through badges here.
            }
            if (limitedAccess && !allow)
            {
                Debug.WriteLine($"User {e.ChatMessage.DisplayName} is not allowed to send message, ignored.");
                return;
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

        // Untested
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
        // Untested
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
        // Untested
        public void OnGiftedSubscription(object sender, OnGiftedSubscriptionArgs e)
        {
            var message = $"{e.GiftedSubscription.DisplayName} gifted a subscription to {e.GiftedSubscription.MsgParamRecipientDisplayName} for {e.GiftedSubscription.MsgParamMonths} month(s) using {e.GiftedSubscription.MsgParamSubPlanName}!";
            Debug.WriteLine(message);
            if (!p.NotifySubscribed) return;
            // TODO: Have these last longer because they're important.
            // TODO: Should I make new broadcasts that supports using the User ID?
            BroadcastNotification(e.GiftedSubscription.DisplayName, message);
        }
        
        // Untested
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
        #endregion

        #region Notification Broadcasting
        public void BroadcastNotificationTest(string username, string message)
        {
            BroadcastNotification(username, message, System.Drawing.Color.Purple);
        }

        public void BroadcastNotificationSystem(string message)
        {
            BroadcastNotification(p.BotUsername, message, System.Drawing.Color.Transparent, true); 
        }

        public void BroadcastNotification(string username, string message)
        {
            BroadcastNotification(username, message, System.Drawing.Color.Black);
        }
        public void BroadcastNotification(string username, string message, System.Drawing.Color color)
        {
            BroadcastNotification(username, message, color, false);
        }
        public async void BroadcastNotification(string username, string message, System.Drawing.Color color, bool overrideIgnore)
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
            lock(userLogosLock)
            {
                if (userLogos.ContainsKey(b64name))
                {
                    Debug.WriteLine("User cached, broadcasting cached.");
                    Bitmap bmp;
                    if (userLogos.TryGetValue(b64name, out bmp))
                    {
                        Debug.WriteLine($"Found cached bitmap for {username}, will use that!");
                        NotificationBitmap_t icon = EasyOpenVRSingleton.BitmapUtils.NotificationBitmapFromBitmap(bmp);
                        SubmitNotification(message, icon);
                        return;
                    }
                }
            }

            RGBtoBGR(ref color); // Fix color
            /*
             * Will skip web requests if avatar disabled or no API access
             */
            if(!p.AvatarEnabled || p.AppClientId.Length == 0)
            {
                Debug.WriteLine("Avatars disabled or no API access, broadcasting without user portrait.");
                SubmitNotification(message, EasyOpenVRSingleton.BitmapUtils.NotificationBitmapFromBitmapData(GeneratePlaceholderBitmapData(color, username, b64name)));
                return;
            }

            /* 
             * Load user data from API
             * Load user icon if available, else placeholder
             * If icon draw border
             * Display notification
             */
            try
            {
                Debug.WriteLine("No weird stuff so far, let's try to load the user image!");
                lock (accessTokenLock)
                {
                    api.Settings.AccessToken = Utils.DecryptStringFromBase64(p.AccessToken, p.Entropy);
                }
                var userResponse = await api.Helix.Users.GetUsersAsync(null, new List<string> {username});
                var user = (User) userResponse.Users.GetValue(0);
                var logoUrl = user.ProfileImageUrl;
                Debug.WriteLine($"This is the URL we got: {logoUrl}");

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
                    gfx.DrawImageUnscaledAndClipped(bmp, rect); // TODO: Maybe we should make sure it's 300x300 here.
                    if(p.AvatarFrameEnabled)
                    {
                        System.Drawing.Pen pen = new System.Drawing.Pen(color, 32f);
                        gfx.DrawRectangle(pen, rect); // Outline
                    }
                    gfx.Flush();
                    lock(userLogosLock)
                    {
                        if (userLogos.Count >= 100) userLogos.Clear();
                        userLogos.Add(b64name, bmpEdit); // Cache
                    }
                    BitmapData TextureData = EasyOpenVRSingleton.BitmapUtils.BitmapDataFromBitmap(bmpEdit); // Allocate
                    Debug.WriteLine("Bitmap acquisition successful, broadcasting.");
                    SubmitNotification(message, EasyOpenVRSingleton.BitmapUtils.NotificationBitmapFromBitmapData(TextureData)); // Submit
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error broadcasting notification: "+e.Message);
                SubmitNotification(message, EasyOpenVRSingleton.BitmapUtils.NotificationBitmapFromBitmapData(GeneratePlaceholderBitmapData(color, username, b64name)));
                return;
            }
        }

        private void SubmitNotification(string message, NotificationBitmap_t icon)
        {
            if (overlayHandle == 0)
            {
                overlayHandle = ovr.InitNotificationOverlay($"Twitch Chat");
            }
            if (OpenVR_Initiated)
            {
                ovr.EnqueueNotification(overlayHandle, message, icon);
            }
        }

        public void InvalidateAvatars()
        {
            lock(userLogosLock)
            {
                userLogos.Clear();
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
            lock(userLogosLock)
            {
                if (userLogos.Count >= 100) userLogos.Clear();
                if (!userLogos.ContainsKey(b64name)) userLogos.Add(b64name, bmp); // Cache
            }
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
            // based on http://stackoverflow.com/a/11743162

            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }
        #endregion
    }
}
