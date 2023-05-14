using Microsoft.Azure.Devices.Client;
using DeviceSDK;
using Opc.UaFx.Client;
using Opc.UaFx;
using Newtonsoft.Json;

ConfigJsonFile config = ReadConfigFile("..\\..\\..\\config.json");
Console.WriteLine("Config OK");

List<DeviceClient> deviceClients = new List<DeviceClient>();
List<VirtualDevice> devices = new List<VirtualDevice>();
List<string> deviceIds = new List<string>();

try
{
    using (var client = new OpcClient(config.opc_server_adress))
    {
        client.Connect();
        Console.WriteLine("Client is connected");

        Console.WriteLine("Setting up device clients");
        for(int i = 0; i < config.device_connection_strings.Length; i++)
        {
            deviceClients.Add(DeviceClient.CreateFromConnectionString(config.device_connection_strings[i]));
            await deviceClients[i].OpenAsync();
            devices.Add(new VirtualDevice(deviceClients[i], client, config.normal_temperature_range));
            await devices[i].InitializeHandlers();
        }
        Console.WriteLine("Device clients OK");

        var node = client.BrowseNode(OpcObjectTypes.ObjectsFolder);
        SetDeviceIds(node, deviceIds);

        for(int i = 0; i < devices.Count; i++)
            devices[i].DeviceId = deviceIds[i];

        Console.WriteLine("Devices connected:");
        foreach(var deviceId in deviceIds)
            Console.WriteLine($"\t{deviceId}");

        while(true)
        {
            for(int i = 0; i < deviceIds.Count && i < devices.Count; i++)
            {
                await SendTelemetry(client, devices[i]);
                Console.WriteLine($"Telemetry data sent\t\t{deviceIds[i]}");
                await CheckErrors(client, devices[i]);
                await CheckProductionRate(client, devices[i]);
            }

            await Task.Delay(config.telemetry_data_send_interval);
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}

async Task SendTelemetry(OpcClient client, VirtualDevice device)
{
    var telemetry = new
    {
        production_status = client.ReadNode($"{device.DeviceId}/ProductionStatus").Value,
        workorder_id = client.ReadNode($"{device.DeviceId}/WorkorderId").Value,
        goodcount = client.ReadNode($"{device.DeviceId}/GoodCount").Value,
        badcount = client.ReadNode($"{device.DeviceId}/BadCount").Value,
        temperature = client.ReadNode($"{device.DeviceId}/Temperature").Value
    };

    await device.SendMessage(telemetry);
}

async Task CheckErrors(OpcClient client, VirtualDevice device)
{
    VirtualDevice.DeviceErrors device_errors = (VirtualDevice.DeviceErrors)client.ReadNode($"{device.DeviceId}/DeviceError").Value;

    var errors = new { device_errors };

    if(device.Errors != device_errors)
    {
        Console.WriteLine($"Errors\t\t\t\t{device.DeviceId}\t\t{device_errors}");
        await device.SendMessage(errors);
        await device.UpdateTwinErrorsAsync(device_errors);
        device.Errors = device_errors;
    }
}

async Task CheckProductionRate(OpcClient client, VirtualDevice device)
{
    int production_rate = (int)client.ReadNode($"{device.DeviceId}/ProductionRate").Value;

    if (device.ProductionRate != production_rate)
    {
        Console.WriteLine($"Production rate change\t\t{device.DeviceId}\t\t{production_rate}");
        await device.UpdateTwinProductionRateAsync(production_rate);
        device.ProductionRate = production_rate;
    }
}

void SetDeviceIds(OpcNodeInfo node, List<string> deviceIds, int level = 0)
{
    if (level == 1 && node.NodeId.ToString().Contains("Device"))
        deviceIds.Add(node.NodeId.ToString());
    level++;
    foreach (var child in node.Children())
        SetDeviceIds(child, deviceIds, level);
}

ConfigJsonFile ReadConfigFile(string path)
{
    StreamReader r = new StreamReader(path);
    string configFileContent = r.ReadToEnd();
    var configJsonFIile = JsonConvert.DeserializeObject<ConfigJsonFile>(configFileContent);
    return configJsonFIile;
}

public class ConfigJsonFile
{
    public string opc_server_adress { get; set; }
    public string[] device_connection_strings { get; set; }
    public int telemetry_data_send_interval { get; set; }
    public double[] normal_temperature_range { get; set; }
}