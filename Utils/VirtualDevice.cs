using Microsoft.Azure.Devices.Client;
using Opc.UaFx;
using Opc.UaFx.Client;

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
        }
    }
}