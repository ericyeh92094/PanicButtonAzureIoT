using System;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Device.Location;
using Microsoft.ServiceBus.Messaging;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Common.Exceptions;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using RestSharp;
using System.Runtime.InteropServices;


namespace PanicButtonAzureIoT
{
    class Program
    {
        static RegistryManager registryManager;
        static string connectionString = "HostName=lishansecurity.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=PuzeLHuLPxH7f48bJppTnVmK7KDLdpp4mIoe/X4+DYg=";
 
        static DeviceClient deviceClient;
        static string iotHubUri = "lishansecurity.azure-devices.net";
        static string deviceKey = "W2rTkHcLmSKqcK79WtGlrvpS4HykaIAW+yxOGb1qpMA=";

        static string devPos = "";
        static string subKey = "SOFTWARE\\TVWS";
        static string KeyName = "PanicButtonID";
        static string deviceId = "";

        static string monitorUrl = "http://192.168.1.21:0808"; //"http://testnode1231231.azurewebsites.net/error/";
        static string monitorId = "1";

        [DllImport("HIDCtrl.dll")]
        internal static extern int PowerOnEx(int i);
        [DllImport("HIDCtrl.dll")]
        internal static extern int PowerOffEx(int i);

        public static void BuzzControl(bool on)
        {
            if (on)
                PowerOnEx(1);
            else
                PowerOffEx(1);
        }

        public static string ReadDeviceIDFromKey()
        {
            // Opening the registry key
            RegistryKey rk = Registry.CurrentUser;
            // Open a subKey as read-only
            RegistryKey sk1 = rk.OpenSubKey(subKey);
            // If the RegistrySubKey doesn't exist -> (null)
            if (sk1 == null)
            {
                return null;
            }
            else
            {
                try
                {
                    // If the RegistryKey exists I get its value
                    // or null is returned.
                    return (string)sk1.GetValue(KeyName);
                }
                catch (Exception e)
                {
                    // AAAAAAAAAAARGH, an error!
                    // ShowErrorMessage(e, "Reading registry " + KeyName.ToUpper());
                    return null;
                }
            }
        }
        public static bool WriteDeviceIDToKey(object Value)
        {
            try
            {
                // Setting
                RegistryKey rk = Registry.CurrentUser;
                // I have to use CreateSubKey 
                // (create or open it if already exits), 
                // 'cause OpenSubKey open a subKey as read-only
                RegistryKey sk1 = rk.CreateSubKey(subKey);
                // Save the value
                sk1.SetValue(KeyName, Value);

                return true;
            }
            catch (Exception e)
            {
                // AAAAAAAAAAARGH, an error!
                // ShowErrorMessage(e, "Writing registry " + KeyName.ToUpper());
                return false;
            }
        }

        private static async Task AddDeviceAsync()
        {
            int numId = 0;
 
            Device device;

            deviceId = ReadDeviceIDFromKey();
            if (deviceId != null)
            {
                device = await registryManager.GetDeviceAsync(deviceId);
            }
            else
            {           
                try
                {
                    var devices = await registryManager.GetDevicesAsync(10);
                    numId = devices.Count<Device>() + 1;

                } catch(DeviceNotFoundException)
                {
                    numId = 0;
                }

                deviceId = "myPanicButton" + numId.ToString();
                WriteDeviceIDToKey((object)deviceId);

                device = await registryManager.AddDeviceAsync(new Device(deviceId));
            }

            deviceKey = device.Authentication.SymmetricKey.PrimaryKey;
            //Console.WriteLine("Generated device key: {0}", device.Authentication.SymmetricKey.PrimaryKey);
        }

        /*
        private static async void SendDeviceToCloudMessagesAsync()
        {
            double avgWindSpeed = 10; // m/s
            Random rand = new Random();

            while (true)
            {
                double currentWindSpeed = avgWindSpeed + rand.NextDouble() * 4 - 2;

                var telemetryDataPoint = new
                {
                    windSpeed = currentWindSpeed
                };
                var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
                var message = new Microsoft.Azure.Devices.Client.Message(Encoding.ASCII.GetBytes(messageString));
                //message.Properties["messageType"] = "interactive";
                //message.MessageId = Guid.NewGuid().ToString();

                await deviceClient.SendEventAsync(message);
                Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, messageString);

                Task.Delay(10000).Wait();
            }
        }
        */

        private static async void SendDeviceToCloudMessagesAsync(string messageString)
        {
            var message = new Microsoft.Azure.Devices.Client.Message(Encoding.ASCII.GetBytes(messageString));
            //message.Properties["messageType"] = "interactive";
            //message.MessageId = Guid.NewGuid().ToString();

            await deviceClient.SendEventAsync(message);
            Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, messageString);

            Task.Delay(10000).Wait();
        }

        private static async void ReceiveC2dAsync()
        {
            Console.WriteLine("\nReceiving cloud to device messages from service");
            while (true)
            {
                Microsoft.Azure.Devices.Client.Message receivedMessage = await deviceClient.ReceiveAsync();
                if (receivedMessage == null) continue;

                string opStr = Encoding.ASCII.GetString(receivedMessage.GetBytes());

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Received message: {0}", opStr);
                Console.ResetColor();

                await deviceClient.CompleteAsync(receivedMessage);

                if (opStr.StartsWith("GPS"))
                {
                    SendDeviceToCloudMessagesAsync(devPos);
                }
                if (opStr.StartsWith("SET ID"))
                {
                    int idx = opStr.IndexOf("=");
                    monitorId = opStr.Substring(idx + 1);

                    SendDeviceToCloudMessagesAsync("OK");
                }
                if (opStr.StartsWith("SET URL"))
                {
                    int idx = opStr.IndexOf("=");
                    monitorUrl = opStr.Substring(idx + 1);

                    SendDeviceToCloudMessagesAsync("OK");
                }
                if (opStr.StartsWith("BUZZ ON"))
                {
                    BuzzControl(true);

                    SendDeviceToCloudMessagesAsync("OK");
                }
                if (opStr.StartsWith("BUZZ OFF"))
                {
                    BuzzControl(false);

                    SendDeviceToCloudMessagesAsync("OK");
                }
            }
        }

        private static void PingOut()
        {
            var client = new RestClient(monitorUrl);
            var request = new RestRequest(Method.POST);
            //request.AddHeader("postman-token", "e578ec62-2e4c-2046-a412-9a343ddab59d");
            //request.AddHeader("cache-control", "no-cache");
            request.AddParameter("error", monitorId.ToString());
            IRestResponse response = client.Execute(request);
        }

        private static void GetConsoleKey()
        {
            while (true)
            {
                bool F4pressed = false;
                int presscount = 0;
                ConsoleKeyInfo key;

                // wait for initial keypress:
                while (!Console.KeyAvailable)
                {
                    System.Threading.Thread.Sleep(10);
                }

                key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.F4:
                        F4pressed = true;
                        Console.WriteLine("F4 pressed start.");
                        break;
                    case ConsoleKey.F1:
                    case ConsoleKey.Escape:
                        Console.WriteLine("Program Exit");
                        BuzzControl(false);
                        return;
                    default:
                        F4pressed = false;
                        continue;
                }

                /*

                DateTime nextCheck = DateTime.Now.AddMilliseconds(1000);

                while ((nextCheck > DateTime.Now) && F4pressed)
                {
                    if (Console.KeyAvailable)
                    {
                        key = Console.ReadKey(true);
                        switch (key.Key)
                        {
                            case ConsoleKey.F4:
                                Console.WriteLine("F4 pressed.");
                                presscount++;
                                F4pressed = true;
                                break;
                            case ConsoleKey.F1:
                            case ConsoleKey.Escape:
                                Console.WriteLine("Program Exit");
                                BuzzControl(false);
                                return;
                            default:
                                F4pressed = false;
                                break;
                        }
                    }
                }
                */
                //if (F4pressed && (presscount > 3))
                if (F4pressed)
                {
                    Console.WriteLine("Alarm Fired");
                    PingOut();
                    BuzzControl(true);
                    System.Threading.Thread.Sleep(5);
                    BuzzControl(false);
                }
            }
        }

        private static void GeoPositionChanged(object sender, GeoPositionChangedEventArgs<GeoCoordinate> e)
        {
            devPos = e.Position.Location.Latitude + "/" + e.Position.Location.Longitude;
        }

        static void Main(string[] args)
        {
            GeoCoordinateWatcher watcher = new GeoCoordinateWatcher();
            watcher.PositionChanged +=
                new EventHandler<GeoPositionChangedEventArgs<
                    GeoCoordinate>>(GeoPositionChanged);

            watcher.Start();

            registryManager = RegistryManager.CreateFromConnectionString(connectionString);
            AddDeviceAsync().Wait();

            deviceClient = DeviceClient.Create(iotHubUri, new DeviceAuthenticationWithRegistrySymmetricKey(deviceId, deviceKey));

            ReceiveC2dAsync();

            GetConsoleKey();

        }
    }
}
