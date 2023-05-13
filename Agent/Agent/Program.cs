using Microsoft.Azure.Devices.Client;
using DeviceSDK;
using Opc.UaFx.Client;
using Opc.UaFx;
using Microsoft.Azure.Devices;


List<string> deviceConnectionStrings = new List<string>();
deviceConnectionStrings.Add("HostName=zajecia2023.azure-devices.net;DeviceId=Device1;SharedAccessKey=eVc1/BofDrZdvWYa8JBrBVmMyTLPmtSoxZy1WXE05MA=");
deviceConnectionStrings.Add("HostName=zajecia2023.azure-devices.net;DeviceId=Device2;SharedAccessKey=kQ9xHtbIHJFIgoZNTCDaFrNdBF5jbNxZvszySxSNFrQ=");
List<string> deviceIds = new List<string>();
List<VirtualDevice.DeviceErrors> currentErrors = new List<VirtualDevice.DeviceErrors>();
List<int> currentProductionRates = new List<int>();

for(int i = 0; i < deviceConnectionStrings.Count; i++)
{
    currentErrors.Add(VirtualDevice.DeviceErrors.None);
    currentProductionRates.Add(0);
}

try
{
    using (var client = new OpcClient("opc.tcp://localhost:4840"))
    {
        client.Connect();
        Console.WriteLine("Client is connected");

        List<DeviceClient> deviceClients = new List<DeviceClient>();
        List<VirtualDevice> devices = new List<VirtualDevice>();

        Console.WriteLine("Setting up device clients");
        for(int i = 0; i < deviceConnectionStrings.Count; i++)
        {
            deviceClients.Add(DeviceClient.CreateFromConnectionString(deviceConnectionStrings[i]));
            await deviceClients[i].OpenAsync();
            devices.Add(new VirtualDevice(deviceClients[i]));
            await devices[i].InitializeHandlers();
        }
        Console.WriteLine("Device clients OK");

        var node = client.BrowseNode(OpcObjectTypes.ObjectsFolder);
        SetDeviceIds(node, deviceIds);

        while(true)
        {
            for(int i = 0; i < deviceIds.Count && i < devices.Count; i++)
            {
                await SendTelemetry(client, deviceIds[i], devices[i]);
                currentErrors[i] = await CheckErrors(client, deviceIds[i], devices[i], currentErrors[i]);
            }

            Console.WriteLine("Telemetry data sent");
            await Task.Delay(5000);
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

async Task<VirtualDevice.DeviceErrors> CheckErrors(OpcClient client, string deviceId, VirtualDevice device, VirtualDevice.DeviceErrors previousErrorState)
{
    VirtualDevice.DeviceErrors device_errors = (VirtualDevice.DeviceErrors)client.ReadNode($"{deviceId}/DeviceError").Value;

    var errors = new { device_errors };

    if(previousErrorState != device_errors)
    {
        Console.WriteLine($"New errors: {device_errors}");
        await device.SendMessage(errors);
    }

    return device_errors;
}

void SetDeviceIds(OpcNodeInfo node, List<string> deviceIds, int level = 0)
{
    if (level == 1 && node.NodeId.ToString().Contains("Device"))
        deviceIds.Add(node.NodeId.ToString());
    level++;
    foreach (var child in node.Children())
        SetDeviceIds(child, deviceIds, level);
}