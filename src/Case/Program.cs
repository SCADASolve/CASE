//----------------------------------------------------------------------------
//  Copyright (C) 2024 by SCADA Solve LLC. All rights reserved.              
//----------------------------------------------------------------------------
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Threading;
using System.Management;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Emgu.CV;
using Emgu.CV.OCR;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace Case
{
    class Program
    {
        public static GlobalParameters gp = new GlobalParameters();
        public static DistributionManager dm = new DistributionManager();
        public static CaseFileManager caseFileManager = new CaseFileManager(gp, dm);
        public static RPA rPA = new RPA();
        public static Guid currentSession;
        public static bool executingSim = false;
        public static bool settingsUnlocked = false;

        [STAThread]
        public static void Main(string[] args)
        {
            Console.Title = "CASE";
            bool debug = false; // Set this to true for debug mode
            if (debug && !executingSim)
            {
                ExampleCommands e = new ExampleCommands();
                e.exampleCommands();
                return;
            }

            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "Settings.config"))
            {
                gp.loadSettingsFile();
                string res = InitialSetup(); 
                if (res.Contains("Failed"))
                {
                    Console.WriteLine(res);
                    gp.updateSettingsFile("CaseStable", "False");
                    Console.ReadLine();
                    return;
                }
                else
                {
                    gp.updateSettingsFile("CaseStable", "True");
                }
            }
            else if (gp.getSetting("CaseStable") == "False")
            {
                Console.WriteLine("Settings file found to be unstable, going through setup.");
                string res = InitialSetup(); 
                if (res.Contains("Failed"))
                {
                    Console.WriteLine(res);
                    gp.updateSettingsFile("CaseStable", "False");
                    Console.ReadLine();
                    return;
                }
                else
                {
                    gp.updateSettingsFile("CaseStable", "True");
                }
            }

            gp.loadSettingsFile();
            currentSession = Guid.NewGuid();
            dm.distributeCaseSessions(currentSession, DateTime.Now, DateTime.Now, Environment.UserName);

            if (args.Length == 0)
            {
                string val = gp.getSetting("DisplayAscii");
                if (val == "True")
                    displayCaseAscii();
                while (true)
                {
                    Console.Write("> ");
                    string input = Console.ReadLine();
                    if (input.ToLower() == "exit")
                    {
                        Console.WriteLine("Exiting the application.");
                        break;
                    }
                    string[] commandArgs = input.Split(' ');
                    ProcessCommands(commandArgs);
                }
            }
            else
            {
                ProcessCommands(args);
            }
        }

        /// <summary>
        /// Processes the given commands.
        /// </summary>
        /// <param name="args">Command arguments</param>
        static void ProcessCommands(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-command":
                        if (args.Length >= 2)
                        {
                            string subCommand = args[1].ToLower();
                            if (subCommand == "-create")
                            {
                                dm.distributeInteractiveCreateCaseCommands();
                                dm.distributeCaseLogs(currentSession, "Went through command creation procedure");
                                dm.updateCaseSessions(currentSession);
                                return;
                            }
                            else if (subCommand == "-update")
                            {
                                dm.distributeInteractiveUpdateCaseCommands();
                                dm.distributeCaseLogs(currentSession, "Went through command update procedure");
                                dm.updateCaseSessions(currentSession);
                                return;
                            }
                            else if (subCommand == "-view")
                            {
                                var availableCommands = dm.GetAvailableCommands();
                                
                                if (args.Length == 3)
                                { 
                                    string software = args[2];
                                    Console.WriteLine("Available commands filtered by software ["+software+"]:");
                                    for (int j = 0; j < availableCommands.Count; j++)
                                    {
                                        if (availableCommands[j].Software.ToLower() == software.ToLower())
                                            Console.WriteLine($"{j + 1}. Command Name: {availableCommands[j].Command}, Keywords: {availableCommands[j].Keywords}, RPA: {availableCommands[j].CodeToExecute}, Last Used: {availableCommands[j].LastUsed.ToString()}");
                                    }
                                }
                                else
                                { 
                                    if (availableCommands.Count == 0)
                                    {
                                        Console.WriteLine("No Commands Detected. ");
                                        Console.WriteLine("Use -command -create to create a new command.");
                                        Console.WriteLine("Use -command -import to import a series of commands.");
                                    }
                                    else
                                    {
                                        Console.WriteLine("Available commands:");
                                        for (int j = 0; j < availableCommands.Count; j++)
                                        {
                                            Console.WriteLine($"{j + 1}. Command Name: {availableCommands[j].Command}, Keywords: {availableCommands[j].Keywords}, RPA: {availableCommands[j].CodeToExecute}, Last Used: {availableCommands[j].LastUsed.ToString()}");
                                        }
                                    }
                                }
                                return;
                            }
                            else if (subCommand == "-help")
                            {
                                RPA.ShowCommands();
                                return;
                            }
                            else if (subCommand == "-import")
                            {
                                if (args.Length < 3)
                                {
                                    Console.WriteLine("Please provide the path to the .case file.");
                                    return;
                                }
                                else
                                {
                                    string combined = "";
                                    if (args.Length > 2)
                                        combined = JoinSeparatedQuotedString(args, ref i);
                                        //combined = JoinQuotedString(args, 2);
                                    else
                                        combined = args[2];
                                    if (!File.Exists(combined) || !File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\" + combined))
                                    { 
                                        Console.WriteLine("Failed to load file: " + combined);
                                    }
                                    else
                                    { 
                                        caseFileManager.LoadCaseFile(args[2]);
                                        dm.distributeCaseLogs(currentSession, "Imported commands from file: " + args[2]);
                                        dm.updateCaseSessions(currentSession);
                                    }
                                    return;
                                }
                            }
                            else if (subCommand == "-export")
                            {
                                caseFileManager.CreateCaseFile();
                                return;
                            }
                            else
                            {
                                string inputKeywords = args[1];
                                var result = dm.distributeDetectKeywordsInString(inputKeywords);
                                if (result.Count == 0)
                                {
                                    string code = dm.collectRPACommands(inputKeywords, true);
                                    if (code.Length > 0)
                                    {
                                        Console.WriteLine("Direct command found, running.");
                                        rPA.runRPA(inputKeywords);
                                        dm.distributeCaseLogs(currentSession, "Ran command: " + inputKeywords);
                                        dm.updateCaseSessions(currentSession);
                                    }
                                    else
                                    {
                                        Console.WriteLine("No matching commands found.");
                                    }
                                    return;
                                }

                                Console.WriteLine("Detected Multiple Commands:");
                                int index = 1;
                                foreach (var kv in result)
                                {
                                    Console.WriteLine($"{index}. {kv.Key}: {kv.Value}% match");
                                    index++;
                                }
                                bool pickFirst = args.Contains("-p");

                                string input = "";
                                if (!pickFirst)
                                {
                                    Console.WriteLine("Enter the number of the command you want to execute or type 'cancel' to exit:");
                                    input = Console.ReadLine();
                                }
                                else
                                {
                                    input = "1";
                                }
                                if (input.ToLower() == "cancel")
                                {
                                    Console.WriteLine("Operation canceled.");
                                    return;
                                }

                                if (int.TryParse(input, out int commandNumber) && commandNumber > 0 && commandNumber <= result.Count)
                                {
                                    var selectedCommand = result.ElementAt(commandNumber - 1).Key;
                                    bool bypassValidation = args.Contains("-v");
                                    bool bypassExplanation = args.Contains("-e");

                                    if (bypassValidation && bypassExplanation)
                                    {
                                        Console.WriteLine($"Executing command: {selectedCommand}");
                                        rPA.runRPA(selectedCommand);
                                        dm.distributeCaseLogs(currentSession, "Executed: " + selectedCommand);
                                        dm.updateCaseSessions(currentSession);
                                        Console.WriteLine("Command executed successfully.");
                                        return;
                                    }
                                    else if (bypassValidation)
                                    {
                                        string readableCommand = rPA.TranslateRPAtoReadable(selectedCommand);
                                        Console.WriteLine($"This command will: {readableCommand}");
                                        Console.WriteLine("Do you want to continue? (y/n)");

                                        string userResponse = Console.ReadLine();
                                        if (userResponse.ToLower() == "y")
                                        {
                                            rPA.runRPA(selectedCommand);
                                            dm.distributeCaseLogs(currentSession, "Executed: " + selectedCommand);
                                            dm.updateCaseSessions(currentSession);
                                            Console.WriteLine("Command executed successfully.");
                                            return;
                                        }
                                        else
                                        {
                                            Console.WriteLine("Command execution canceled.");
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        string execCommand = dm.collectRPACommands(selectedCommand, true);
                                        Console.WriteLine($"Verifying code stability and executing command: {selectedCommand}");
                                        string stabilityCheck = rPA.ValidateCommands(execCommand);

                                        if (stabilityCheck == "Valid")
                                        {
                                            if (!bypassExplanation)
                                            {
                                                execCommand = dm.collectRPACommands(selectedCommand);
                                                string readableCommand = rPA.TranslateRPAtoReadable(execCommand);
                                                Console.WriteLine($"This command will: {readableCommand}");
                                                Console.WriteLine("Do you want to continue? (y/n)");

                                                string userResponse = Console.ReadLine();
                                                if (userResponse.ToLower() == "y")
                                                {
                                                    rPA.autoRun(execCommand);
                                                    dm.distributeCaseLogs(currentSession, "Executed: " + selectedCommand);
                                                    dm.updateCaseSessions(currentSession);
                                                    Console.WriteLine("Command executed successfully.");
                                                    return;
                                                }
                                                else
                                                {
                                                    Console.WriteLine("Command execution canceled.");
                                                    return;
                                                }
                                            }
                                            else
                                            {
                                                execCommand = dm.collectRPACommands(selectedCommand);
                                                rPA.autoRun(execCommand);
                                                dm.distributeCaseLogs(currentSession, "Executed: " + selectedCommand);
                                                dm.updateCaseSessions(currentSession);
                                                Console.WriteLine("Command executed successfully.");
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine(stabilityCheck);
                                            Console.WriteLine("Please correct your command and try again");
                                        }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Invalid selection. Operation canceled.");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid number of arguments for -command.");
                        }
                        break;

                    case "-execute":
                        string subComm = args[1].ToLower();
                        if (subComm == "-verbose")
                        {
                            i++;
                            string command = JoinQuotedString(args, ref i);
                            command = RPA.TranslateCommand(command, false);
                            string stabilityCheck = rPA.ValidateCommands(command);
                            if (stabilityCheck == "Valid")
                            {
                                rPA.autoRun(command);
                                dm.distributeCaseLogs(currentSession, "Executed: " + command);
                                dm.updateCaseSessions(currentSession);
                                return;
                            }
                            else
                            {
                                Console.WriteLine(stabilityCheck);
                                Console.WriteLine("Please correct your command and try again");
                            }
                        }
                        else if (i + 1 < args.Length)
                        {
                            string command = JoinQuotedString(args, ref i);
                            string stabilityCheck = rPA.ValidateCommands(command);
                            if (stabilityCheck == "Valid")
                            {
                                rPA.autoRun(command);
                                dm.distributeCaseLogs(currentSession, "Executed: " + command);
                                dm.updateCaseSessions(currentSession);
                                return;
                            }
                            else
                            {
                                Console.WriteLine(stabilityCheck);
                                Console.WriteLine("Please correct your command and try again");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Error: No command provided for -execute option.");
                        }
                        break;

                    case "-talk":
                        Console.WriteLine("Starting Conversational Agent");
                        runLLM CaseInit = new runLLM();
                        dm.distributeCaseLogs(currentSession, Environment.UserName + " activated the chat agent");
                        CaseInit.initCase("Conversational");
                        dm.updateCaseSessions(currentSession);
                        break;

                    case "-monitor":
                        if (i + 1 < args.Length)
                        {
                            string monitorType = args[++i];
                            string data1, data2, command;

                            switch (monitorType)
                            {
                                case "-image":
                                    if (i + 2 < args.Length)
                                    {
                                        data1 = args[++i];
                                        command = args[++i];
                                        dm.distributeCaseLogs(currentSession, "Started Image Monitoring: " + data1);
                                        rPA.autoRun("h" + data1);
                                        dm.distributeCaseLogs(currentSession, "Executing Command: " + command);
                                        rPA.runRPA(command);
                                        dm.updateCaseSessions(currentSession);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error: Insufficient arguments for image detection.");
                                    }
                                    break;

                                case "-file":
                                    if (i + 3 < args.Length)
                                    {
                                        data1 = args[++i];
                                        data2 = args[++i];
                                        command = args[++i];
                                        dm.distributeCaseLogs(currentSession, "Started File Monitoring: " + data1 + " in " + data2);
                                        MonitorFile(data1, data2);
                                        dm.distributeCaseLogs(currentSession, "Executing Command: " + command);
                                        rPA.runRPA(command);
                                        dm.updateCaseSessions(currentSession);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error: Insufficient arguments for file detection.");
                                    }
                                    break;

                                case "-database":
                                    if (i + 3 < args.Length)
                                    {
                                        string connString = gp.getSetting("SQLConnectionString");
                                        if (string.IsNullOrEmpty(connString))
                                        {
                                            Console.WriteLine("Error: No SQL connection string configured.");
                                            return;
                                        }
                                        command = args[++i];
                                        data1 = args[++i];
                                        data2 = JoinQuotedString(args, ref i);
                                        Console.WriteLine("SQL detection: Table " + data1 + " with condition " + data2);
                                        dm.distributeCaseLogs(currentSession, "Started SQL Monitoring: Table " + data1 + " with condition " + data2);
                                        SQLManager sQL = new SQLManager();
                                        if (sQL.MonitorSQL(connString, data1, data2))
                                        {
                                            dm.distributeCaseLogs(currentSession, "Executing Command: " + command);
                                            rPA.runRPA(command);
                                            dm.updateCaseSessions(currentSession);
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error: Insufficient arguments for SQL detection.");
                                    }
                                    break;

                                case "-c":
                                    rPA.initClipboardMonitor(currentSession, 1000);
                                    dm.updateCaseSessions(currentSession);
                                    break;

                                default:
                                    Console.WriteLine("Error: Unknown monitoring type " + monitorType);
                                    break;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Error: No monitor type provided.");
                        }
                        break;
                    /*
                    case "-queue":
                        if (i + 1 < args.Length)
                        {
                            string queueType = args[++i];
                            if (queueType == "-command")
                            {
                                if (i + 1 < args.Length)
                                {
                                    string subCommand = args[++i];
                                    if (subCommand == "-remove" && i + 1 < args.Length)
                                    {
                                        string commandName = args[++i];
                                        dm.RemoveCommand(commandName);
                                        Console.WriteLine($"Removed command: {commandName}");
                                    }
                                    else if (subCommand == "-add" && i + 1 < args.Length)
                                    {
                                        string queueCommand = args[++i];
                                        string queueCode = dm.collectRPACommands(queueCommand);
                                        dm.EnqueueCommand(queueCommand, queueCode, Environment.UserName);
                                        Console.WriteLine($"Enqueued command: {queueCommand}");
                                    }
                                    else if (subCommand == "-flush")
                                    {
                                        dm.FlushCommandQueue();
                                        Console.WriteLine("Flushed command queue.");
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error: Invalid or insufficient arguments for command queue.");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Error: No command provided for command queue.");
                                }
                            }
                            else if (queueType == "-monitor")
                            {
                                if (i + 1 < args.Length)
                                {
                                    string subCommand  = args[++i];
                                    if (subCommand == "-remove" && i + 1 < args.Length)
                                    {
                                        string monitorName = args[++i];
                                        dm.RemoveMonitor(monitorName);
                                        Console.WriteLine($"Removed monitor: {monitorName}");
                                    }
                                    else if (subCommand != "-remove" && subCommand != "-flush" && i + 4 < args.Length)
                                    {
                                        string queueMonitor = subCommand;
                                        string queueMonitorName = args[++i];
                                        string queueMonitorData1 = args[++i];
                                        string queueMonitorData2 = args[++i];
                                        string queueMonitorCommand = args[++i];
                                        dm.EnqueueMonitor(queueMonitor, queueMonitorName, queueMonitorData1, queueMonitorData2, queueMonitorCommand, Environment.UserName);
                                        Console.WriteLine($"Enqueued monitor: {queueMonitor}");
                                    }
                                    else if (subCommand == "-flush")
                                    {
                                        dm.FlushMonitorQueue();
                                        Console.WriteLine("Flushed monitor queue.");
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error: Invalid or insufficient arguments for monitor queue.");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Error: No monitor provided for monitor queue.");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Error: Unknown queue type.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Error: No queue type provided.");
                        }
                        break;

                    case "-execQueue":
                        if (i + 1 < args.Length)
                        {
                            string queueType = args[++i];
                            if (queueType == "-monitor")
                            {
                                DistributionManager dm = new DistributionManager();
                                Console.WriteLine("Processing Monitor queue...");
                                foreach (var monitor in dm.GetMonitorQueue())
                                {
                                    if (!monitor.Activated)
                                    {
                                        switch (monitor.MonitorType)
                                        {
                                            case "image":
                                                Console.WriteLine("Image detection: " + monitor.MonitorData1);
                                                dm.distributeCaseLogs(currentSession, "Started Image Monitoring: " + monitor.MonitorData1);
                                                rPA.autoRun("h" + monitor.MonitorData1, Convert.ToInt32(monitor.MonitorData2));
                                                dm.distributeCaseLogs(currentSession, "Queueing Command: " + monitor.Command);
                                                rPA.runRPA(monitor.Command);
                                                break;

                                            case "file":
                                                Console.WriteLine("File detection: " + monitor.MonitorData1 + " in " + monitor.MonitorData2);
                                                dm.distributeCaseLogs(currentSession, "Started File Monitoring: " + monitor.MonitorData1 + " in " + monitor.MonitorData2);
                                                MonitorFile(monitor.MonitorData1, monitor.MonitorData2);
                                                dm.distributeCaseLogs(currentSession, "Executing Command: " + monitor.Command);
                                                rPA.runRPA(monitor.Command);
                                                break;

                                            case "sql":
                                                string connString = gp.getSetting("SQLConnectionString");
                                                if (string.IsNullOrEmpty(connString))
                                                {
                                                    Console.WriteLine("Error: No SQL connection string configured.");
                                                    continue;
                                                }
                                                Console.WriteLine("SQL detection: Table " + monitor.MonitorData1 + " with condition " + monitor.MonitorData2);
                                                dm.distributeCaseLogs(currentSession, "Started SQL Monitoring: Table " + monitor.MonitorData1 + " with condition " + monitor.MonitorData2);
                                                SQLManager sQL = new SQLManager();
                                                if (sQL.MonitorSQL(connString, monitor.MonitorData1, monitor.MonitorData2))
                                                {
                                                    dm.distributeCaseLogs(currentSession, "Executing Command: " + monitor.Command);
                                                    rPA.runRPA(monitor.Command);
                                                }
                                                break;

                                            case "clipboard":
                                                rPA.initClipboardMonitor(currentSession, 1000);
                                                break;

                                            default:
                                                Console.WriteLine("Error: Unknown monitoring type " + monitor.MonitorType);
                                                break;
                                            }

                                            dm.UpdateMonitorActivationStatus(monitor.MonitorName, true);
                                        }
                                    }
                                }
                                else if (queueType == "-command")
                                { 
                                    Console.WriteLine("Processing command queue...");
                                    DistributionManager dm = new DistributionManager();
                                    RPA rPA = new RPA();
                                    dm.ProcessCommandQueue(rPA, currentSession);
                                }
                            }
                        break;
                        case "-viewQueue":
                        if (i + 1 < args.Length)
                        {
                            string queueType = args[++i];
                            if (queueType == "-monitor")
                            {
                                DistributionManager dm = new DistributionManager();
                                Console.WriteLine("Showing Monitor queue...");
                                foreach (var monitor in dm.GetMonitorQueue())
                                {
                                    JsonElement monitorTypeElement;
                                    if (monitor.TryGetProperty("MonitorName", out monitorTypeElement))
                                    {
                                        string monitorType = monitorTypeElement.GetString();
                                        monitor.TryGetProperty("MonitorData1", out monitorTypeElement);
                                        string md1 = monitorTypeElement.GetString();
                                        monitor.TryGetProperty("MonitorData2", out monitorTypeElement);
                                        string md2 = monitorTypeElement.GetString();
                                        monitor.TryGetProperty("Command", out monitorTypeElement);
                                        string mc = monitorTypeElement.GetString();
                                        switch (monitorType)
                                        {
                                            case "image":
                                                Console.WriteLine("Monitor named '" + monitorType + "' for this image on screen: " + md1 + " with a delay timeout of: " + md2);
                                                Console.WriteLine(" which, when found, will queue this command: " + mc);
                                                Console.WriteLine("============================================================================================");
                                                break;

                                            case "file":
                                                Console.WriteLine("Monitor named '" + monitorType+ "' monitoring for this file: " + md1 + " in folder: " + md2);
                                                Console.WriteLine(" which, when found, will queue this command: " + mc);
                                                Console.WriteLine("============================================================================================");
                                                break;

                                            case "sql":
                                                Console.WriteLine("Monitor named '" + monitorType + "' monitoring this table: " + md1 + " with condition " + md2);
                                                Console.WriteLine(" which, when found, will queue this command: " + mc);
                                                Console.WriteLine("============================================================================================");
                                                break;
                                        }
                                    }
                                }
                            }
                            else if (queueType == "-command")
                            {
                                Console.WriteLine("Showing command queue...");
                                List<CommandQueueRecord> commands = dm.GetCommandQueue();
                                foreach (CommandQueueRecord cmd in commands)
                                {
                                    Console.WriteLine("Command: " + cmd.CommandName + " - Code: " + cmd.Command);
                                    Console.WriteLine("Last Executed: " + cmd.Timestamp+ " - User: " + cmd.User);
                                    Console.WriteLine("============================================================================================");
                                }
                                Console.ReadLine();
                            }
                        }
                        break;
                    */
                    case "-analyze":
                        if (i + 1 < args.Length)
                        {
                            string analyzeType = args[++i];
                            switch (analyzeType)
                            {
                                case "-file":
                                    if (i + 2 < args.Length)
                                    {
                                        i++;
                                        string filePath = JoinSeparatedQuotedString(args, ref i);
                                        string prompt = JoinSeparatedQuotedString(args, ref i);
                                        runLLM rL = new runLLM();
                                        rL.initCase("Analysis", "File", filePath, prompt, currentSession);
                                        dm.distributeCaseLogs(currentSession, $"Began Analyzing file: {filePath} with prompt: {prompt}");
                                        dm.updateCaseSessions(currentSession);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error: Insufficient arguments for file analysis.");
                                    }
                                    break;

                                case "-database":
                                    if (i + 2 < args.Length)
                                    {
                                        i++;
                                        string sqlQuery = JoinSeparatedQuotedString(args, ref i);
                                        string prompt = JoinSeparatedQuotedString(args, ref i);
                                        runLLM rL = new runLLM();
                                        rL.initCase("Analysis", "SQL", sqlQuery, prompt, currentSession);
                                        dm.distributeCaseLogs(currentSession, $"Began Analyzing SQL query: {sqlQuery} with prompt: {prompt}");
                                        dm.updateCaseSessions(currentSession);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error: Insufficient arguments for SQL analysis.");
                                    }
                                    break;

                                default:
                                    Console.WriteLine("Error: Unknown analyze type " + analyzeType);
                                    break;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Error: No analyze type provided.");
                        }
                        break;

                    case "-settings":
                        if (i + 1 < args.Length)
                        {
                            string subCommand = args[++i];
                            switch (subCommand)
                            {
                                case "-unlock":
                                    if (SecurityManager.IsPasswordConfigured())
                                    {
                                        settingsUnlocked = false;
                                        if (SecurityManager.CheckPassword())
                                        {
                                            settingsUnlocked = true;
                                            Console.WriteLine("Settings unlocked.");
                                        }
                                        else
                                        {
                                            Console.WriteLine("Incorrect password. Settings remain locked.");
                                        }
                                    }
                                    else
                                    {
                                        settingsUnlocked = true;
                                        Console.WriteLine("Password is not configured. Configure a password first.");
                                    }
                                    break;

                                case "-password":
                                    if (!SecurityManager.IsPasswordConfigured())
                                    {
                                        settingsUnlocked = true;
                                    }
                                    if (!settingsUnlocked)
                                    {
                                        Console.WriteLine("Settings are locked. Use '-settings -unlock' to unlock.");
                                        break;
                                    }
                                    if (i + 1 < args.Length)
                                    {
                                        string passwordSubCommand = args[++i];
                                        switch (passwordSubCommand)
                                        {
                                            case "-c":
                                                SecurityManager.CreatePasswordFile();
                                                dm.distributeCaseLogs(currentSession, Environment.UserName + " went through the password creation procedure.");
                                                dm.updateCaseSessions(currentSession);
                                                break;
                                            case "-k":
                                                SecurityManager.CheckPassword();
                                                dm.distributeCaseLogs(currentSession, Environment.UserName + " checked their password.");
                                                dm.updateCaseSessions(currentSession);
                                                break;
                                            case "-h":
                                                SecurityManager.ChangePassword();
                                                dm.distributeCaseLogs(currentSession, Environment.UserName + " went through the change password procedure.");
                                                dm.updateCaseSessions(currentSession);
                                                break;
                                            default:
                                                Console.WriteLine($"Unknown password sub-command: {passwordSubCommand}");
                                                DisplayHelp();
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error: No sub-command provided for password option.");
                                        DisplayHelp();
                                    }
                                    break;

                                case "-update":
                                    if (!settingsUnlocked && SecurityManager.IsPasswordConfigured())
                                    {
                                        Console.WriteLine("Settings are locked. Use '-settings -unlock' to unlock.");
                                        break;
                                    }
                                    else
                                    {
                                        if (i + 1 < args.Length)
                                        {
                                            string[] setting = args[++i].Split(':');
                                            if (gp.updateSettingsFile(setting[0], setting[1]))
                                            {
                                                dm.distributeCaseLogs(currentSession, Environment.UserName + $" updated {setting[0]} to {setting[1]}");
                                                dm.updateCaseSessions(currentSession);
                                            }
                                            else
                                            {
                                                Console.WriteLine("Error: Invalid format for update option.");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Error: No setting provided for update option.");
                                        }
                                    }
                                    break;
                                case "-show":
                                    if (!settingsUnlocked && SecurityManager.IsPasswordConfigured())
                                    {
                                        Console.WriteLine("Settings are locked. Use '-settings -unlock' to unlock.");
                                        break;
                                    }
                                    else
                                    {
                                        displaySettings();
                                        dm.updateCaseSessions(currentSession);
                                    }
                                    break;

                                case "-gpu":
                                    if (!settingsUnlocked && SecurityManager.IsPasswordConfigured())
                                    {
                                        Console.WriteLine("Settings are locked. Use '-settings -unlock' to unlock.");
                                        break;
                                    }
                                    else
                                    { 
                                        getGPUs();
                                        dm.distributeCaseLogs(currentSession, Environment.UserName + " went through the GPU designation procedure.");
                                        dm.updateCaseSessions(currentSession);
                                    }
                                    break;

                                case "-ai":
                                    if (!settingsUnlocked && SecurityManager.IsPasswordConfigured())
                                    {
                                        Console.WriteLine("Settings are locked. Use '-settings -unlock' to unlock.");
                                        break;
                                    }
                                    else
                                    {
                                        runLLM rl = new runLLM();
                                        rl.AI_UnderTheHood();
                                        dm.distributeCaseLogs(currentSession, Environment.UserName + " went through the AI reconfiguration procedure.");
                                        dm.updateCaseSessions(currentSession);
                                    }
                                    break;

                                default:
                                    Console.WriteLine($"Unknown settings sub-command: {subCommand}");
                                    DisplayHelp();
                                    break;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Error: No sub-command provided for settings option.");
                            DisplayHelp();
                        }
                        break;

                    case "-resetSettings":
                        File.Delete(AppDomain.CurrentDomain.BaseDirectory + "Settings.config");
                        Console.WriteLine("Settings deleted, re-open Case.");
                        Console.ReadLine();
                        return;

                    case "-help":
                        DisplayHelp();
                        break;

                    default:
                        Console.WriteLine($"Unknown argument: {args[i]}");
                        DisplayHelp();
                        break;
                }
                dm.updateCaseSessions(currentSession);
            }
        }

        /// <summary>
        /// Displays the help information.
        /// </summary>
        public static void DisplayHelp()
        {
            Console.WriteLine("Help: List of Available Commands and Their Functions");
            Console.WriteLine("---------------------------------------------------");

            Console.WriteLine("-command <sub-command> [options]");
            Console.WriteLine("    Sub-commands:");
            Console.WriteLine("        -create");
            Console.WriteLine("            Starts the interactive command which steps you through the creation process.");
            Console.WriteLine("        -update");
            Console.WriteLine("            Starts the interactive command which explains the update process for each command.");
            Console.WriteLine("        -view");
            Console.WriteLine("            Views available commands.");
            Console.WriteLine("        -view <keywords> (optional)");
            Console.WriteLine("            Views available commands while filtering out the commands by software.");
            Console.WriteLine("        -help");
            Console.WriteLine("            Displays a basic overview of the RPA commands and options.");
            Console.WriteLine("        -import <filePath>");
            Console.WriteLine("            Imports commands from a specified .case file.");
            Console.WriteLine("        -export");
            Console.WriteLine("            Exports the selected commands to a .case file.");
            Console.WriteLine("        <keywords>");
            Console.WriteLine("            Detects and executes commands based on keywords.");
            Console.WriteLine("            Options:");
            Console.WriteLine("                -p  Automatically picks the first detected command.");
            Console.WriteLine("                -v  Bypasses command validation.");
            Console.WriteLine("                -e  Bypasses command explanation.");
            Console.WriteLine();

            Console.WriteLine("-execute \"<command>\"");
            Console.WriteLine("    Executes the specified RPA directly.");
            Console.WriteLine("    Example: -execute e;sExplorer.exe{ENTER}");
            Console.WriteLine();

            Console.WriteLine("-monitor <type> <data1> [data2] <command>");
            Console.WriteLine("    Begins monitoring with the provided type and data.");
            Console.WriteLine("    Types:");
            Console.WriteLine("        -image");
            Console.WriteLine("            Monitors the screen for an image, once found, activates a defined command stored in the commands.");
            Console.WriteLine("            Example: -monitor -image \"path/to/image.jpg\" \"some command\"");
            Console.WriteLine("        -file");
            Console.WriteLine("            Monitors the filesystem for a file, once found, activates a defined command stored in the commands.");
            Console.WriteLine("            Example: -monitor -file \"file.txt\" \"directory\" \"some command\"");
            Console.WriteLine("        -database");
            Console.WriteLine("            Monitors a SQL Table for a Condition, once found, activates a defined command stored in the commands.");
            Console.WriteLine("            Example: -monitor -database \"table\" \"condition\" \"some command\"");
            Console.WriteLine("        -c");
            Console.WriteLine("            Monitors the clipboard for a defined command, once found, runs the command.");
            Console.WriteLine("            Example: -monitor -c");
            Console.WriteLine();
            /*
            Console.WriteLine("-queue <type> <name> <Parameter1> [Parameter2] <command>");
            Console.WriteLine("    Enqueues a command or monitor for later execution.");
            Console.WriteLine("    Types:");
            Console.WriteLine("        -command");
            Console.WriteLine("            Adds a command into the queue for sequential execution of commands following the -command usage for execution.");
            Console.WriteLine("            Example: -queue -command \"commandName\" \"command\"");
            Console.WriteLine("        -monitor");
            Console.WriteLine("            Monitors images, files or database tables following the -monitor parameters and adds them to a queue for detection.");
            Console.WriteLine("            Example: -queue -monitor \"monitorName\" \"monitorType\" \"data1\" \"data2\" \"command\"");
            Console.WriteLine();

            Console.WriteLine("-execQueue <type>");
            Console.WriteLine("    Processes the queued commands and monitors.");
            Console.WriteLine("    Types:");
            Console.WriteLine("        -monitor");
            Console.WriteLine("            Continually the monitor queue for monitored items, and sends commands to the command queue when detected.");
            Console.WriteLine("            Example: -execQueue -monitor");
            Console.WriteLine("        -command");
            Console.WriteLine("            Scans the command queue and executes commands sequentially to avoid overlapped commands.");
            Console.WriteLine("            Example: -execQueue -command");
            Console.WriteLine();

            Console.WriteLine("-viewQueue <type>");
            Console.WriteLine("    Views the queued commands and monitors.");
            Console.WriteLine("    Types:");
            Console.WriteLine("        -monitor");
            Console.WriteLine("            Shows all active items in the monitor queue that have not activated or been detected.");
            Console.WriteLine("            Example: -viewQueue -monitor");
            Console.WriteLine("        -command");
            Console.WriteLine("            Lists commands awaiting execution in the command queue.");
            Console.WriteLine("            Example: -viewQueue -command");
            Console.WriteLine();
            */
            //LLM Capabilities...
            if (gp.getSetting("BypassLLM") == "False")
            { 
                Console.WriteLine("-talk");
                Console.WriteLine("    Starts the Conversational Generative AI agent.");
                Console.WriteLine("    Example: -talk");
                Console.WriteLine();

                Console.WriteLine("-analyze <type> <data> <prompt>");
                Console.WriteLine("    Starts a Conversational Generative AI agent, populates template files and analyzes the specified data in the request.");
                Console.WriteLine("    Types:");
                Console.WriteLine("        -file");
                Console.WriteLine("            Example: -analyze -file \"path/to/file.txt\" \"analyze this file\"");
                Console.WriteLine("        -database");
                Console.WriteLine("            Example: -analyze -database \"SELECT * FROM table\" \"analyze this query\"");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("Conversational Agent and Analysis of data disabled.");
                Console.WriteLine();
            }    
            Console.WriteLine("-settings <sub-command> [options]");
            Console.WriteLine("    Sub-commands:");
            Console.WriteLine("        -unlock");
            Console.WriteLine("            Unlocks the settings for modification.");
            Console.WriteLine("            Example: -settings -unlock");
            Console.WriteLine("        -password");
            Console.WriteLine("            Manages passwords (Requires unlock).");
            Console.WriteLine("            Sub-commands:");
            Console.WriteLine("                -c  Goes through the password creation routine and creates a new encrypted password file from an entered password.");
            Console.WriteLine("                    Example: -settings -password -c");
            Console.WriteLine("                -k  Checks the entered password against the encrypted stored pasword file.");
            Console.WriteLine("                    Example: -settings -password -k");
            Console.WriteLine("                -h  Goes through the Change Password procedure.");
            Console.WriteLine("                    Example: -settings -password -h");
            Console.WriteLine("        -update <setting:value>");
            Console.WriteLine("            Updates a setting to the given value (Requires unlock).");
            Console.WriteLine("            Example: -settings -update \"SettingName:True\"");
            Console.WriteLine("        -show");
            Console.WriteLine("            Display current settings for Case and it's relative properties (Requires unlock).");
            Console.WriteLine("            Example: -settings -show");
            Console.WriteLine("        -gpu");
            Console.WriteLine("            Lists and allows a user to select a GPU to use in the Generative AI configuration files.");
            Console.WriteLine("            Example: -settings -gpu");
            Console.WriteLine("        -ai");
            Console.WriteLine("            Lists and allows a user to reconfigure multiple properties utilized by the Generative AI configuration files.");
            Console.WriteLine("            Example: -settings -ai");
            Console.WriteLine();
            
            Console.WriteLine("-resetSettings");
            Console.WriteLine("    Deletes settings file to be re-initalized through the 'InitialSetup' process.");
            Console.WriteLine("    Example: -help");
            Console.WriteLine();

            Console.WriteLine("-help");
            Console.WriteLine("    Displays this help message.");
            Console.WriteLine("    Example: -help");
            Console.WriteLine();

            Console.WriteLine("---------------------------------------------------");
            Console.WriteLine("For further details on each command, you can view CASE's source code @ https://github.com/SCADASolve/CASE.");
        }

        /// <summary>
        /// Joins quoted strings into a single string.
        /// </summary>
        /// <param name="args">Command arguments</param>
        /// <param name="index">Current index</param>
        /// <returns>Joined string</returns>
        public static string JoinQuotedString(string[] args, ref int index)
        {
            List<string> parts = new List<string>();
            index++; // Move to the first part of the quoted string
            while (index < args.Length && !args[index].StartsWith("-"))
            {
                parts.Add(args[index]);
                index++;
            }
            index--; // Adjust index because the loop exits after incrementing
            return string.Join(" ", parts);
        }

        /// <summary>
        /// Monitors the specified file in the given folder.
        /// </summary>
        /// <param name="fileName">File name to monitor</param>
        /// <param name="folderPath">Folder path where the file is located</param>
        public static void MonitorFile(string fileName, string folderPath)
        {
            Console.WriteLine($"Monitoring file {fileName} in folder {folderPath}.");
            string filePath = Path.Combine(folderPath, fileName);
            while (!File.Exists(filePath))
            {
                // Busy wait
            }
        }

        /// <summary>
        /// Joins quoted strings into a single string.
        /// </summary>
        /// <param name="args">Command arguments</param>
        /// <param name="index">Current index</param>
        /// <returns>Joined string</returns>
        public static string JoinSeparatedQuotedString(string[] args, ref int index)
        {
            string result = args[index];
            if (result.StartsWith("\""))
            {
                while (!result.EndsWith("\"") && index + 1 < args.Length)
                {
                    index++;
                    result += " " + args[index];
                }
            }
            index++; // Move the index to the next position after processing the quoted string
            return result.Trim('\"');
        }

        /// <summary>
        /// Displays the case ASCII art.
        /// </summary>
        public static void displayCaseAscii()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(@"#############################################");
            Console.WriteLine(@"# ______     ______     ______     ______   #");
            Console.WriteLine(@"#/\  ___\   /\  __ \   /\  ___\   /\  ___\  #");
            Console.WriteLine(@"#\ \ \____  \ \  __ \  \ \___  \  \ \  __\_ #");
            Console.WriteLine(@"# \ \_____\  \ \_\ \_\  \/\_____\  \ \_____\#");
            Console.WriteLine(@"#  \/_____/   \/_/ /_/   \/_____/   \/_____/#");
            Console.WriteLine(@"#############################################");
            Console.WriteLine(@"######## A product of SCADA Solve ###########");
            Console.WriteLine(@"#############################################");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
        }

        /// <summary>
        /// Initial setup process for the application.
        /// </summary>
        static string InitialSetup()
        {
            string result = "";
            Console.WriteLine("Settings file not found. Starting initial setup...");

            // Check for Python install
            if (!IsPythonInstalled())
            {
                result = "Setup Failed: Python is not installed. Please install Python and try again.";
                return result;
            }

            // Ask for GPT4All host file location with default
            Console.WriteLine("Enter the GPT4All host file location, press Enter to use default (C:\\Users\\[Your Username]\\GPT4All\\):");
            Console.WriteLine("Type 'bypass' to skip LLM and Generative AI configuration, or to strictly use RPA.");
            string hostFileLocation = Console.ReadLine().Trim();
            
            string cacheLocation = "";
            string model = "";
            int threadCount = 0;

            if (hostFileLocation.ToLower() != "bypass")
            { 
                if (string.IsNullOrEmpty(hostFileLocation))
                {
                    hostFileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "gpt4all"); // Default location
                }

                // Ask for GPT4All cache location with default
                Console.WriteLine("Enter the GPT4All cache location (press Enter to use default):");
                cacheLocation = Console.ReadLine().Trim();
                if (string.IsNullOrEmpty(cacheLocation))
                {
                    cacheLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData","Local","nomic.ai","GPT4All"); // Default location
                }

                // Check for GPT4All install
                if (!IsGPT4AllInstalled(hostFileLocation))
                {
                    result = "Setup Failed: GPT4All is not installed at the specified location. Please install GPT4All and try again.";
                    return result;
                }

                runLLM rl = new runLLM();
                rl.AI_UnderTheHood();

                // Suggest available models to use
                model = SuggestAvailableModels(cacheLocation);
                if (string.IsNullOrEmpty(model))
                {
                    result = "Setup Failed: No models found in cache. Please place the models in .cache/gpt4all and try again.";
                    return result;
                }
                gp.updateSettingsFile("BypassLLM", "False");
            }
            else
            {
                gp.updateSettingsFile("BypassLLM", "True");
            }

            // Ask for FlatFile or DB
            Console.WriteLine("Enter storage method (default is 'FlatFile', type 'Database' for SQL):");
            string storageMethod = Console.ReadLine().Trim();
            while (storageMethod != "FlatFile" && storageMethod != "Database" && storageMethod != "")
            {
                storageMethod = Console.ReadLine().Trim();
                Console.WriteLine("Try again please.");
            }
            if (string.IsNullOrEmpty(storageMethod) || storageMethod.Equals("FlatFile", StringComparison.OrdinalIgnoreCase))
            {
                // FlatFile selected
                Console.WriteLine("Enter the directory where Case will be stored:");
                Console.WriteLine("Press Enter to default to the application directory (" + AppDomain.CurrentDomain.BaseDirectory + ")");
                string directory = Console.ReadLine().Trim();
                if (directory.Length == 0)
                {
                    directory = AppDomain.CurrentDomain.BaseDirectory;
                }
                SetupFlatFile(directory);
                gp.updateSettingsFile("FlatFilePath", directory); 
            }
            else if (storageMethod.Equals("Database", StringComparison.OrdinalIgnoreCase))
            {
                // Database selected
                Console.WriteLine("Enter the connection string for the SQL database:");
                string connectionString = Console.ReadLine().Trim();
                if (TestDatabaseConnection(connectionString))
                {
                    SetupDatabase(connectionString);
                }
                else
                {
                    result = "Setup Failed: Database connection failed. Please check the connection string and try again.";
                    return result;
                }
            }
            else
            {
                Console.WriteLine("Invalid storage method. Defaulting to FlatFile.");
                Console.WriteLine("Enter the directory where Case will be stored or exit and restart:");
                string directory = Console.ReadLine().Trim();
                SetupFlatFile(directory);
                if(!Directory.Exists(directory))
                {
                    result = "Setup Failed: No valid path provided.";
                }
                else
                {
                    gp.updateSettingsFile("FlatFilePath", directory);
                }    
            }

            // Ask for the findables directory
            Console.WriteLine("Enter the directory where findable images will be stored:");
            Console.WriteLine("These are for image detection used in the RPA functions.");
            Console.WriteLine("Press Enter to default to the application directory (" + AppDomain.CurrentDomain.BaseDirectory + "find\\)");
            string findablesDirectory = Console.ReadLine().Trim();
            if (findablesDirectory.Length == 0)
            {
                findablesDirectory = AppDomain.CurrentDomain.BaseDirectory + "find\\";
                Directory.CreateDirectory(findablesDirectory);
            }
            gp.updateSettingsFile("ImageStorageDirectory", findablesDirectory);    

            if (result.Contains("Failed"))
            {
                dm.distributeCaseLogs(currentSession, Environment.UserName + " failed their setup procedure.");
                dm.updateCaseSessions(currentSession);
                return result;
            }
            else
            {
                SaveSettings(hostFileLocation, cacheLocation.Replace("\\", "\\\\"), model, storageMethod, findablesDirectory, threadCount);
                Console.WriteLine("Setup successful.");
                dm.distributeCaseLogs(currentSession, Environment.UserName + " went through the initial setup procedure.");
                dm.updateCaseSessions(currentSession);
                return "Setup Successful.";
            }
            // Save settings to Settings.config
            
            
        }

        /// <summary>
        /// Checks if Python is installed.
        /// </summary>
        /// <returns>True if Python is installed, false otherwise</returns>
        static bool IsPythonInstalled()
        {
            try
            {
                Process.Start("python", "--version");
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if GPT4All is installed at the specified location.
        /// </summary>
        /// <param name="hostFileLocation">GPT4All host file location</param>
        /// <returns>True if GPT4All is installed, false otherwise</returns>
        static bool IsGPT4AllInstalled(string hostFileLocation)
        {
            return File.Exists(hostFileLocation + "\\bin\\chat.exe");
        }

        /// <summary>
        /// Suggests available models from the cache location.
        /// </summary>
        /// <param name="cacheLocation">Cache location for models</param>
        /// <returns>Selected model</returns>
        static string SuggestAvailableModels(string cacheLocation)
        {
            if (!Directory.Exists(cacheLocation))
            {
                return null;
            }

            var models = new string[0];
            if (!cacheLocation.ToLower().Contains("gpt4all"))
                models = Directory.GetFiles(cacheLocation + "\\gpt4all");
            else
                models = Directory.GetFiles(cacheLocation, "*.gguf");
            if (models.Length == 0)
            {
                return null;
            }

            Console.WriteLine("Available models in cache:");
            for (int i = 0; i < models.Length; i++)
            {
                Console.WriteLine($"{i + 1}. {Path.GetFileName(models[i])}");
            }

            Console.WriteLine("Enter the number of the model you want to use:");
            int modelNumber;
            while (!int.TryParse(Console.ReadLine().Trim(), out modelNumber) || modelNumber < 1 || modelNumber > models.Length)
            {
                Console.WriteLine("Invalid selection. Please enter a valid model number:");
            }

            return Path.GetFileName(models[modelNumber - 1]);
        }

        /// <summary>
        /// Tests the connection to the database.
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        /// <returns>True if the connection is successful, false otherwise</returns>
        static bool TestDatabaseConnection(string connectionString)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sets up the FlatFile storage.
        /// </summary>
        /// <param name="directory">Directory for FlatFile storage</param>
        static void SetupFlatFile(string directory)
        {
            FlatFileManager ffM = new FlatFileManager(directory);
            Console.WriteLine($"FlatFile storage setup completed in {directory}");
        }

        /// <summary>
        /// Sets up the database storage.
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        static void SetupDatabase(string connectionString)
        {
            SQLManager sQL = new SQLManager();
            sQL.EnsureTablesExist(connectionString);
            Console.WriteLine("Database setup completed.");
        }

        /// <summary>
        /// Saves the settings to the Settings.config file.
        /// </summary>
        /// <param name="hostFileLocation">GPT4All host file location</param>
        /// <param name="cacheLocation">GPT4All cache location</param>
        /// <param name="model">Selected model</param>
        /// <param name="storageMethod">Storage method</param>
        /// <param name="findablesDirectory">Directory for findable images</param>
        /// <param name="threadCount">CPU thread count</param>
        static void SaveSettings(string hostFileLocation, string cacheLocation, string model, string storageMethod, string findablesDirectory, int threadCount)
        {
            gp.updateSettingsFile("HostFileLocation", hostFileLocation);
            gp.updateSettingsFile("ModelRepository", cacheLocation);
            gp.updateSettingsFile("Model", model);
            gp.updateSettingsFile("StorageMethod", storageMethod);
            gp.updateSettingsFile("FindablesDirectory", findablesDirectory);
            gp.updateSettingsFile("ThreadCount", threadCount.ToString());
        }
        /// <summary>
        /// Displays the current settings from the Settings.config file.
        /// </summary>
        public static void displaySettings()
        {
            string filePath = AppDomain.CurrentDomain.BaseDirectory + "Settings.config";
            if (!File.Exists(filePath))
            {
                Console.WriteLine("Settings.config file not found.");
                return;
            }

            Console.WriteLine("Case Settings");
            Console.WriteLine("=============================================");

            try
            {
                using (StreamReader sr = File.OpenText(filePath))
                {
                    string s = "";
                    while ((s = sr.ReadLine()) != null)
                        Console.WriteLine(s);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading settings file: " + ex.Message);
            }

            Console.WriteLine("=============================================");
        }

        /// <summary>
        /// Lists available GPUs and allows the user to select one.
        /// </summary>
        public static void getGPUs()
        {
            // Create a new instance of the ManagementObjectSearcher class to query GPU information
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            List<string> gpus = new List<string>();

            try
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    string gpuName = obj["Name"].ToString();
                    gpus.Add(gpuName);
                    Console.WriteLine($"{gpus.Count}. Name: {obj["Name"]}");
                    Console.WriteLine($"   Status: {obj["Status"]}");
                    Console.WriteLine($"   DeviceID: {obj["DeviceID"]}");
                    Console.WriteLine($"   AdapterRAM: {obj["AdapterRAM"]}");
                    Console.WriteLine($"   DriverVersion: {obj["DriverVersion"]}");
                    Console.WriteLine("-----------------------------------");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving GPU information: " + ex.Message);
                return;
            }

            if (gpus.Count == 0)
            {
                Console.WriteLine("No GPUs found. Setting GPU to '', WARNING: This will use your CPU, and will take longer to load.");
                gp.updateSettingsFile("GPU", "");
            }
            else
            {
                Console.WriteLine("Enter the number of the GPU you want to use:");
                string input = Console.ReadLine();
                if (int.TryParse(input, out int gpuIndex) && gpuIndex > 0 && gpuIndex <= gpus.Count)
                {
                    string selectedGPU = gpus[gpuIndex - 1];
                    Console.WriteLine($"You selected: {selectedGPU}");
                    gp.updateSettingsFile("GPU", selectedGPU);
                }
                else
                {
                    Console.WriteLine("No GPUs found. Setting GPU to '', WARNING: This will use your CPU, and will take longer to load.");
                    gp.updateSettingsFile("GPU", "");
                }
            }
        }

    }
}
