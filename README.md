# TwitchVRNotifications
A compantion application for SteamVR that pipes Twitch chat messages to SteamVR notifications
![SteamVR notification triggered by Twitch chat](https://i.imgur.com/pKchzJk.png)

This application is being worked on very sporadically but has gotten quite stable so here it is. 
It was made mainly to get noticable delivery of chat messages as I often missed chat.

## Use
The interface should be somewhat self-explanatory, but just here is some fleshed out instructions.
### Main setup
* **Twitch username**: fill in your Twitch username, this is used to authenticate the chat bot that connects to your channel as well as when doing SteamVR notifications about bot status.
* **Auto-connect chat**: As it says, will automatically connect the chat bot to Twitch upon application launch.
* **Chat auth token**: This is an OAuth token, something that has been mandatory to sign into Twitch chat since 2013, you can acquire one by signing in [here](https://twitchapps.com/tmi/).
* **Connect**: If the chat is not on auto-connect or failed connecting this button will initiate a connection attempt.
* **Filter messages on**: This will only pipe messages starting with this tag, there is also a checkbox to easily turn it on or off.
* **Kraken API client-ID**: This is an API key to be able to load users avatar pictures, if that's not important you can leave this empty. Getting one involves registering an application [here](https://glass.twitch.tv/console/apps/create). Simply fill in a name you will recognize, redirect URL can be "http://localhost", pick a suitable category. After you saved it, click manage to retrieve your Client ID.
### Test
* **Test username**: The Twitch username that will be used for the test message, will if Kraken Client ID is supplied load their avatar picture.
* **Test notification**: This button will push the message into SteamVR, it does _not_ send anything to the Twitch chat.
* **Test message**: The text to be used in the notification.
### System
* **Save settings**: This is a button to save settings in case you don't have auto-save checked.
* **Auto-save**: will save after a field has been modified, for text areas it means after focus has been lost.
* **Init OpenVR**: The app tries to initialize on startup, but if that fails you can attempt it manually with this button.

## Note
* One main concern is that this is running a quite old version of the OpenVR DLL, this due to relying on [a library](https://github.com/artumino/SteamVR_HUDCenter) that fixes the broken endpoints for notifications that exist in the C# header provided by Valve. This means if Valve does breaking changes to OpenVR this might stop working.
* The application will close if SteamVR is closed, this as exiting is not handled yet. I'm not sure if I can actually affect it as I'm using the above library for OpenVR interactions.
* This was developed very much ad hoc, the interface is super raw and a bit clunky. As it's currently working well enough there are no immediate plans for major changes.
* After first run, filling in all the information, if it does not work properly try just restarting it. There are apparently a few hickups on init when the values are missing.
* Chat OAuth token and Kraken Client ID are encrypted based on the current user using the system.
