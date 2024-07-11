//----------------------------------------------------------------------------
//  Copyright (C) 2024 by SCADA Solve LLC. All rights reserved.              
//----------------------------------------------------------------------------
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Case
{
    public class FlatFileManager
    {
        public static string[] CommandDescriptions = new string[]
        {
            "Enter new command:",
            "Commands can be multiple words, but its best to make them with no spaces.",
            "E.g. (openWindows, runScriptExample, loadNewDevice)",

            "Enter keywords:",
            "Keywords should be comma separated words, that you can detect in a sentence.",
            "E.g. (\"FIT-1044,Test,Separator\" OR \"Windows,Kepware,Allen-Bradley\") without quotes.",

            "Enter code to execute:",
            "This is RPA Code, so will follow the standard RPA structure built into Case.",
            "E.g. (\"e;sExplorer.exe{ENTER}\" OR \"fNextButton.png;fLoginButton.png\") without quotes.",

            "Enter software:",
            "This would be the software relative to the executing command, and is a filtering mechanism for the -view subcommand.",
            "E.g. (\"Windows\" OR \"Chrome\" OR \"Generic\") without quotes."
        };

        private string basePath;

        public FlatFileManager(string basePath)
        {
            this.basePath = basePath;
            EnsureDirectoriesExist();
        }
        public List<CaseCommand> GetCommandsFromFlatFile(string filePath)
        {
            List<CaseCommand> commands = new List<CaseCommand>();
            FlatFileManager flatFileManager = new FlatFileManager(filePath);
            var records = flatFileManager.ReadData<FF_CaseCommands>("CaseCommands");
            foreach (var record in records)
            {
                commands.Add(new CaseCommand
                {
                    Command = record.Command,
                    CodeToExecute = record.CodeToExecute,
                    Keywords = record.Keywords,
                    Software = record.Software,
                    LastUsed = record.LastUsed,
                    Dynamic = record.Dynamic
                });
            }
            return commands;
        }
        private void EnsureDirectoriesExist()
        {
            string[] folderNames = { "CaseCommands", "CaseInitializationLog", "CaseInteractions", "CaseLogs", "CaseSessions" };
            foreach (string folderName in folderNames)
            {
                string folderPath = Path.Combine(basePath, folderName);
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
            }
        }

        private string EnsureTodaysFileExists(string folderName)
        {
            string folderPath = Path.Combine(basePath, folderName);
            string filePath = folderName + "\\" + folderName.Split('\\').Last() + ".dat";
            if (!File.Exists(filePath))
            {
                File.Create(filePath).Dispose();
            }
            return filePath;
        }

        // Serialize and append data as raw bytes to the .dat file
        public void InsertData<T>(string folderName, T record)
        {
            string filePath = EnsureTodaysFileExists(folderName);
            string json = System.Text.Json.JsonSerializer.Serialize(record);
            byte[] data = Encoding.UTF8.GetBytes(json + Environment.NewLine);
            using (var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write))
            {
                fileStream.Write(data, 0, data.Length);
            }
        }

        public void UpdateSessionData<T>(string folderName, Guid session) where T : FF_CaseSessions
        {
            string filePath = EnsureTodaysFileExists(folderName);

            // Read all lines from the file
            var lines = File.ReadAllLines(filePath);
            var records = new List<T>();
            var before = records;
            // Deserialize each line into a record
            foreach (var line in lines)
            {
                var record = System.Text.Json.JsonSerializer.Deserialize<T>(line);
                if (record != null)
                {
                    records.Add(record);
                }
            }

            // Update the record with the matching GUID
            foreach (var record in records)
            {
                if (record.Session == session)
                {
                    record.EndDT = DateTime.Now;
                    break;
                }
            }

            // Serialize records back to the file
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                foreach (var record in records)
                {
                    string json = System.Text.Json.JsonSerializer.Serialize(record);
                    byte[] data = Encoding.UTF8.GetBytes(json + Environment.NewLine);
                    fileStream.Write(data, 0, data.Length);
                }
            }
        }
        public void InteractiveCreateCaseCommands()
        {
            Console.WriteLine(CommandDescriptions[0]);
            Console.WriteLine(CommandDescriptions[1]);
            Console.WriteLine(CommandDescriptions[2]);
            string command = Console.ReadLine();
            Console.WriteLine(CommandDescriptions[3]);
            Console.WriteLine(CommandDescriptions[4]);
            Console.WriteLine(CommandDescriptions[5]);
            string keywords = Console.ReadLine();
            Console.WriteLine(CommandDescriptions[6]);
            Console.WriteLine(CommandDescriptions[7]);
            Console.WriteLine(CommandDescriptions[8]);
            string codeToExecute = Console.ReadLine();
            Console.WriteLine(CommandDescriptions[9]);
            Console.WriteLine(CommandDescriptions[10]);
            Console.WriteLine(CommandDescriptions[11]);
            string software = Console.ReadLine();
            string lastUsedInput = "1970-01-01 00:00:00";
            //Save for later.
            //Console.WriteLine("Enter dynamic status (true/false):");
            string dynamicInput = "false";

            DateTime lastUsed = DateTime.TryParse(lastUsedInput, out DateTime lastUsedDate) ? lastUsedDate : new DateTime(1970, 1, 1);
            bool dynamic = bool.TryParse(dynamicInput, out bool dynamicStatus) ? dynamicStatus : false;

            string filePath = EnsureTodaysFileExists("CaseCommands");
            var record = new FF_CaseCommands
            {
                Command = command,
                Keywords = keywords,
                CodeToExecute = codeToExecute,
                Software = software,
                LastUsed = lastUsed,
                Dynamic = dynamic
            };

            InsertData("CaseCommands", record);

            Console.WriteLine("Command created successfully.");
        }
        public void InteractiveUpdateCaseCommands()
        {
            string filePath = EnsureTodaysFileExists("CaseCommands");
            var lines = File.ReadAllLines(filePath);
            var records = new List<FF_CaseCommands>();

            foreach (var line in lines)
            {
                var record = System.Text.Json.JsonSerializer.Deserialize<FF_CaseCommands>(line);
                if (record != null)
                {
                    records.Add(record);
                }
            }

            Console.WriteLine("Available Commands:");
            for (int i = 0; i < records.Count; i++)
            {
                Console.WriteLine($"{i + 1}: {records[i].Command}");
            }

            Console.WriteLine("Enter the number of the command you want to update or type 'cancel' to exit:");
            string input = Console.ReadLine();

            if (input.ToLower() == "cancel")
            {
                Console.WriteLine("Update canceled.");
                return;
            }

            if (int.TryParse(input, out int commandNumber) && commandNumber > 0 && commandNumber <= records.Count)
            {
                var recordToUpdate = records[commandNumber - 1];

                Console.WriteLine($"Selected Command: {recordToUpdate.Command}");
                Console.WriteLine("Enter new command name (leave blank to keep current):");
                Console.WriteLine("Current Value:" + recordToUpdate.Command);
                Console.WriteLine(CommandDescriptions[1]);
                Console.WriteLine(CommandDescriptions[2]);
                string newCommand = Console.ReadLine();
                Console.WriteLine("Enter new keywords (leave blank to keep current):");
                Console.WriteLine("Current Value:" + recordToUpdate.Keywords);
                Console.WriteLine(CommandDescriptions[4]);
                Console.WriteLine(CommandDescriptions[5]);
                string newKeywords = Console.ReadLine();
                Console.WriteLine("Enter new code to execute (leave blank to keep current):");
                Console.WriteLine("Current Value:" + recordToUpdate.CodeToExecute);
                Console.WriteLine(CommandDescriptions[7]);
                Console.WriteLine(CommandDescriptions[8]);
                string newCodeToExecute = Console.ReadLine();
                Console.WriteLine("Enter new software (leave blank to keep current):");
                Console.WriteLine("Current Value:" + recordToUpdate.Software);
                Console.WriteLine(CommandDescriptions[10]);
                Console.WriteLine(CommandDescriptions[11]);
                string newSoftware = Console.ReadLine();
                Console.WriteLine("'Last used' updated automatically to now.");
                //Save for later.
                //Console.WriteLine("Enter new dynamic status (true/false) (leave blank to keep current):");
                string newDynamic = "false";

                if (!string.IsNullOrWhiteSpace(newCommand)) recordToUpdate.Command = newCommand;
                if (!string.IsNullOrWhiteSpace(newKeywords)) recordToUpdate.Keywords = newKeywords;
                if (!string.IsNullOrWhiteSpace(newCodeToExecute)) recordToUpdate.CodeToExecute = newCodeToExecute;
                if (!string.IsNullOrWhiteSpace(newSoftware)) recordToUpdate.Software = newSoftware;
                if (DateTime.TryParse(DateTime.Now.ToString(), out DateTime lastUsedDate)) recordToUpdate.LastUsed = lastUsedDate;
                if (bool.TryParse(newDynamic, out bool dynamicStatus)) recordToUpdate.Dynamic = dynamicStatus;

                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    foreach (var record in records)
                    {
                        string json = System.Text.Json.JsonSerializer.Serialize(record);
                        byte[] data = Encoding.UTF8.GetBytes(json + Environment.NewLine);
                        fileStream.Write(data, 0, data.Length);
                    }
                }

                Console.WriteLine("Command updated successfully.");
            }
            else
            {
                Console.WriteLine("Invalid selection. Update canceled.");
            }
        }

        public void UpdateCommandLastUsed(string commandName)
        {
            string filePath = EnsureTodaysFileExists("CaseCommands");
            var lines = File.ReadAllLines(filePath);
            var records = new List<FF_CaseCommands>();

            foreach (var line in lines)
            {
                var record = System.Text.Json.JsonSerializer.Deserialize<FF_CaseCommands>(line);
                if (record != null)
                {
                    records.Add(record);
                }
            }

            var recordToUpdate = records.FirstOrDefault(r => r.Command.Equals(commandName, StringComparison.OrdinalIgnoreCase));

            if (recordToUpdate != null)
            {
                recordToUpdate.LastUsed = DateTime.Now;

                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    foreach (var record in records)
                    {
                        string json = System.Text.Json.JsonSerializer.Serialize(record);
                        byte[] data = Encoding.UTF8.GetBytes(json + Environment.NewLine);
                        fileStream.Write(data, 0, data.Length);
                    }
                }

                Console.WriteLine($"Last used date for command '{recordToUpdate.Command}' updated successfully.");
            }
            else
            {
                Console.WriteLine($"Command '{commandName}' not found.");
            }
        }

        /// <summary>
        /// Retrieves Case Command Properties from a flat file.
        /// </summary>
        /// <param name="command">The command to retrieve the code for.</param>
        /// <returns>A string of data corresponding with the command variables</returns>
        public object GetCommandPropertiesFromFile(string command)
        {
            string filePath = EnsureTodaysFileExists("CaseCommands");
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                var record = System.Text.Json.JsonSerializer.Deserialize<FF_CaseCommands>(line);
                if (record != null && record.Command == command)
                {
                    var cProps = new FF_CaseCommands
                    {
                        Command = record.Command,
                        Keywords = record.Keywords,
                        CodeToExecute = record.CodeToExecute,
                        Software = record.Software,
                        LastUsed = record.LastUsed,
                        Dynamic = record.Dynamic
                    };
                    return cProps;
                }
            }
            Console.WriteLine("No matching command found.");
            return null;
        }

        public string GetCodeToExecuteFromFile(string command)
        {
            string filePath = EnsureTodaysFileExists("CaseCommands");
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                var record = System.Text.Json.JsonSerializer.Deserialize<FF_CaseCommands>(line);
                if (record != null && record.Command == command)
                {
                    return record.CodeToExecute;
                }
            }
            Console.WriteLine("No matching command found.");
            return null;
        }

        // Update function for CaseInteractions
        public void UpdateCaseInteractions(Guid id, string username = null, string message = null, string screen = null, DateTime? dt = null, string receiver = null, bool? submitted = null, bool? waitingOnResponse = null, bool? answered = null, bool? actionPerformed = null, string timeToProcess = null, string commandsActivated = null)
        {
            string filePath = EnsureTodaysFileExists("CaseInteractions");
            var lines = File.ReadAllLines(filePath);
            var records = new List<FF_CaseInteractions>();

            foreach (var line in lines)
            {
                var record = System.Text.Json.JsonSerializer.Deserialize<FF_CaseInteractions>(line);
                if (record != null)
                {
                    records.Add(record);
                }
            }

            foreach (var record in records)
            {
                if (record.Id == id)
                {
                    if (username != null) record.Username = username;
                    if (message != null) record.Message = message;
                    if (screen != null) record.Screen = screen;
                    if (dt.HasValue) record.DT = dt.Value;
                    if (receiver != null) record.Receiver = receiver;
                    if (submitted.HasValue) record.Submitted = submitted.Value;
                    if (waitingOnResponse.HasValue) record.WaitingOnResponse = waitingOnResponse.Value;
                    if (answered.HasValue) record.Answered = answered.Value;
                    if (actionPerformed.HasValue) record.ActionPerformed = actionPerformed.Value;
                    if (timeToProcess != null) record.TimeToProcess = timeToProcess;
                    if (commandsActivated != null) record.CommandsActivated = commandsActivated;
                    break;
                }
            }

            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                foreach (var record in records)
                {
                    string json = System.Text.Json.JsonSerializer.Serialize(record);
                    byte[] data = Encoding.UTF8.GetBytes(json + Environment.NewLine);
                    fileStream.Write(data, 0, data.Length);
                }
            }
        }

        // Read and deserialize data from raw bytes in the .dat file
        public List<T> ReadData<T>(string folderName)
        {
            string filePath = EnsureTodaysFileExists(folderName);
            var records = new List<T>();
            var data = File.ReadAllBytes(filePath);
            string allText = Encoding.UTF8.GetString(data);
            var lines = allText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                T record = System.Text.Json.JsonSerializer.Deserialize<T>(line);
                records.Add(record);
            }
            return records;
        }
        private void DisplayRecords<T>(string folderName)
        {
            var records = ReadData<T>(folderName);
            foreach (var record in records)
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(record));
            }
        }

        public bool CheckFileSystemStability()
        {
            try
            {
                EnsureDirectoriesExist();
                string[] folderNames = { "CaseCommands", "CaseInitializationLog", "CaseInteractions", "CaseLogs", "CaseSessions" };
                foreach (string folder in folderNames)
                {
                    EnsureTodaysFileExists(folder);
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error ensuring file system stability: " + ex.Message);
                return false;
            }
        }
        public void InsertIntoCaseCommands(FF_CaseCommands record)
        {
            InsertData(basePath + "\\CaseCommands", record);
        }

        // Function to insert data into CaseInitializationLog file
        public void InsertIntoCaseInitializationLog(FF_CaseInitializationLog record)
        {
            InsertData(basePath + "\\CaseInitializationLog", record);
        }

        // Function to insert data into CaseInteractions file
        public void InsertIntoCaseInteractions(FF_CaseInteractions record)
        {
            InsertData(basePath + "\\CaseInteractions", record);
        }

        // Function to insert data into CaseLogs file
        public void InsertIntoCaseLogs(FF_CaseLogs record)
        {
            InsertData(basePath + "\\CaseLogs", record);
        }

        // Function to insert data into CaseSessions file
        public void InsertIntoCaseSessions(FF_CaseSessions record)
        {
            InsertData(basePath + "\\CaseSessions", record);
        }
        public void UpdateCaseSession(Guid id)
        {
            UpdateSessionData<FF_CaseSessions>(basePath + "\\CaseSessions", id);
        }
        public List<FF_CaseInteractions> GetPendingCaseInteractions(string receiver, DateTime lastCheckTime)
        {
            // Code to get pending case interactions from flat file
            return new List<FF_CaseInteractions>(); // Mocked list for demonstration
        }

        public void UpdateCaseInteraction(Guid id, string message, string username)
        {
            // Code to update case interaction in flat file
        }

        public void InsertCaseInteractionResponse(DateTime dt, string message, string username, string timeToRun, string response)
        {
            FF_CaseInteractions rec = new FF_CaseInteractions()
            {
                Username = username,
                Message = message,
                DT = dt,
                Receiver = username,
                WaitingOnResponse = false,
                Answered = true,
                ActionPerformed = false,
                TimeToProcess = timeToRun
            };
        }
        public Dictionary<string, float> DetectKeywordsInString(string input)
        {
            string filePath = EnsureTodaysFileExists("CaseCommands");
            var keywordMatches = new Dictionary<string, float>();

            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                var record = System.Text.Json.JsonSerializer.Deserialize<FF_CaseCommands>(line);
                if (record != null)
                {
                    string[] keywordArray = record.Keywords.Split(';');
                    int matchCount = 0;
                    foreach (var keyword in keywordArray)
                    {
                        if (input.Contains(keyword.Trim()))
                        {
                            matchCount++;
                        }
                    }

                    float matchPercentage = (float)matchCount / keywordArray.Length * 100;
                    if (matchPercentage > 0)
                    {
                        keywordMatches[record.Command] = matchPercentage;
                    }
                }
            }

            return keywordMatches
             .OrderByDescending(kv => kv.Value)
             .ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        public void EnqueueCommand(string commandName, string command, string user)
        {
            string filePath = EnsureTodaysFileExists("CommandQueue");
            var record = new CommandQueueRecord
            {
                CommandName = commandName,
                Command = command,
                User = user,
                Timestamp = DateTime.Now
            };
            var json = System.Text.Json.JsonSerializer.Serialize(record);
            File.AppendAllText(filePath, json + Environment.NewLine);
        }

        public string DequeueCommand()
        {
            string folderPath = Path.Combine(basePath, "CommandQueue");
            foreach (var filePath in Directory.GetFiles(folderPath, "*.dat"))
            {
                var lines = File.ReadAllLines(filePath).ToList();
                if (lines.Any())
                {
                    var record = System.Text.Json.JsonSerializer.Deserialize<CommandQueueRecord>(lines[0]);
                    lines.RemoveAt(0);
                    File.WriteAllLines(filePath, lines);
                    return record.Command;
                }
            }
            return null;
        }
        public void RemoveCommand(string commandName)
        {
            string folderPath = Path.Combine(basePath, "CommandQueue");
            foreach (var filePath in Directory.GetFiles(folderPath, "*.dat"))
            {
                var lines = File.ReadAllLines(filePath).ToList();
                var updatedLines = new List<string>();
                foreach (var line in lines)
                {
                    var record = System.Text.Json.JsonSerializer.Deserialize<dynamic>(line);
                    if (record != null && record.CommandName != commandName)
                    {
                        updatedLines.Add(line);
                    }
                }
                File.WriteAllLines(filePath, updatedLines);
            }
        }
        public void RemoveMonitor(string monitorName)
        {
            string folderPath = Path.Combine(basePath, "MonitorQueue");
            foreach (var filePath in Directory.GetFiles(folderPath, "*.dat"))
            {
                var lines = File.ReadAllLines(filePath).ToList();
                var updatedLines = new List<string>();
                foreach (var line in lines)
                {
                    var record = System.Text.Json.JsonSerializer.Deserialize<dynamic>(line);
                    if (record != null && record.MonitorName != monitorName)
                    {
                        updatedLines.Add(line);
                    }
                }
                File.WriteAllLines(filePath, updatedLines);
            }
        }
        public void FlushMonitors()
        {
            string folderPath = Path.Combine(basePath, "MonitorQueue");
            foreach (var filePath in Directory.GetFiles(folderPath, "*.dat"))
            {
                File.Delete(filePath);
            }
        }
        public void FlushCommands()
        {
            string folderPath = Path.Combine(basePath, "CommandQueue");
            foreach (var filePath in Directory.GetFiles(folderPath, "*.dat"))
            {
                File.Delete(filePath);
            }
        }
        public List<CommandQueueRecord> GetCommandQueue()
        {
            string folderPath = Path.Combine(basePath, "CommandQueue");
            var records = new List<CommandQueueRecord>();

            foreach (var filePath in Directory.GetFiles(folderPath, "*.dat"))
            {
                var lines = File.ReadAllLines(filePath);
                foreach (var line in lines)
                {
                    var record = System.Text.Json.JsonSerializer.Deserialize<CommandQueueRecord>(line);
                    if (record != null)
                    {
                        records.Add(record);
                    }
                }
            }

            return records;
        }
        public void EnqueueMonitor(string monitorName, string monitorType, string monitorData1, string monitorData2, string command, string user)
        {
            string filePath = EnsureTodaysFileExists("MonitorQueue");
            var record = new
            {
                MonitorName = monitorName,
                MonitorType = monitorType,
                MonitorData1 = monitorData1,
                MonitorData2 = monitorData2,
                Command = command,
                User = user,
                Activated = false,
                Timestamp = DateTime.Now
            };
            InsertData("MonitorQueue", record);
        }

        public List<dynamic> GetMonitorQueue()
    {
        string folderPath = Path.Combine(basePath, "MonitorQueue");
        var records = new List<dynamic>();

        foreach (var filePath in Directory.GetFiles(folderPath, "*.dat"))
        {
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                var record = JsonSerializer.Deserialize<JsonElement>(line);
                if (record.ValueKind != JsonValueKind.Undefined && record.ValueKind != JsonValueKind.Null)
                {
                    records.Add(record);
                }
            }
        }
        return records;
    }

        public void UpdateMonitorActivationStatus(string monitorName, bool activated)
        {
            string folderPath = Path.Combine(basePath, "MonitorQueue");
            foreach (var filePath in Directory.GetFiles(folderPath, "*.dat"))
            {
                var lines = File.ReadAllLines(filePath);
                var updatedLines = new List<string>();
                foreach (var line in lines)
                {
                    var record = System.Text.Json.JsonSerializer.Deserialize<dynamic>(line);
                    if (record != null && record.MonitorName == monitorName)
                    {
                        record.Activated = activated;
                    }
                    updatedLines.Add(System.Text.Json.JsonSerializer.Serialize(record));
                }
                File.WriteAllLines(filePath, updatedLines);
            }
        }
    }

    // Define data classes for each table
    public class FF_CaseCommands
    {
        public string Command { get; set; }
        public string CodeToExecute { get; set; }
        public string Keywords { get; set; }
        public string Software { get; set; }
        public DateTime LastUsed { get; set; }
        public bool Dynamic { get; set; }
    }

    public class FF_CaseInitializationLog
    {
        public string LLM { get; set; }
        public DateTime DT { get; set; }
        public bool Loading { get; set; }
        public bool Loaded { get; set; }
        public string TimeToLoad { get; set; }
    }

    public class FF_CaseInteractions
    {
        public string Username { get; set; }
        public string Message { get; set; }
        public string Screen { get; set; }
        public DateTime DT { get; set; }
        public string Receiver { get; set; }
        public bool Submitted { get; set; }
        public bool WaitingOnResponse { get; set; }
        public bool Answered { get; set; }
        public bool ActionPerformed { get; set; }
        public string TimeToProcess { get; set; }
        public string CommandsActivated { get; set; }
        public Guid Id { get; set; }
    }

    public class FF_CaseLogs
    {
        public Guid SessionId { get; set; }
        public string LogMessage { get; set; }
    }

    public class FF_CaseSessions
    {
        public Guid Session { get; set; }
        public DateTime StartDT { get; set; }
        public DateTime EndDT { get; set; }
        public string Username { get; set; }
    }
    public class CommandQueueRecord
    {
        public string CommandName { get; set; }
        public string Command { get; set; }
        public string User { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
