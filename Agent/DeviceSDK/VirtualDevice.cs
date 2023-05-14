using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Opc.UaFx.Client;
using System.Net.Mime;
using System.Text;

namespace DeviceSDK
{
    public class VirtualDevice
    {
        private readonly DeviceClient deviceClient;
        private readonly OpcClient opcClient;

        [Flags]
        public enum DeviceErrors
        {
            None = 0,
            Emergency_stop = 1,
            Power_failure = 2,
            Sensor_failure = 4,
            Unknown = 8
        }

        public int ProductionRate { get; set; } = 0;
        public DeviceErrors Errors { get; set; } = DeviceErrors.None;
        public string DeviceId { get; set; } = string.Empty;
        public double[] NormalTemperatureRange { get; }

        public VirtualDevice(DeviceClient deviceClient, OpcClient opcClient, double[] normalTemperatureRange)
        {
            this.deviceClient = deviceClient;
            this.opcClient = opcClient;
            NormalTemperatureRange = normalTemperatureRange;
        }

        #region Sending Messages
        public async Task SendMessage(object data)
        {
            var dataString = JsonConvert.SerializeObject(data);
            Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataString));
            eventMessage.ContentType = MediaTypeNames.Application.Json;
            eventMessage.ContentEncoding = "utf-8";

            await deviceClient.SendEventAsync(eventMessage);
        }
        #endregion

        #region Receiving Messages
        private async Task OnC2dMessageReceivedAsync(Message receivedMessage, object _)
        {
            Console.WriteLine($"\t{DateTime.Now}> C2D message callback - message received with Id={receivedMessage.MessageId}");
            PrintMessages(receivedMessage);
            await deviceClient.CompleteAsync(receivedMessage);
            Console.WriteLine($"\t{DateTime.Now}> Completed C2D message with Id = {receivedMessage.MessageId}.");

            receivedMessage.Dispose();
        }

        private void PrintMessages(Message receivedMessage)
        {
            string messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
            Console.WriteLine($"\t\tReceived message: {messageData}");

            int propCount = 0;
            foreach (var prop in receivedMessage.Properties)
            {
                Console.WriteLine($"\t\tProperty[{propCount++}]> Key={prop.Key} : Value={prop.Value}");
            }
        }
        #endregion

        #region Direct methods

        private async Task<MethodResponse> DefaultServiceHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"\tMETHOD EXECUTED: {methodRequest.Name}");

            await Task.Delay(1000);

            return new MethodResponse(0);
        }

        private async Task<MethodResponse> EmergencyStop(MethodRequest methodRequest, object userContext)
        {
            opcClient.CallMethod(DeviceId, $"{DeviceId}/EmergencyStop");
            Console.WriteLine($"Direct Method\t\t\t{DeviceId}\t\tEmergencyStop");
            return new MethodResponse(0);
        }

        private async Task<MethodResponse> ResetErrorStatus(MethodRequest methodRequest, object userContext)
        {
            Errors = DeviceErrors.None;
            opcClient.CallMethod(DeviceId, $"{DeviceId}/ResetErrorStatus");
            Console.WriteLine($"Direct Method\t\t\t{DeviceId}\t\tResetErrorStatus");
            return new MethodResponse(0);
        }
        #endregion

        #region Device Twin
        public async Task UpdateTwinErrorsAsync(DeviceErrors deviceErrors)
        {
            var reportedProperties = new TwinCollection();
            reportedProperties["DeviceErrors"] = deviceErrors;

            await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }

        public async Task UpdateTwinProductionRateAsync(int productionRate)
        {
            var reportedProperties = new TwinCollection();
            reportedProperties["ProductionRate"] = productionRate;

            await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }

        private async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object _)
        {
            if (desiredProperties["ProductionRate"] != ProductionRate)
            {
                ProductionRate = desiredProperties["ProductionRate"];
                TwinCollection twinCollection = new TwinCollection();
                twinCollection["ProductionRate"] = ProductionRate;
                opcClient.WriteNode($"{DeviceId}/ProductionRate", ProductionRate);
                await deviceClient.UpdateReportedPropertiesAsync(twinCollection);
                Console.WriteLine($"{DeviceId}: ProductionRate zmienione na {ProductionRate}");
            }
        }
        #endregion

        public async Task InitializeHandlers()
        {
            await deviceClient.SetReceiveMessageHandlerAsync(OnC2dMessageReceivedAsync, deviceClient);
            await deviceClient.SetMethodDefaultHandlerAsync(DefaultServiceHandler, deviceClient);
            await deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, deviceClient);
            await deviceClient.SetMethodHandlerAsync("EmergencyStop", EmergencyStop, deviceClient);
            await deviceClient.SetMethodHandlerAsync("ResetErrorStatus", ResetErrorStatus, deviceClient);
        }
    }
}