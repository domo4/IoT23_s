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

        private async Task MethodCaller(int method)
        {
            //Call selected method node
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
                Console.WriteLine($"[{selectedDevice}] {DateTime.Now}: Sending a message to IoTHub...");

                //Read selected nodes from OPC UA server
                var job = opcClient.ReadNodes(telemetry);

                //Create a list containing read data, and convert it to JSON afterwards
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

                //Send D2C message containing telemetry data
                await client.SendEventAsync(eventMessage);
                await Task.Delay(ms);
            }
        }

        public async Task SendEventMessage()
        {
            Console.WriteLine($"[{selectedDevice}] {DateTime.Now}: Sending an event message to IoTHub...");

            //Read DeviceError and create JSON that contains device name and the error value
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

            //Send D2C message containing event data
            await client.SendEventAsync(eventMessage);
        }
        #endregion

        #region DeviceTwin
        public async Task UpdateTwinAsync()
        {
            //Update reported properties of device twin
            var reportedProperties = new TwinCollection();
            var deviceError = new OpcReadNode("ns=2;s=" + selectedDevice + "/DeviceError");
            var productionRate = new OpcReadNode("ns=2;s=" + selectedDevice + "/ProductionRate");
            reportedProperties["DeviceError"] = opcClient.ReadNode(deviceError).Value;
            reportedProperties["ProductionRate"] = opcClient.ReadNode(productionRate).Value;

            await client.UpdateReportedPropertiesAsync(reportedProperties);
        }

        private async Task UpdateDesiredTwinAsync()
        {
            //Update production rate in reported properties of device twin, after changing desired properties
            var reportedProperties = new TwinCollection();
            var productionRate = new OpcReadNode("ns=2;s=" + selectedDevice + "/ProductionRate");
            reportedProperties["ProductionRate"] = opcClient.ReadNode(productionRate).Value;

            await client.UpdateReportedPropertiesAsync(reportedProperties);
        }

        private async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object _)
        {
            //Get production rate from desired properties of device twin
            var twin = await client.GetTwinAsync();
            Int32 productionRate = twin.Properties.Desired["ProductionRate"];
            //Set the production rate on the device in OPC UA server
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
            //Initialize handlers for direct methods & device twin
            await client.SetMethodDefaultHandlerAsync(DefaultServiceHandler, client);
            await client.SetMethodHandlerAsync("EmergencyStop", EmergencyStopHandler, client);
            await client.SetMethodHandlerAsync("ResetErrorStatus", ResetErrorStatusHandler, client);
            await client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, client);
        }
    }
}