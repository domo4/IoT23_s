using Utils;
using Microsoft.Azure.Devices.Client;
using Opc.UaFx.Client;
using Opc.UaFx;

Console.WriteLine("Enter Azure device connection string:"); //placeholder
string deviceConnectionString = Console.ReadLine();

using (var client = new OpcClient("opc.tcp://localhost:4840/"))
{
    try
    {
        client.Connect();
        Console.WriteLine("Connection success");

        //Create a list of all devices
        List<string> deviceList = new List<string>();
        var node = client.BrowseNode(OpcObjectTypes.ObjectsFolder); //Start from the 'root' of all Nodes of the Server
        foreach (var childNode in node.Children())
        {
            if (childNode.DisplayName.Value != "Server")
            {
                deviceList.Add(childNode.DisplayName.Value);
            }
        }

        //Display the list of devices
        Console.WriteLine("Available devices:");
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
        var vdevice = new VirtualDevice(deviceClient, client, selectedDevice);
        await vdevice.InitializeHandlers();

        //Main loop
        Console.WriteLine("\nWorking...");
        while (true) { }
    }
    catch (Exception e)
    {
        Console.WriteLine("Failed to connect to server. Press anything to exit.");
    }
}