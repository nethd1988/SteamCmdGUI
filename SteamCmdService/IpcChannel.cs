using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

namespace SteamCmdCommon
{
    public class IpcChannel
    {
        private const string PipeName = "SteamCmdServicePipe";

        // Client (GUI) gửi lệnh đến Service
        public static async Task<ServiceResponse> SendCommandAsync(ServiceCommand command)
        {
            try
            {
                using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
                {
                    await pipeClient.ConnectAsync(5000); // Timeout 5 giây

                    // Serialize lệnh
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(pipeClient, command);
                    pipeClient.Flush();

                    // Đọc phản hồi
                    ServiceResponse response = (ServiceResponse)formatter.Deserialize(pipeClient);
                    return response;
                }
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Success = false,
                    Message = $"Communication error: {ex.Message}"
                };
            }
        }

        // Server (Service) lắng nghe lệnh
        public static async Task<ServiceCommand> ReceiveCommandAsync(NamedPipeServerStream pipeServer)
        {
            try
            {
                await pipeServer.WaitForConnectionAsync();
                BinaryFormatter formatter = new BinaryFormatter();
                return (ServiceCommand)formatter.Deserialize(pipeServer);
            }
            catch (Exception)
            {
                return new ServiceCommand { CommandType = CommandType.GetStatus };
            }
        }

        // Server (Service) gửi phản hồi
        public static void SendResponse(NamedPipeServerStream pipeServer, ServiceResponse response)
        {
            try
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(pipeServer, response);
                pipeServer.Flush();
            }
            catch (Exception)
            {
                // Xử lý lỗi nếu cần
            }
        }
    }
}