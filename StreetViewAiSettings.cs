using System;
using System.IO;
using Newtonsoft.Json;

namespace AVMap
{
    public class StreetViewAiSettings
    {
        public string Provider { get; set; } = "BuiltIn"; // "BuiltIn", "GoogleGemini", "Clipdrop"
        public string GoogleApiKey { get; set; } = "";
        public string ClipdropApiKey { get; set; } = "";
        public bool AutoCleanLogo { get; set; } = true;
        public bool RemoveRoadWatermark { get; set; } = true;
        public bool AddSurveyStamp { get; set; } = false;
        public bool IncludeTimestamp { get; set; } = false;
        public string ProjectName { get; set; } = "KHAO SAT TUYEN";

        private static readonly string SettingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AVMap", "streetview_ai_settings.json");

        private static StreetViewAiSettings _instance;

        public static StreetViewAiSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Load();
                }
                return _instance;
            }
        }

        public static StreetViewAiSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    var settings = JsonConvert.DeserializeObject<StreetViewAiSettings>(json);
                    if (settings != null) return settings;
                }
            }
            catch { }
            return new StreetViewAiSettings();
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }
    }
}
