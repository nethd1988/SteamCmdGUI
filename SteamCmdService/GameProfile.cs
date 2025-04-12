using System;
using System.IO;
using System.Xml.Serialization;

namespace SteamCmdCommon
{
    [Serializable]
    public class GameProfile
    {
        public string ProfileName { get; set; }
        public string InstallDir { get; set; }
        public string EncryptedUsername { get; set; }
        public string EncryptedPassword { get; set; }
        public string AppID { get; set; }
        public string Arguments { get; set; }

        public GameProfile()
        {
            ProfileName = "";
            InstallDir = "";
            EncryptedUsername = "";
            EncryptedPassword = "";
            AppID = "";
            Arguments = "-norepairfiles -noverifyfiles";
        }

        public void SaveToFile(string filePath)
        {
            try
            {
                // Đảm bảo thư mục tồn tại
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                XmlSerializer serializer = new XmlSerializer(typeof(GameProfile));
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    serializer.Serialize(writer, this);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi lưu profile: {ex.Message}", ex);
            }
        }

        public static GameProfile LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(GameProfile));
                using (StreamReader reader = new StreamReader(filePath))
                {
                    return (GameProfile)serializer.Deserialize(reader);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi đọc profile: {ex.Message}", ex);
            }
        }
    }
}