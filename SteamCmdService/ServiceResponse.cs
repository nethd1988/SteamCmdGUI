using System;

namespace SteamCmdCommon
{
    [Serializable]
    public class ServiceResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Data { get; set; }

        public ServiceResponse()
        {
            Success = false;
            Message = string.Empty;
            Data = string.Empty;
        }
    }
}