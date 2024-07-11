//----------------------------------------------------------------------------
//  Copyright (C) 2024 by SCADA Solve LLC. All rights reserved.              
//----------------------------------------------------------------------------
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Case
{
    /// <summary>
    /// Manages global parameters and settings for the application.
    /// </summary>
    public class GlobalParameters
    {
        public string lastExec = "";
        public string COS = "";
        public string[] serverSettings;
        public string[] defaultServerSettings;
        public string[] clientSettings;
        public string[] defaultClientSettings;
        private readonly HashSet<string> validStorageMethods = new HashSet<string> { "FlatFile", "Database" };
        private readonly HashSet<string> validBooleanValues = new HashSet<string> { "True", "False" };

        /// <summary>
        /// Initializes the settings configuration.
        /// </summary>
        /// <param name="defaultSettings">If true, returns default settings; otherwise, returns current settings.</param>
        /// <returns>An array of settings.</returns>
        public string[] SettingsConfiguration(bool defaultSettings = false)
        {
            serverSettings = new string[]
            {
                "SFile",
                "ThreadCount",
                "GPU",
                "Model",
                "BaseDirectoryPath",
                "TemporaryScreenCapturePath",
                "StorageMethod",
                "FlatFilePath",
                "ImageStorageDirectory",
                "DisplayAscii",
                "InterfacingStructure",
                "RPAStorageStructure",
                "LoggingStructure",
                "MonitoringStructure",
                "SessionsStructure",
                "SQLConnectionString",
                "ConversationalTrainingModel",
                "ConversationalTrainingModelData",
                "AnalysisTrainingModel",
                "AnalysisTrainingModelData",
                "CaseStable",
                "BypassLLM"
            };
            defaultServerSettings = new string[]
            {
                "logConfig.dat",
                "500",
                "",
                "mistral-7b-instruct-v0.1.Q4_0.gguf",
                AppDomain.CurrentDomain.BaseDirectory,
                AppDomain.CurrentDomain.BaseDirectory,
                "FlatFile",
                AppDomain.CurrentDomain.BaseDirectory,
                AppDomain.CurrentDomain.BaseDirectory + "\\find\\",
                "True",
                @"\Conversations\:[Username],[Message],[Screen],[DT],[Receiver],[Submitted],[WaitingOnResponse],[Answered],[ActionPerformed],[TimeToProcess],[CommandsActivated],[id]",
                @"\RPA\:[Command],[codeToExecute],[Software],[LastUsed],[Dynamic]",
                @"\Logs\:[sessionid],[logmessage]",
                @"\Monitoring_Queue\:[Datapoint],[startTime],[endTime],[Parameter],[AlarmValue],[RPA]",
                @"\CaseSessions\:[session],[startDT],[endDT],[username]",
                "",
                AppDomain.CurrentDomain.BaseDirectory.Replace("\\", "\\\\") + @"\\initCaseTemplate.py",
                AppDomain.CurrentDomain.BaseDirectory.Replace("\\", "\\\\") + @"\\initCase.Conversational.txt",
                AppDomain.CurrentDomain.BaseDirectory.Replace("\\", "\\\\") + @"\\initAnalysisTemplate.py",
                AppDomain.CurrentDomain.BaseDirectory.Replace("\\", "\\\\") + @"\\initCase.Analysis.Empty.txt",
                "False",
                "False"
            };
            return defaultSettings ? defaultServerSettings : serverSettings;
        }

        /// <summary>
        /// Updates a specific setting value in the current configuration.
        /// </summary>
        /// <param name="set">The setting key to update.</param>
        /// <param name="val">The new value for the setting.</param>
        public void updateSetting(string set, string val)
        {
            for (int i = 0; i < serverSettings.Length; i++)
            {
                if (serverSettings[i] == set)
                {
                    defaultServerSettings[i] = val;
                }
            }
        }

        /// <summary>
        /// Updates a specific setting in the settings file.
        /// </summary>
        /// <param name="set">The setting key to update.</param>
        /// <param name="val">The new value for the setting.</param>
        /// <returns>True if the setting is updated successfully; otherwise, false.</returns>
        public bool updateSettingsFile(string set, string val)
        {

            if (ValidateSetting(set, val))
            {
                try
                {
                    string basePath = AppDomain.CurrentDomain.BaseDirectory;
                    string filePath = Path.Combine(basePath, "Settings.config");

                    // Read all lines from the file
                    string[] lines = File.ReadAllLines(filePath);

                    for (int i = 0; i < lines.Length; i++)
                    {
                        // Find the index of the first '='
                        int indexOfEquals = lines[i].IndexOf('=');

                        if (indexOfEquals > 0)
                        {
                            // Extract the key and trim any whitespace
                            string key = lines[i].Substring(0, indexOfEquals).Trim();

                            if (key == set)
                            {
                                // Update the value, preserving everything after the first '='
                                lines[i] = $"{key}={val}";
                            }
                        }
                    }

                    // Write the updated lines back to the file
                    File.WriteAllLines(filePath, lines);
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating settings file: {ex.Message}");
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the value of a specific setting.
        /// </summary>
        /// <param name="set">The setting key to retrieve.</param>
        /// <returns>The value of the setting.</returns>
        public string getSetting(string set)
        {
            string result = "";
            try 
            { 
                loadSettingsFile();
                for (int i = 0; i < serverSettings.Length; i++)
                {
                    if (serverSettings[i] == set)
                    {
                        result = defaultServerSettings[i];
                    }
                }
            }
            catch (Exception e)
            {
                result = "Error";
            }

            return result;
        }

        /// <summary>
        /// Loads the settings file into the current configuration.
        /// </summary>
        public void loadSettingsFile()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string[] set = SettingsConfiguration();
            string[] val = SettingsConfiguration(true);
            string filePath = basePath + "Settings.config";
            try
            {
                if (!File.Exists(filePath))
                {
                    using (StreamWriter sw = File.CreateText(filePath))
                    {
                        for (int i = 0; i < set.Length; i++)
                        {
                            sw.WriteLine(set[i] + "=" + val[i]);
                        }
                    }
                }
                else
                {
                    using (StreamReader sr = File.OpenText(filePath))
                    {
                        string s = "";
                        while ((s = sr.ReadLine()) != null)
                        {
                            int separatorIndex = s.IndexOf('=');
                            if (separatorIndex > 0)
                            {
                                string key = s.Substring(0, separatorIndex).Trim();
                                string value = s.Substring(separatorIndex + 1).Trim();
                                updateSetting(key, value);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings file: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates a specific setting key and value.
        /// </summary>
        /// <param name="key">The setting key to validate.</param>
        /// <param name="value">The setting value to validate.</param>
        /// <returns>True if the setting is valid; otherwise, false.</returns>
        private bool ValidateSetting(string key, string value)
        {
            try
            {
                switch (key)
                {
                    case "BypassLLM":
                        if (!validBooleanValues.Contains(value))
                        {
                            throw new ArgumentException("BypassLLM must be 'True' or 'False'.");
                        }
                        return true;
                    case "CaseStable":
                        if (!validBooleanValues.Contains(value))
                        {
                            throw new ArgumentException("CaseStable must be 'True' or 'False'.");
                        }
                        return true;
                    case "ThreadCount":
                        if (!int.TryParse(value, out _))
                        {
                            throw new ArgumentException("ThreadCount must be a numeric value.");
                        }
                        return true;

                    case "StorageMethod":
                        if (!validStorageMethods.Contains(value))
                        {
                            throw new ArgumentException("StorageMethod must be either 'FlatFile' or 'Database'.");
                        }
                        return true;

                    case "GPU":
                        return true;

                    case "DisplayAscii":
                        if (!validBooleanValues.Contains(value))
                        {
                            throw new ArgumentException("DisplayAscii must be 'True' or 'False'.");
                        }
                        return true;

                    case "SQLConnectionString":
                        return true;

                    default:
                        // For paths, ensure they exist if it's a folder or file path
                        if (key.EndsWith("Path") || key.EndsWith("Directory"))
                        {
                            if (!Directory.Exists(value) && !File.Exists(value))
                            {
                                throw new ArgumentException($"{key} must be a valid path.");
                            }
                            return true;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating setting: {ex.Message}");
                return false;
            }
            return false;
        }
    }
}
