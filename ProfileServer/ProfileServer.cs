using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SteamCmdApp
{
    public class ProfileServer
    {
        private TcpListener listener;
        private bool isRunning;
        private readonly Dictionary<string, string> profiles = new Dictionary<string, string>
        {
            { "Machine1", "profile1|user1|password1" },
            { "Machine2", "profile2|user2|password2" }
        };

        public void Start(int port)
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                isRunning = true;
                Console.WriteLine($"Server started on port {port}");

                while (isRunning)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Thread clientThread = new Thread(HandleClient);
                    clientThread.Start(client);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error starting server: " + ex.Message);
            }
        }

        public void Stop()
        {
            isRunning = false;
            listener.Stop();
        }

        private void HandleClient(object obj)
        {
            TcpClient client = (TcpClient)obj;
            NetworkStream stream = null;

            try
            {
                stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string request = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                if (profiles.TryGetValue(request, out string profileData))
                {
                    byte[] response = Encoding.UTF8.GetBytes(profileData);
                    stream.Write(response, 0, response.Length);
                }
                else
                {
                    byte[] response = Encoding.UTF8.GetBytes("Profile not found");
                    stream.Write(response, 0, response.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error handling client: " + ex.Message);
            }
            finally
            {
                stream?.Dispose();
                client.Close();
            }
        }

        public static void Main(string[] args)
        {
            ProfileServer server = new ProfileServer();
            server.Start(12345);
        }
    }
}