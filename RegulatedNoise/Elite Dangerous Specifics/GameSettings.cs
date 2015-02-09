﻿using System;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace RegulatedNoise
{
    //GameSettings class interfaces with the actual Game configuration files.
    //Note only needed functions and properties are loaded.
    
    public class GameSettings
    {
        public AppConfig AppConfig;
        public EdDisplayConfig Display;
        
        public GameSettings()
        {
            try
            {
                //Load DisplaySettings from AppData
                LoadDisplaySettings();

                //Load AppConfig
                LoadAppConfig();

                //Set up some filewatchers, If user changes config its reflected here
                WatcherDisplaySettings();
                WatcherAppDataSettings(); //Currently disabled as we only check Verbose logging and that cant be changed from the game

                //Check and Request for Verbose Logging
                CheckAndRequestVerboseLogging();
            }
            catch (Exception ex)
            {
                cErr.processError(ex, "Error in GameSettings");
            }
        }

        void CheckAndRequestVerboseLogging()
        {
            if (AppConfig.Network.VerboseLogging != 1)
            {
                var setLog =
                    MessageBox.Show(
                        "Verbose logging isn't set in your Elite Dangerous AppConfig.xml, so I can't read system names. Would you like me to set it for you?",
                        "Set verbose logging?", MessageBoxButtons.YesNo);

                if (setLog == DialogResult.Yes)
                {
                    var appconfig = Path.Combine(Form1.RegulatedNoiseSettings.GamePath, "AppConfig.xml");

                    //Make backup
                    File.Copy(appconfig, appconfig+".bak", true);

                    //Set werbose to one
                    var doc = new XmlDocument();
                    doc.Load(appconfig);
                    var ie = doc.SelectNodes("/AppConfig/Network").GetEnumerator();

                    while (ie.MoveNext())
                    {
                        if ((ie.Current as XmlNode).Attributes["VerboseLogging"] != null)
                        {
                            (ie.Current as XmlNode).Attributes["VerboseLogging"].Value = "1";
                        }
                        else
                        {
                            var verb = doc.CreateAttribute("VerboseLogging");
                            verb.Value = "1";

                            (ie.Current as XmlNode).Attributes.Append(verb);
                        }
                    }

                    doc.Save(appconfig);

                    MessageBox.Show(
                        "AppConfig.xml updated.  You'll need to restart Elite Dangerous if it's already running.");
                }

                //Update config
                LoadAppConfig();
            }
        }

        void LoadAppConfig()
        {
            try
            {
                var configFile = Path.Combine(Form1.RegulatedNoiseSettings.GamePath, "AppConfig.xml");
                var serializer = new XmlSerializer(typeof(AppConfig));
                using (var myFileStream = new FileStream(configFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    AppConfig = (AppConfig)serializer.Deserialize(myFileStream);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error in LoadAppConfig()", ex);
            }
                
        }

        private void AppData_Changed(object sender, FileSystemEventArgs e)
        {
            try
            {
                LoadAppConfig();
            }
            catch (Exception ex)
            {
                cErr.processError(ex, "Error in AppData_Changed()");
            }
        }

        void LoadDisplaySettings()
        {
            var configFile = Path.Combine(Form1.RegulatedNoiseSettings.ProductAppData, "Graphics" ,"DisplaySettings.xml");
            if (!File.Exists(configFile))
            {
                return;
            }
            var serializer = new XmlSerializer(typeof(EdDisplayConfig));

            try
            {
                using (var myFileStream = new FileStream(configFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    Display = (EdDisplayConfig)serializer.Deserialize(myFileStream);
                }
            }
            catch (Exception ex)
            {
                if (ex.GetBaseException().HResult == (int)-2146232000)
                {
                    // System.Xml.XmlException : no root element (ED is rewriting the file sometimes ?) - ignore
                    return;
                }
                else
                {
                    cErr.processError(ex, "Error in Function \"LoadDisplaySettings\"");
                }
                
            }
        }

        private void LoadDisplaySettings(object sender, FileSystemEventArgs e)
        {
            LoadDisplaySettings();
        }

        private readonly FileSystemWatcher _displayWatcher = new FileSystemWatcher();
        void WatcherDisplaySettings()
        {
            var path = Path.Combine(Form1.RegulatedNoiseSettings.ProductAppData, "Graphics");
            if (!Directory.Exists(path) || !File.Exists(Path.Combine(path, "DisplaySettings.xml")))
                return;

            _displayWatcher.Path = path;
            _displayWatcher.Filter = "DisplaySettings.xml";
            _displayWatcher.NotifyFilter = NotifyFilters.LastWrite;
            _displayWatcher.Changed += LoadDisplaySettings;
            _displayWatcher.EnableRaisingEvents = true;
        }

        private readonly FileSystemWatcher _appdataWatcher = new FileSystemWatcher();
        void WatcherAppDataSettings()
        {
            _appdataWatcher.Path = Form1.RegulatedNoiseSettings.GamePath;
            _appdataWatcher.Filter = "AppConfig.xml";
            _appdataWatcher.NotifyFilter = NotifyFilters.LastWrite;
            _appdataWatcher.Changed += AppData_Changed;
            _appdataWatcher.EnableRaisingEvents = false; //Set to TRUE to enable watching!
        }

    }
}
