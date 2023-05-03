using Utils;
using Microsoft.Azure.Devices.Client;
using Opc.UaFx.Client;
using Opc.UaFx;

//Read IIoTsimDeviceName:AzurePrimaryConnectionString pairs
IDictionary<string, string> devices = new Dictionary<string, string>();
using(StreamReader file = new StreamReader("config.txt"))
{
    string? line;
    string[] split;
    while ((line = file.ReadLine()) != null)
    {
        split = line.Split(':');
        devices.Add(split[0], split[1]);
    }
    file.Close();
}

using (var opcClient = new OpcClient("opc.tcp://localhost:4840/"))
{
    try
    {
        opcClient.Connect();
        Console.WriteLine("Connection success");

        //Create a list of all devices
        List<string> deviceList = new List<string>();
        var node = opcClient.BrowseNode(OpcObjectTypes.ObjectsFolder); //Start from the 'root' of all Nodes of the Server
        foreach (var childNode in node.Children())
        {
            if (childNode.DisplayName.Value != "Server")
            {
                deviceList.Add(childNode.DisplayName.Value);
            }
        }

        //Display the list of devices
        Console.WriteLine("\nAvailable devices:");
        foreach (var device in deviceList)
        {
            Console.WriteLine(device);
        }

        //Ask the user which device he wants to monitor
        string selectedDevice = "";
        while (selectedDevice == "" || !deviceList.Contains(selectedDevice))
        {
            Console.WriteLine("\nPlease specify which device you want to monitor:");
            selectedDevice = Console.ReadLine();
        }

        //Azure
        using var deviceClient = DeviceClient.CreateFromConnectionString(devices[selectedDevice], TransportType.Mqtt);
        await deviceClient.OpenAsync();
        var vDevice = new VirtualDevice(deviceClient, opcClient, selectedDevice);
        await vDevice.InitializeHandlers();

        //Telemetry Data Nodes for D2C
        var telemetry = new OpcReadNode[]
        {
            new OpcReadNode("ns=2;s=" + selectedDevice + "/ProductionStatus"),
            new OpcReadNode("ns=2;s=" + selectedDevice + "/WorkorderId"),
            new OpcReadNode("ns=2;s=" + selectedDevice + "/GoodCount"),
            new OpcReadNode("ns=2;s=" + selectedDevice + "/BadCount"),
            new OpcReadNode("ns=2;s=" + selectedDevice + "/Temperature")
        };

        //Device Twins for reported properties
        async Task TwinCaller(bool sendEvent)
        {
            await vDevice.UpdateTwinAsync();
            if(sendEvent)
            {
                await vDevice.SendEventMessage();
            }
            
        }

        void HandleDataChangedDeviceError(object sender, OpcDataChangeReceivedEventArgs e)
        {
            _ = TwinCaller(true);
        }

        void HandleDataChangedProductionRate(object sender, OpcDataChangeReceivedEventArgs e)
        {
            _ = TwinCaller(false);
        }

        //Monitoring Device Errors & Production Rate
        OpcSubscribeDataChange[] monitoredNodes = new OpcSubscribeDataChange[]
        {
            new OpcSubscribeDataChange("ns=2;s=" + selectedDevice + "/DeviceError", HandleDataChangedDeviceError),
            new OpcSubscribeDataChange("ns=2;s=" + selectedDevice + "/ProductionRate", HandleDataChangedProductionRate)
        };
        OpcSubscription subscription = opcClient.SubscribeNodes(monitoredNodes);
        subscription.PublishingInterval = 500; //Interval value in ms
        subscription.ApplyChanges(); //Alway call it after modifying the sub, otherwise server won't know the new sub config

        //Main loop
        Console.WriteLine("\nWorking...");
        await vDevice.SendMessage(telemetry, 2000);
        while (true) { }
    }
    catch (Exception e)
    {
        Console.WriteLine("Failed to connect to server. Press anything to exit.");
    }
}