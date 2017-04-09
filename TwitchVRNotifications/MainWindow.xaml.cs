using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Valve.VR;
using TwitchLib;
using TwitchLib.Models.Client;
using TwitchLib.Events.Client;

namespace TwitchVRNotifications
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private OpenVRHandler oh;
        Properties.Settings p = Properties.Settings.Default;
        TwitchClient client;

        public MainWindow()
        {
            InitializeComponent();

            oh = new OpenVRHandler();
            // bool success = oh.broadcastNotification("This is a test.", "User");
            // Debug.Write("This happened: "+success.ToString());
            textBox_UserName.Text = p.UserName;
            textBox_AuthToken.Text = p.AuthToken;

            var t = new Thread(worker);
            if (!t.IsAlive) t.Start();
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("We clicked ze bytton.");
            oh.test();
        }

        private void worker()
        {
            Thread.CurrentThread.IsBackground = true;
            while (true)
            {
                if (oh.IsEventNotification())
                {
                    Debug.WriteLine("Notifcation shown.");
                } else
                {
                    // Debug.WriteLine("Nothing!");
                }
                Thread.Sleep(100);
            }
        }

        private void button_Save_Click(object sender, RoutedEventArgs e)
        {
            p.UserName = textBox_UserName.Text;
            p.AuthToken = textBox_AuthToken.Text;
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
            Debug.WriteLine(e.ChatMessage.Username+": "+e.ChatMessage.Message);
            if(e.ChatMessage.Message.Contains("!VR"))
            {
                Debug.WriteLine("VR Message received: " + e.ChatMessage.Message);
            }
        } 
    }
}
