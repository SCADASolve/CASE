//----------------------------------------------------------------------------
//  Copyright (C) 2024 by SCADA Solve LLC. All rights reserved.              
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Data.SqlClient;
namespace Case
{
    public class ServerClass
    {
        private GlobalParameters gp;
        private DistributionManager dm;
        private RPA rpa;
        private Dictionary<string, Guid> userSessions;

        /// <summary>
        /// Initializes a new instance of the ServerClass.
        /// </summary>
        public ServerClass()
        {
            gp = new GlobalParameters();
            dm = new DistributionManager();
            rpa = new RPA();
            userSessions = new Dictionary<string, Guid>();
        }

        /// <summary>
        /// Starts the server, continuously checking for commands to process.
        /// </summary>
        public void StartServer()
        {
            Console.WriteLine("Starting server...");
            while (true)
            {
                CheckForCommands();
                Thread.Sleep(10000);
            }
        }

        /// <summary>
        /// Checks for commands based on the storage method setting (Database or FlatFile).
        /// </summary>
        private void CheckForCommands()
        {
            string storageMethod = gp.getSetting("StorageMethod");

            if (storageMethod == "Database")
            {
                CheckDatabaseCommands();
            }
            else if (storageMethod == "FlatFile")
            {
                CheckFlatFileCommands();
            }
        }

        /// <summary>
        /// Checks the database for new commands to process.
        /// </summary>
        private void CheckDatabaseCommands()
        {
            string connectionString = gp.getSetting("SQLConnectionString");
            DateTime lastCheckTime = DateTime.UtcNow.AddMinutes(-1);

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT message, [username] FROM CaseInteractions WHERE receiver = 'case' AND answered IS NULL AND waitingonresponse = 1";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@lastCheckTime", lastCheckTime);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string message = reader.GetString(0);
                                string username = reader.GetString(1);
                                ProcessCommand(message, username);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking database commands: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks a flat file for new commands to process.
        /// </summary>
        private void CheckFlatFileCommands()
        {
            string flatFilePath = gp.getSetting("FlatFilePath");
            try
            {
                FlatFileManager flatFileManager = new FlatFileManager(flatFilePath);
                List<FF_CaseInteractions> interactions = flatFileManager.GetPendingCaseInteractions("case", DateTime.UtcNow.AddMinutes(-1));

                foreach (var interaction in interactions)
                {
                    ProcessCommand(interaction.Message, interaction.Username);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking flat file commands: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a command from a user.
        /// </summary>
        /// <param name="command">The command to process.</param>
        /// <param name="username">The username associated with the command.</param>
        private void ProcessCommand(string command, string username)
        {
            Console.WriteLine($"Processing command from {username}: {command}");

            InitializeUserSession(username);
            string rawCommand = dm.collectRPACommands(command);
            if (!string.IsNullOrEmpty(rawCommand))
            {
                rpa.runRPA(rawCommand);
                LogCommandResponse(username, command, "executed");
            }
            else
            {
                Console.WriteLine($"Unknown command: {command}");
            }
        }

        /// <summary>
        /// Initializes a session for a user if it does not already exist.
        /// </summary>
        /// <param name="username">The username to initialize a session for.</param>
        private void InitializeUserSession(string username)
        {
            DateTime now = DateTime.Now;
            if (!userSessions.ContainsKey(username))
            {
                userSessions[username] = Guid.NewGuid();
                dm.distributeCaseSessions(userSessions[username], now, now, username);
            }
        }

        /// <summary>
        /// Logs the response of a command execution to the database or a flat file.
        /// </summary>
        /// <param name="username">The username associated with the command.</param>
        /// <param name="command">The command that was executed.</param>
        /// <param name="response">The response of the command execution.</param>
        private void LogCommandResponse(string username, string command, string response)
        {
            string timeToRun = "0 seconds"; // Placeholder for actual execution time
            string connectionString = gp.getSetting("SQLConnectionString");

            try
            {
                if (gp.getSetting("StorageMethod") == "Database")
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        string insertQuery = "INSERT INTO CaseInteractions (dt, message, [username], receiver, submitted, waitingonresponse, timetoprocess, answered) VALUES (GETDATE(), @Message, 'case', @Username, 1, 0, @TimeToRun, 1)";
                        using (SqlCommand command2 = new SqlCommand(insertQuery, connection))
                        {
                            command2.Parameters.AddWithValue("@Message", response);
                            command2.Parameters.AddWithValue("@Username", username);
                            command2.Parameters.AddWithValue("@TimeToRun", timeToRun);
                            command2.ExecuteNonQuery();
                        }
                    }
                }
                else if (gp.getSetting("StorageMethod") == "FlatFile")
                {
                    string flatFilePath = gp.getSetting("FlatFilePath");
                    FlatFileManager flatFileManager = new FlatFileManager(flatFilePath);
                    flatFileManager.InsertCaseInteractionResponse(DateTime.Now, response, username, timeToRun, response);
                }

                dm.distributeCaseLogs(userSessions[username], $"Executed command: {command}, Response: {response}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging command response: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs a message for a user.
        /// </summary>
        /// <param name="username">The username to log the message for.</param>
        /// <param name="message">The message to log.</param>
        private void LogMessage(string username, string message)
        {
            try
            {
                dm.distributeCaseLogs(userSessions[username], message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging message: {ex.Message}");
            }
        }
    }

}
