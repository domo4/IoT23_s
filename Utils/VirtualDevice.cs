using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Opc.UaFx;
using Opc.UaFx.Client;
using System.Net.Mime;
using System.Text;

namespace Utils
{
    public class VirtualDevice
    {
        private readonly DeviceClient client;
        private readonly OpcClient opcClient;
        private readonly string selectedDevice;

        public VirtualDevice(DeviceClient client, OpcClient opcClient, string selectedDevice)
        {
            this.client = client;
            this.opcClient = opcClient;
            this.selectedDevice = selectedDevice;
        }

        public async Task MethodCaller(int method)
        {
            switch(method)
            {
                case 0:
                    opcClient.CallMethod(new OpcCallMethod("ns=2;s=" + selectedDevice, "ns=2;s=" + selectedDevice + "/EmergencyStop"));
                    break;
                case 1:
                    opcClient.CallMethod(new OpcCallMethod("ns=2;s=" + selectedDevice, "ns=2;s=" + selectedDevice + "/ResetErrorStatus"));
                    break;
            }
            await Task.Delay(0);
        }

        #region SendingMessages
        public async Task SendMessage(OpcReadNode[] telemetry, int ms)
        {
            while(true) 
            {
                Console.WriteLine($"{DateTime.Now}: Sending a message to IoTHub...");

                var job = opcClient.ReadNodes(telemetry);

                List<object> l = new List<object>();
                foreach (var item in job)
                {
                    l.Add(item.Value);
                }

                var data = new
                {
                    DeviceName = selectedDevice,
                    ProductionStatus = l[0],
                    WorkorderId = l[1],
                    GoodCount = l[2],
                    BadCount = l[3],
                    Temperature = l[4]
                };

                var dataString = JsonConvert.SerializeObject(data);

                Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataString));
                eventMessage.ContentType = MediaTypeNames.Application.Json;
                eventMessage.ContentEncoding = "utf-8";
                eventMessage.Properties.Add("MessageType", "Telemetry");

                await client.SendEventAsync(eventMessage);
                await Task.Delay(ms);
            }
        }

        public async Task SendEventMessage()
        {
            Console.WriteLine($"{DateTime.Now}: Sending an event message to IoTHub...");

            var deviceError = new OpcReadNode("ns=2;s=" + selectedDevice + "/DeviceError");
            var data = new
            {
                DeviceName = selectedDevice,
                DeviceError = opcClient.ReadNode(deviceError).Value
            };
            var dataString = JsonConvert.SerializeObject(data);

            Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataString));
            eventMessage.ContentType = MediaTypeNames.Application.Json;
            eventMessage.ContentEncoding = "utf-8";
            eventMessage.Properties.Add("MessageType", "Event");

            await client.SendEventAsync(eventMessage);
        }
        #endregion

        #region DeviceTwin
        public async Task UpdateTwinAsync()
        {
            var reportedProperties = new TwinCollection();
            var deviceError = new OpcReadNode("ns=2;s=" + selectedDevice + "/DeviceError");
            var productionRate = new OpcReadNode("ns=2;s=" + selectedDevice + "/ProductionRate");
            reportedProperties["DeviceError"] = opcClient.ReadNode(deviceError).Value;
            reportedProperties["ProductionRate"] = opcClient.ReadNode(productionRate).Value;

            await client.UpdateReportedPropertiesAsync(reportedProperties);
        }

        private async Task UpdateDesiredTwinAsync()
        {
            var reportedProperties = new TwinCollection();
            var productionRate = new OpcReadNode("ns=2;s=" + selectedDevice + "/ProductionRate");
            reportedProperties["ProductionRate"] = opcClient.ReadNode(productionRate).Value;

            await client.UpdateReportedPropertiesAsync(reportedProperties);
        }

        private async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object _)
        {
            var twin = await client.GetTwinAsync();
            Int32 productionRate = twin.Properties.Desired["ProductionRate"];
            OpcStatus result = opcClient.WriteNode("ns=2;s=" + selectedDevice + "/ProductionRate", productionRate);
            await UpdateDesiredTwinAsync();
        }
        #endregion

        #region DirectMethods
        private async Task<MethodResponse> EmergencyStopHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"\tMETHOD EXECUTED: {methodRequest.Name}");
            await MethodCaller(0);
            return new MethodResponse(0);
        }
        private async Task<MethodResponse> ResetErrorStatusHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"\tMETHOD EXECUTED: {methodRequest.Name}");
            await MethodCaller(1);
            return new MethodResponse(0);
        }
        private async Task<MethodResponse> DefaultServiceHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"\tUNDEFINED HANDLER FOR METHOD: {methodRequest.Name}");
            await Task.Delay(1000);
            return new MethodResponse(0);
        }
        #endregion

        public async Task InitializeHandlers()
        {
            await client.SetMethodDefaultHandlerAsync(DefaultServiceHandler, client);
            await client.SetMethodHandlerAsync("EmergencyStop", EmergencyStopHandler, client);
            await client.SetMethodHandlerAsync("ResetErrorStatus", ResetErrorStatusHandler, client);
            await client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, client);
        }
    }
}