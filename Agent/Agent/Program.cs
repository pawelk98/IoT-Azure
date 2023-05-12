using Microsoft.Azure.Devices.Client;
using DeviceSDK;
using Opc.UaFx.Client;
using Opc.UaFx;

string deviceConnectionString = "HostName=zajecia2023.azure-devices.net;DeviceId=Device1;SharedAccessKey=eVc1/BofDrZdvWYa8JBrBVmMyTLPmtSoxZy1WXE05MA=";
List<string> deviceIds = new List<string>();

try
{
    using (var client = new OpcClient("opc.tcp://localhost:4840"))
    {
        client.Connect();
        Console.WriteLine("Client is connected.");

        var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString);
        await deviceClient.OpenAsync();
        var device = new VirtualDevice(deviceClient);
        Console.WriteLine("Połączenia udane");
        await device.InitializeHandlers();
        Console.WriteLine("Inicjalizacja udana");

        var node = client.BrowseNode(OpcObjectTypes.ObjectsFolder);
        SetDeviceIds(node, deviceIds);

        while(true)
        {
            await SendTelemetry(client, deviceIds[0], device);
            await Task.Delay(1000);
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}

async Task SendTelemetry(OpcClient client, string deviceId, VirtualDevice device)
{
    var telemetry = new
    {
        production_status = client.ReadNode($"{deviceId}/ProductionStatus").Value,
        workorder_id = client.ReadNode($"{deviceId}/WorkorderId").Value,
        goodcount = client.ReadNode($"{deviceId}/GoodCount").Value,
        badcount = client.ReadNode($"{deviceId}/BadCount").Value,
        temperature = client.ReadNode($"{deviceId}/Temperature").Value
    };

    await device.SendMessage(telemetry);
}

void SetDeviceIds(OpcNodeInfo node, List<string> deviceIds, int level = 0)
{
    if (level == 1 && node.NodeId.ToString().Contains("Device"))
        deviceIds.Add(node.NodeId.ToString());
    level++;
    foreach (var child in node.Children())
        SetDeviceIds(child, deviceIds, level);
}