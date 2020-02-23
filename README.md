# TwitchVRNotifications
A companion application for OpenVR that pipes Twitch chat messages to notifications in VR
![SteamVR notification triggered by Twitch chat](https://i.imgur.com/eKwTCJZ.png)

This application has been worked on sporadically but lately has seen a burst of activity working towards the publishing of the first binaries. Issues will be handled when there is time.

My personal motivation to make this was to get chat notifications I could notice, as I often forgot to check other overlays that were attached to my controllers or the world. I used the built in notifications as they always pop up in your field of view.

## Disclaimer
I hardly know what the word means, but I was told this can be important. This software is provided as is with no warranties or guarantees it will actually work or not blow up your machine or similar cataclysms. That said I'm fairly confident it will do what is advertised.

## Libraries and SDKs used
* https://github.com/BOLL7708/EasyOpenVR
* https://github.com/ValveSoftware/openvr
* https://github.com/TwitchLib/TwitchLib
* https://github.com/Fody/Costura

## How to use
The options and buttons should be somewhat self-explanatory, there are a number of tooltips in the application but here are some fleshed out instructions just in case.
### Interface
![The application interface](https://i.imgur.com/dTyaJQu.png)
### Chat Settings
* **Channel to monitor**: The channel on Twitch that you want to monitor for chat messages and events.
* **Bot username**: This is the user account we will grab the avatar from for system events, like being disconnected. You can use your own account if you don't care to have a separate avatar for it, or use the ChatBotVR to get the logo for this app.
* **Bot chat token**: This is an OAuth token, something that has been mandatory to sign into Twitch chat since 2013, you can acquire one by signing in [here](https://twitchapps.com/tmi/).
### App Auth Settings
* **App client ID**: This ID is used when talking to the Twitch APIs to acquire things like user profile pictures and follower status. Related features in this application will not work without it and the secret below, but it's not a must to use these. 
Acquiring a client ID involves registering an application [here](https://glass.twitch.tv/console/apps). Fill in a unique name, something like "[YourName] VR Chat", the redirect URL can be set to mostly anything, like "http://localhost". Pick a suitable category, like "Chat bot". After you saved it, click manage to retrieve your Client ID.
* **App secret**: This is a secret needed to acquire the access token for the APIs. It's in the same place as above, under manage for the app, click "New Secret" and copy it over to this field.
### Message Filtering
* **Require prefix**: This will only allow messages starting with this prefix through, there is also a checkbox Enabled to easily turn it on or off.
### Status
Three status fields for the various services we connect to.
* **Bot status**: Shows if you are connected to chat, without this working the app is nearly useless. On disconnect it automatically tries to reconnect with increasing delay between retries.
* **OpenVR status**: Shows if there is an OpenVR-compatible server running and that we could connect to it.
* **Access token status**: Shows if we have an access token or not, if in doubt if it's still valid it can be forcibly refreshed.
* **Force access token refresh**: Will force an update of the access token regardless if it has expired or not. This quires a valid client ID and secret to succeed.
### Avatar Settings
* **Enabled**: Enable this to load user profile pictures to use as the image when they send you messages. Otherwise they'll get a flat colored image with the first letter in their username.
* **Add chat color border**: This adds a border around the profile image that is colorized after their set color.
### Test
* **Test username**: The Twitch username that will be used for the test message, will if possible load their avatar picture.
* **Test notification**: This button will push the message into SteamVR, it does _not_ send anything to the Twitch chat. It overrides any message filtering settings you have activated.
* **Test message**: The text that is used in the test notification.

## Note
* Bot chat token, app client ID, app secret and the access token are stored encrypted based on the current user signed in to the system.
* Notifications for subscriptions and raid has not been properly tested, time simply ran out. At some point I'll connect the bot to a popular channel or try to simulate some of these events. If you experience something weird don't hesitate reporting it.
* Feedback is appreciated, the [issues](https://github.com/BOLL7708/TwitchVRNotifications/issues) section is open.
