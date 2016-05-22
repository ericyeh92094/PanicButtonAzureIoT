using System;
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

namespace PanicButtonAzureIoT
{
    class Program
    {
        static RegistryManager registryManager;
        static string connectionString = "HostName=lishansecurity.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=PuzeLHuLPxH7f48bJppTnVmK7KDLdpp4mIoe/X4+DYg=";
        //"HostName=lishansecurity.azure-devices.net;DeviceId=panicbutton-1;SharedAccessKey=vAJNz3OKUjAioTrTsXxJWxDwjxzoVnrFNf8qNO2D9/k="

        static DeviceClient deviceClient;
        static string iotHubUri = "{iot hub hostname}";
        static string deviceKey = "{device key}";
        static string iotHubD2cEndpoint = "messages/events";
        static EventHubClient eventHubClient;

        static string devPos = "";

        private static async Task AddDeviceAsync()
        {
            int numId = 0;
            string deviceId = "myPanicButton" + numId.ToString();
            Device device;
            bool success = false;

            while (!success)
            {
                try
                {
                    device = await registryManager.AddDeviceAsync(new Device(deviceId));
                }
                catch (DeviceAlreadyExistsException)
                {
                    //device = await registryManager.GetDeviceAsync(deviceId);
                    numId++;
                    deviceId = "myPanicButton" + numId.ToString();

                    if (numId > 10) success = true;
                    else continue;
                }
                success = true;
                
            }
            Console.WriteLine("Generated device key: {0}", device.Authentication.SymmetricKey.PrimaryKey);
        }

        private static async void SendDeviceToCloudMessagesAsync()
        {
            double avgWindSpeed = 10; // m/s
            Random rand = new Random();

            while (true)
            {
                double currentWindSpeed = avgWindSpeed + rand.NextDouble() * 4 - 2;

                var telemetryDataPoint = new
                {
                    deviceId = "myFirstDevice",
                    windSpeed = currentWindSpeed
                };
                var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
                var message = new Message(Encoding.ASCII.GetBytes(messageString));

                await deviceClient.SendEventAsync(message);
                Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, messageString);

                Task.Delay(1000).Wait();
            }
        }

        private static async Task ReceiveMessagesFromDeviceAsync(string partition, CancellationToken ct)
        {
            var eventHubReceiver = eventHubClient.GetDefaultConsumerGroup().CreateReceiver(partition, DateTime.UtcNow);
            while (true)
            {
                if (ct.IsCancellationRequested) break;
                EventData eventData = await eventHubReceiver.ReceiveAsync();
                if (eventData == null) continue;

                string data = Encoding.UTF8.GetString(eventData.GetBytes());
                Console.WriteLine("Message received. Partition: {0} Data: '{1}'", partition, data);
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
        }
    }
}
