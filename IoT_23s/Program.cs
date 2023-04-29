using Utils;
using Microsoft.Azure.Devices.Client;
using Opc.UaFx.Client;
using Opc.UaFx;

Console.WriteLine("Enter Azure device connection string:"); //placeholder
string deviceConnectionString = Console.ReadLine();

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
        using var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Mqtt);
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