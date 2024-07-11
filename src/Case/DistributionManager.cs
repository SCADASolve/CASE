//----------------------------------------------------------------------------
//  Copyright (C) 2024 by SCADA Solve LLC. All rights reserved.              
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Case
{
    /// <summary>
    /// Manages the distribution of various case commands, interactions, logs, and sessions.
    /// </summary>
    public class DistributionManager
    {
        GlobalParameters gP = new GlobalParameters();

        /// <summary>
        /// Distributes initialization logs to the appropriate storage method.
        /// </summary>
        /// <param name="timeToLoad">The time taken to load.</param>
        /// <param name="llm">The LLM value.</param>
        public void distributeInitLogs(string timeToLoad, string llm)
        {
            string pattern = @"[^0-9.]";
            timeToLoad = Regex.Replace(timeToLoad, pattern, "");
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sQL = new SQLManager();
                    string connectionString = gP.getSetting("SQLConnectionString");
                    sQL.InsertIntoCaseInitializationLog(connectionString, llm, DateTime.Now, false, true, timeToLoad);
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    FF_CaseInitializationLog cIL = new FF_CaseInitializationLog
                    {
                        DT = DateTime.Now,
                        LLM = llm,
                        Loading = false,
                        Loaded = true,
                        TimeToLoad = timeToLoad
                    };
                    flatFileManager.InsertIntoCaseInitializationLog(cIL);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error distributing initialization logs: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves available commands from the appropriate storage method.
        /// </summary>
        /// <returns>A list of available commands.</returns>
        public List<CaseCommand> GetAvailableCommands()
        {
            string storageMethod = gP.getSetting("StorageMethod");
            try
            {
                if (storageMethod == "Database")
                {
                    SQLManager sQL = new SQLManager();
                    return sQL.GetCommandsFromDatabase();
                }
                else if (storageMethod == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    return flatFileManager.GetCommandsFromFlatFile(ffPath);
                }
                else
                {
                    throw new InvalidOperationException("Invalid storage method.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting available commands: {ex.Message}");
                return new List<CaseCommand>();
            }
        }

        /// <summary>
        /// Detects keywords in a string and distributes the results.
        /// </summary>
        /// <param name="input">The input string to search for keywords.</param>
        /// <returns>A dictionary of detected keywords and their frequencies.</returns>
        public Dictionary<string, float> distributeDetectKeywordsInString(string input)
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sQL = new SQLManager();
                    string connectionString = gP.getSetting("SQLConnectionString");
                    return sQL.DetectKeywordsInString(connectionString, input);
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    return flatFileManager.DetectKeywordsInString(input);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting keywords in string: {ex.Message}");
            }
            return new Dictionary<string, float>();
        }

        /// <summary>
        /// Removes a command from the appropriate storage method.
        /// </summary>
        /// <param name="commandName">The name of the command to remove.</param>
        public void RemoveCommand(string commandName)
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sqlManager = new SQLManager();
                    sqlManager.RemoveCommand(commandName);
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    flatFileManager.RemoveCommand(commandName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing command: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes a monitor from the appropriate storage method.
        /// </summary>
        /// <param name="monitorName">The name of the monitor to remove.</param>
        public void RemoveMonitor(string monitorName)
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sqlManager = new SQLManager();
                    sqlManager.RemoveMonitor(monitorName);
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    flatFileManager.RemoveMonitor(monitorName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing monitor: {ex.Message}");
            }
        }

        /// <summary>
        /// Enqueues a command to the appropriate storage method.
        /// </summary>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="command">The command text.</param>
        /// <param name="user">The user associated with the command.</param>
        public void EnqueueCommand(string commandName, string command, string user)
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sQL = new SQLManager();
                    sQL.EnqueueCommand(commandName, command, user);
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    flatFileManager.EnqueueCommand(commandName, command, user);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enqueuing command: {ex.Message}");
            }
        }
        /// <summary>
        /// Gets the command queue from the appropriate storage method.
        /// </summary>
        /// <returns>A list of CommandQueueRecord objects.</returns>
        public List<dynamic> GetMonitorQueue()
        {
            string sM = gP.getSetting("StorageMethod");
            string cn = gP.getSetting("SQLConnectionString");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sqlManager = new SQLManager();
                    return sqlManager.GetMonitorQueue();
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    return flatFileManager.GetMonitorQueue();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting command queue: {ex.Message}");
            }
            return new List<dynamic>();
        }

        /// <summary>
        /// Flushes Command Queue
        /// </summary>
        /// <returns>A list of CommandQueueRecord objects.</returns>
        public void FlushCommandQueue()
        {
            string sM = gP.getSetting("StorageMethod");
            string cn = gP.getSetting("SQLConnectionString");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sqlManager = new SQLManager();
                    sqlManager.FlushCommandQueue();
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    flatFileManager.FlushCommands();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error flushing command queue: {ex.Message}");
            }
        }
        /// <summary>
        /// Flushes Monitor Queue
        /// </summary>
        /// <returns>A list of CommandQueueRecord objects.</returns>
        public void FlushMonitorQueue()
        {
            string sM = gP.getSetting("StorageMethod");
            string cn = gP.getSetting("SQLConnectionString");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sqlManager = new SQLManager();
                    sqlManager.FlushMonitorQueue();
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    flatFileManager.FlushMonitors();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error flushing monitor queue: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the command queue from the appropriate storage method.
        /// </summary>
        /// <returns>A list of CommandQueueRecord objects.</returns>
        public List<CommandQueueRecord> GetCommandQueue()
        {
            string sM = gP.getSetting("StorageMethod");
            string cn = gP.getSetting("SQLConnectionString");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sqlManager = new SQLManager();
                    return sqlManager.GetCommandQueue(cn);
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    return flatFileManager.GetCommandQueue();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting command queue: {ex.Message}");
            }
            return new List<CommandQueueRecord>();
        }

        /// <summary>
        /// Processes the command queue from the appropriate storage method.
        /// </summary>
        /// <param name="rPA">The RPA manager to execute the commands.</param>
        /// <param name="currentSession">The current session for logging purposes.</param>
        public void ProcessCommandQueue(RPA rPA, Guid currentSession)
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sqlManager = new SQLManager();
                    var commandQueue = sqlManager.GetCommandQueue(gP.getSetting("SQLConnectionString"));

                    foreach (var commandRecord in commandQueue)
                    {
                        string commandToProcess = commandRecord.CommandName;
                        Console.WriteLine($"Processing command: {commandToProcess}");
                        rPA.runRPA(commandToProcess);
                        distributeCaseLogs(currentSession, "Executed from queue: " + commandToProcess);
                    }

                    while (sqlManager.DequeueCommand() is string commandToProcess && !string.IsNullOrEmpty(commandToProcess))
                    {
                        Console.WriteLine($"Processing command: {commandToProcess}");
                        rPA.runRPA(commandToProcess);
                        distributeCaseLogs(currentSession, "Executed from queue: " + commandToProcess);
                    }
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    var commandQueue = flatFileManager.GetCommandQueue();
                    foreach (var commandRecord in commandQueue)
                    {
                        string commandToProcess = commandRecord.CommandName;
                        Console.WriteLine($"Processing command: {commandToProcess}");
                        rPA.runRPA(commandToProcess);
                        distributeCaseLogs(currentSession, "Executed from queue: " + commandToProcess);
                    }
                    while (flatFileManager.DequeueCommand() is string commandToProcess && !string.IsNullOrEmpty(commandToProcess))
                    {
                        Console.WriteLine($"Processing command: {commandToProcess}");
                        rPA.runRPA(commandToProcess);
                        distributeCaseLogs(currentSession, "Executed from queue: " + commandToProcess);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing command queue: {ex.Message}");
            }
        }

        /// <summary>
        /// Dequeues the next command from the appropriate storage method.
        /// </summary>
        /// <returns>The dequeued command, or null if no command is available.</returns>
        public string DequeueCommand()
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sqlManager = new SQLManager();
                    return sqlManager.DequeueCommand();
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    return flatFileManager.DequeueCommand();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error dequeuing command: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Enqueues a monitor to the appropriate storage method.
        /// </summary>
        /// <param name="monitorName">The name of the monitor.</param>
        /// <param name="monitorType">The type of the monitor.</param>
        /// <param name="monitorData1">The first monitor data.</param>
        /// <param name="monitorData2">The second monitor data.</param>
        /// <param name="command">The command associated with the monitor.</param>
        /// <param name="user">The user associated with the monitor.</param>
        public void EnqueueMonitor(string monitorName, string monitorType, string monitorData1, string monitorData2, string command, string user)
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sQL = new SQLManager();
                    sQL.EnqueueMonitor(monitorName, monitorType, monitorData1, monitorData2, command, user);
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    flatFileManager.EnqueueMonitor(monitorName, monitorType, monitorData1, monitorData2, command, user);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enqueuing monitor: {ex.Message}");
            }
        }


        /// <summary>
        /// Updates the activation status of a monitor in the appropriate storage method.
        /// </summary>
        /// <param name="monitorName">The name of the monitor.</param>
        /// <param name="activated">The activation status to set.</param>
        public void UpdateMonitorActivationStatus(string monitorName, bool activated)
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sqlManager = new SQLManager();
                    sqlManager.UpdateMonitorActivationStatus(monitorName, activated);
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    flatFileManager.UpdateMonitorActivationStatus(monitorName, activated);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating monitor activation status: {ex.Message}");
            }
        }

        /// <summary>
        /// Distributes case commands to the appropriate storage method.
        /// </summary>
        /// <param name="command">The command text.</param>
        /// <param name="codeToExecute">The code to execute.</param>
        /// <param name="keywords">The keywords associated with the command.</param>
        /// <param name="software">The software associated with the command.</param>
        /// <param name="lastUsed">The last used date of the command.</param>
        /// <param name="dynamic">Indicates if the command is dynamic.</param>
        public void distributeCaseCommands(string command, string codeToExecute, string keywords, string software, DateTime lastUsed, bool dynamic)
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sQL = new SQLManager();
                    string connectionString = gP.getSetting("SQLConnectionString");
                    sQL.InsertIntoCaseCommands(connectionString, command, keywords, codeToExecute, software, lastUsed, dynamic);
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    FF_CaseCommands cC = new FF_CaseCommands
                    {
                        Command = command,
                        CodeToExecute = codeToExecute,
                        Software = software,
                        LastUsed = lastUsed,
                        Dynamic = dynamic
                    };
                    flatFileManager.InsertIntoCaseCommands(cC);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error distributing case commands: {ex.Message}");
            }
        }

        /// <summary>
        /// Interactively creates case commands and stores them in the appropriate storage method.
        /// </summary>
        public void distributeInteractiveCreateCaseCommands()
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sQL = new SQLManager();
                    string connectionString = gP.getSetting("SQLConnectionString");
                    sQL.InteractiveCreateCaseCommands(connectionString);
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    flatFileManager.InteractiveCreateCaseCommands();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error interactively creating case commands: {ex.Message}");
            }
        }

        /// <summary>
        /// Interactively updates case commands and stores them in the appropriate storage method.
        /// </summary>
        public void distributeInteractiveUpdateCaseCommands()
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sQL = new SQLManager();
                    string connectionString = gP.getSetting("SQLConnectionString");
                    sQL.InteractiveUpdateCaseCommands(connectionString);
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    flatFileManager.InteractiveUpdateCaseCommands();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error interactively updating case commands: {ex.Message}");
            }
        }

        /// <summary>
        /// Distributes case interactions to the appropriate storage method.
        /// </summary>
        /// <param name="username">The username associated with the interaction.</param>
        /// <param name="message">The message of the interaction.</param>
        /// <param name="screen">The screen associated with the interaction.</param>
        /// <param name="dt">The date and time of the interaction.</param>
        /// <param name="receiver">The receiver of the interaction.</param>
        /// <param name="submitted">Indicates if the interaction is submitted.</param>
        /// <param name="waitingOnResponse">Indicates if the interaction is waiting for a response.</param>
        /// <param name="answered">Indicates if the interaction is answered.</param>
        /// <param name="actionPerformed">Indicates if an action was performed.</param>
        /// <param name="timeToProcess">The time taken to process the interaction.</param>
        /// <param name="commandsActivated">The commands activated during the interaction.</        /// <param name="id">The unique identifier for the interaction.</param>
        public void distributeCaseInteractions(string username, string message, string screen, DateTime dt, string receiver, bool submitted, bool waitingOnResponse, bool answered, bool actionPerformed, string timeToProcess, string commandsActivated, Guid id)
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sQL = new SQLManager();
                    string connectionString = gP.getSetting("SQLConnectionString");
                    sQL.InsertIntoCaseInteractions(connectionString, username, message, screen, dt, receiver, submitted, waitingOnResponse, answered, actionPerformed, timeToProcess, commandsActivated, id);
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    FF_CaseInteractions cI = new FF_CaseInteractions
                    {
                        Username = username,
                        Message = message,
                        Screen = screen,
                        DT = dt,
                        Receiver = receiver,
                        Submitted = submitted,
                        WaitingOnResponse = waitingOnResponse,
                        Answered = answered,
                        ActionPerformed = actionPerformed,
                        TimeToProcess = timeToProcess,
                        CommandsActivated = commandsActivated,
                        Id = id
                    };
                    flatFileManager.InsertIntoCaseInteractions(cI);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error distributing case interactions: {ex.Message}");
            }
        }

        /// <summary>
        /// Distributes case logs to the appropriate storage method.
        /// </summary>
        /// <param name="sessionId">The session ID associated with the log.</param>
        /// <param name="logMessage">The log message.</param>
        public void distributeCaseLogs(Guid sessionId, string logMessage)
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sQL = new SQLManager();
                    string connectionString = gP.getSetting("SQLConnectionString");
                    sQL.InsertIntoCaseLogs(connectionString, sessionId, logMessage);
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    FF_CaseLogs cL = new FF_CaseLogs
                    {
                        SessionId = sessionId,
                        LogMessage = logMessage
                    };
                    flatFileManager.InsertIntoCaseLogs(cL);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error distributing case logs: {ex.Message}");
            }
        }

        /// <summary>
        /// Distributes case sessions to the appropriate storage method.
        /// </summary>
        /// <param name="session">The session ID.</param>
        /// <param name="startDT">The start date and time of the session.</param>
        /// <param name="endDT">The end date and time of the session.</param>
        /// <param name="username">The username associated with the session.</param>
        public void distributeCaseSessions(Guid session, DateTime startDT, DateTime endDT, string username)
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sQL = new SQLManager();
                    string connectionString = gP.getSetting("SQLConnectionString");
                    sQL.InsertIntoCaseSessions(connectionString, session, startDT, endDT, username);
                }
                else if (sM == "FlatFile")
                {
                    string ffPath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(ffPath);
                    FF_CaseSessions cS = new FF_CaseSessions
                    {
                        Session = session,
                        StartDT = startDT,
                        EndDT = endDT,
                        Username = username
                    };
                    flatFileManager.InsertIntoCaseSessions(cS);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error distributing case sessions: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the end date and time of a session in the appropriate storage method.
        /// </summary>
        /// <param name="session">The session ID.</param>
        public void updateCaseSessions(Guid session)
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sQL = new SQLManager();
                    string connectionString = gP.getSetting("SQLConnectionString");
                    sQL.UpdateCaseSessions(connectionString, session, DateTime.Now);
                }
                else if (sM == "FlatFile")
                {
                    string basePath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(basePath);
                    flatFileManager.UpdateCaseSession(session);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating case sessions: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates case interactions in the appropriate storage method.
        /// </summary>
        /// <param name="session">The session ID.</param>
        public void updateCaseInteractions(Guid session)
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sQL = new SQLManager();
                    string connectionString = gP.getSetting("SQLConnectionString");
                    sQL.UpdateCaseSessions(connectionString, session, DateTime.Now);
                }
                else if (sM == "FlatFile")
                {
                    string basePath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(basePath);
                    flatFileManager.UpdateCaseSession(session);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating case interactions: {ex.Message}");
            }
        }

        /// <summary>
        /// Collects RPA commands for a given command name from the appropriate storage method.
        /// </summary>
        /// <param name="commandName">The name of the command.</param>
        /// <returns>The RPA commands.</returns>
        public string collectRPACommands(string commandName, bool checkIfValid = false)
        {
            string sM = gP.getSetting("StorageMethod");
            string result = "";
            try
            {
                if (sM == "Database")
                {
                    SQLManager sQL = new SQLManager();
                    string connectionString = gP.getSetting("SQLConnectionString");
                    result = sQL.GetCodeToExecute(connectionString, commandName);
                    if (!checkIfValid)
                        sQL.UpdateCommandLastUsedSQL(connectionString, commandName);
                }
                else if (sM == "FlatFile")
                {
                    string basePath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(basePath);
                    result = flatFileManager.GetCodeToExecuteFromFile(commandName);
                    if (!checkIfValid)
                        flatFileManager.UpdateCommandLastUsed(commandName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error collecting RPA commands: {ex.Message}");
            }
            return result;
        }
        public object collectCommandProperties(string commandName)
        {
            string sM = gP.getSetting("StorageMethod");
            try
            {
                if (sM == "Database")
                {
                    SQLManager sQL = new SQLManager();
                    string connectionString = gP.getSetting("SQLConnectionString");
                    return sQL.GetCommandProperties(connectionString, commandName);
                }
                else if (sM == "FlatFile")
                {
                    string basePath = gP.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(basePath);
                    return flatFileManager.GetCommandPropertiesFromFile(commandName);
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error collecting RPA commands: {ex.Message}");
                return null;
            }
        }
    }
}
