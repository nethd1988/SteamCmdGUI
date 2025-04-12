using System;

namespace SteamCmdCommon
{
    [Serializable]
    public enum CommandType
    {
        RunProfile,
        StopProfile,
        RunAllProfiles,
        GetStatus,
        UpdateConfig
    }

    [Serializable]
    public class ServiceCommand
    {
        public CommandType CommandType { get; set; }
        public string ProfileName { get; set; }
        public string Data { get; set; }

        public ServiceCommand()
        {
            ProfileName = string.Empty;
            Data = string.Empty;
        }
    }
}