using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Valve.VR;

namespace TwitchVRNotifications
{
    class OpenVRHandler
    {
        private TrackedDevicePose_t[] devicePoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        private bool successfullyInitialized = false;

        private List<uint> Notifications = new List<uint>(1);

        /**
        * A simple identifying class for tracked devices
        */
        public class TrackedDevice
        {
            private uint deviceId;
            private string deviceClass;
            public TrackedDevice(uint deviceId, string deviceClass)
            {
                this.deviceId = deviceId;
                this.deviceClass = deviceClass;
            }
            public uint getDeviceId()
            {
                return deviceId;
            }
            public string getDeviceClass()
            {
                return deviceClass;
            }
            override public string ToString()
            {
                return deviceId + " " + deviceClass;
            }
            public static bool Parse(string inStr, out TrackedDevice output)
            {
                String[] parts = inStr.Split(new Char[] { ' ' });
                uint deviceId = 0;
                if (parts.Length == 2 && UInt32.TryParse(parts[0], out deviceId))
                {
                    output = new TrackedDevice(deviceId, parts[1]);
                    return true;
                }
                output = null;
                return false;
            }
        }

        public OpenVRHandler()
        {
            successfullyInitialized = initialize();
        }

        private bool initialize()
        {
            var error = EVRInitError.None;
            uint initState = OpenVR.InitInternal(ref error, EVRApplicationType.VRApplication_Overlay);
            // OpenVR.GetGenericInterface(OpenVR.IVRCompositor_Version, ref error);
            // OpenVR.GetGenericInterface(OpenVR.IVRNotifications_Version, ref error);
            return initState > 0;
        }

        public bool isInitialized()
        {
            return successfullyInitialized;
        }

        private bool reinitialize()
        {
            return successfullyInitialized = initialize();
        }

        private class VRNotification : OpenVR
        {

        }

        public Boolean IsEventNotification()
        {
            // VR Events
            try
            {
                VREvent_t mEvent = new VREvent_t();
                if (OpenVR.System.PollNextEvent(ref mEvent, (uint)System.Runtime.InteropServices.Marshal.SizeOf(mEvent)))
                {
                    return (EVREventType) mEvent.eventType == EVREventType.VREvent_Notification_Shown;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Event poll exception: " + e.Message);
            }
            return false;
        }

        public bool broadcastNotification(String message, String sender)
        {
            if (!isInitialized() && !reinitialize()) return false;

            // Fetch SteamVR overlay
            string overlayKey = "valve.steam.desktop";
            ulong overlayHandle = 0;
            EVROverlayError overlayError = OpenVR.Overlay.FindOverlay(overlayKey, ref overlayHandle);
            if (overlayError != EVROverlayError.None)
            {
                Debug.WriteLine("Overlay error: " + overlayError.ToString());
                return false;
            }

            // Initialize notification interface
            EVRInitError initError = EVRInitError.None;
            IntPtr notificationInterface = OpenVR.GetGenericInterface(OpenVR.IVRNotifications_Version, ref initError);
            if (initError != EVRInitError.None)
            {
                Debug.WriteLine("Notification init error: " + initError.ToString());
                return false;
            }

            // Create notification
            NotificationBitmap_t bitmap = new NotificationBitmap_t();

            uint id = 0;
            Debug.WriteLine(overlayHandle.ToString()+", "+id);
            CVRNotifications notificationController = new CVRNotifications(notificationInterface);
            notificationController.CreateNotification(
                    overlayHandle,
                    0,
                    EVRNotificationType.Transient,
                    sender + ": " + message,
                    EVRNotificationStyle.None,
                    ref bitmap,
                    ref id
                );
         

            /*
            vrnotification->CreateNotification(
                parent->overlayHandle(), 666, vr::EVRNotificationType_Transient, alarmMessageBuffer, vr::EVRNotificationStyle_Application, messageIconPtr, &notificationId
            );
            */
            
            return true;
        }

        private static void ProcessResult(IAsyncResult result)
        {
            Debug.Write("Invocation result: "+result.ToString());
        }

        internal void test()
        {
            Debug.WriteLine("This was a success? : "+broadcastNotification("This is a test.", "Tester").ToString());
        }
    }
}
