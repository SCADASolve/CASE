//----------------------------------------------------------------------------
//  Copyright (C) 2024 by SCADA Solve LLC. All rights reserved.              
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlClient;

namespace Case
{
    public class SQLManager
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
        public static GlobalParameters gpa = new GlobalParameters();

        /// <summary>
        /// Ensures the stability of the SQL connection, including testing the connection, database, and tables.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <param name="databaseName">The name of the database to test the connection.</param>
        /// <returns>True if the connection, database, and tables are accessible, otherwise false.</returns>
        public static bool CheckSQLStability(string connectionString, string databaseName)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Please enter the connection string:");
                connectionString = Console.ReadLine();
                gpa.updateSettingsFile("SQLConnectionString", connectionString);
            }

            string[] tableNames = { "CaseCommands", "CaseInitializationLog", "CaseInteractions", "CaseLogs", "CaseSessions" };
            if (TestSqlConnection(connectionString) &&
                TestDatabaseConnection(connectionString, databaseName) &&
                TestTableAccess(connectionString, tableNames))
            {
                SQLManager sql = new SQLManager();
                return sql.EnsureTablesExist(connectionString);
            }
            return false;
        }

        /// <summary>
        /// Retrieves a list of commands from the database.
        /// </summary>
        /// <returns>A list of CaseCommand objects retrieved from the database.</returns>
        public List<CaseCommand> GetCommandsFromDatabase()
        {
            GlobalParameters gP = new GlobalParameters();
            List<CaseCommand> commands = new List<CaseCommand>();
            string connectionString = gP.getSetting("SQLConnectionString");
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT Command, CodeToExecute, Keywords, Software, LastUsed, Dynamic FROM CaseCommands";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                commands.Add(new CaseCommand
                                {
                                    Command = reader.GetString(0),
                                    CodeToExecute = reader.GetString(1),
                                    Keywords = reader.GetString(2),
                                    Software = reader.GetString(3),
                                    LastUsed = reader.GetDateTime(4),
                                    Dynamic = reader.GetBoolean(5)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving commands from database: {ex.Message}");
            }
            return commands;
        }

        /// <summary>
        /// Tests the SQL connection using the provided connection string.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <returns>True if the connection is successful, otherwise false.</returns>
        private static bool TestSqlConnection(string connectionString)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error connecting to SQL Server: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Tests the connection to a specific database.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <param name="databaseName">The name of the database to test the connection.</param>
        /// <returns>True if the connection to the database is successful, otherwise false.</returns>
        private static bool TestDatabaseConnection(string connectionString, string databaseName)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    return conn.Database == databaseName;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error connecting to database: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Tests if the specified tables can be accessed.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <param name="tables">An array of table names to test access.</param>
        /// <returns>True if all tables can be accessed, otherwise false.</returns>
        private static bool TestTableAccess(string connectionString, string[] tables)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    foreach (string table in tables)
                    {
                        using (SqlCommand cmd = new SqlCommand($"SELECT 1 FROM {table} WHERE 1 = 0", conn))
                        {
                            cmd.ExecuteNonQuery();  // Just to check access, no data needed
                        }
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing tables: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a specific table exists in the database.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <param name="tableName">The name of the table to check existence.</param>
        /// <returns>True if the table exists, otherwise false.</returns>
        public bool TableExists(string connectionString, string tableName)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
                SELECT CASE 
                    WHEN EXISTS (
                        SELECT * 
                        FROM INFORMATION_SCHEMA.TABLES 
                        WHERE TABLE_NAME = @TableName
                    ) THEN 1
                    ELSE 0
                END";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@TableName", tableName);
                        int result = (int)cmd.ExecuteScalar();
                        return result == 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking table existence: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Ensures that all required tables exist in the database, creating them if necessary.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <returns>True if the tables exist or are created successfully, otherwise false.</returns>
        public bool EnsureTablesExist(string connectionString)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string[] createTableCommands = new string[]
                    {
                    @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'CaseCommands')
                    BEGIN
                        CREATE TABLE dbo.CaseCommands(
                            [Command] NVARCHAR(MAX) NULL,
                            [Keywords] NVARCHAR(MAX) NULL,
                            [codeToExecute] NVARCHAR(MAX) NULL,
                            [Software] NVARCHAR(MAX) NULL,
                            [LastUsed] DATETIME NULL,
                            [Dynamic] BIT NULL
                        ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
                    END
                    ",
                    @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'CaseInitializationLog')
                    BEGIN
                        CREATE TABLE dbo.CaseInitializationLog(
                            [llm] NVARCHAR(20) NULL,
                            [dt] DATETIME NULL,
                            [loading] BIT NULL,
                            [loaded] BIT NULL,
                            [timeToLoad] NVARCHAR(50) NULL
                        ) ON [PRIMARY]
                    END
                    ",
                    @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'CaseInteractions')
                    BEGIN
                        CREATE TABLE dbo.CaseInteractions(
                            [Username] NVARCHAR(20) NOT NULL,
                            [Message] NVARCHAR(MAX) NULL,
                            [Screen] NVARCHAR(255) NULL,
                            [DT] DATETIME NULL,
                            [Receiver] NVARCHAR(20) NULL,
                            [Submitted] BIT NULL,
                            [WaitingOnResponse] BIT NULL,
                            [Answered] BIT NULL,
                            [ActionPerformed] BIT NULL,
                            [TimeToProcess] NVARCHAR(50) NULL,
                            [CommandsActivated] NVARCHAR(MAX) NULL,
                            [id] UNIQUEIDENTIFIER NULL
                        ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
                    END
                    ",
                    @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'CaseLogs')
                    BEGIN
                        CREATE TABLE dbo.CaseLogs(
                            [sessionid] UNIQUEIDENTIFIER NULL,
                            [logmessage] NVARCHAR(MAX) NULL
                        ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
                    END
                    ",
                    @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'CaseSessions')
                    BEGIN
                        CREATE TABLE dbo.CaseSessions(
                            [session] UNIQUEIDENTIFIER NOT NULL,
                            [startDT] DATETIME NULL,
                            [endDT] DATETIME NULL,
                            [username] NVARCHAR(50) NULL
                        ) ON [PRIMARY];
                        ALTER TABLE dbo.CaseSessions ADD CONSTRAINT DF_CaseSessions_session DEFAULT (newid()) FOR [session]
                    END
                    ",
                    @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'CommandQueue')
                    BEGIN
                        CREATE TABLE dbo.CommandQueue(
                            [ID] INT IDENTITY(1,1) PRIMARY KEY,
                            [CommandName] NVARCHAR(255) NOT NULL,
                            [Command] NVARCHAR(MAX) NOT NULL,
                            [User] NVARCHAR(255) NOT NULL,
                            [Timestamp] DATETIME NOT NULL
                        ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
                    END
                    ",
                    @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'MonitorQueue')
                    BEGIN
                        CREATE TABLE dbo.MonitorQueue(
                            [ID] INT IDENTITY(1,1) PRIMARY KEY,
                            [MonitorName] NVARCHAR(255) NOT NULL,
                            [MonitorType] NVARCHAR(50) NOT NULL,
                            [MonitorData1] NVARCHAR(MAX) NOT NULL,
                            [MonitorData2] NVARCHAR(MAX) NULL,
                            [Command] NVARCHAR(MAX) NOT NULL,
                            [User] NVARCHAR(255) NOT NULL,
                            [Activated] BIT NOT NULL,
                            [Timestamp] DATETIME NOT NULL
                        ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
                    END
                    "
                    };

                    foreach (string cmdText in createTableCommands)
                    {
                        using (SqlCommand cmd = new SqlCommand(cmdText, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ensuring tables exist: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Inserts a new command into the CaseCommands table.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <param name="command">The command to insert.</param>
        /// <param name="keywords">The keywords associated with the command.</param>
        /// <param name="codeToExecute">The code to execute for the command.</param>
        /// <param name="software">The software associated with the command.</param>
        /// <param name="lastUsed">The last used date of the command.</param>
        /// <param name="dynamic">Indicates if the command is dynamic.</param>
        public void InsertIntoCaseCommands(string connectionString, string command, string keywords, string codeToExecute, string software, DateTime lastUsed, bool dynamic)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Please enter the connection string:");
                connectionString = Console.ReadLine();
                gpa.updateSettingsFile("SQLConnectionString", connectionString);
            }
            EnsureTablesExist(connectionString);
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO dbo.CaseCommands (Command, Keywords, codeToExecute, Software, LastUsed, Dynamic) VALUES (@Command, @Keywords, @codeToExecute, @Software, @LastUsed, @Dynamic)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Command", command);
                        cmd.Parameters.AddWithValue("@Keywords", keywords);
                        cmd.Parameters.AddWithValue("@codeToExecute", codeToExecute);
                        cmd.Parameters.AddWithValue("@Software", software);
                        cmd.Parameters.AddWithValue("@LastUsed", lastUsed);
                        cmd.Parameters.AddWithValue("@Dynamic", dynamic);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting into CaseCommands: {ex.Message}");
            }
        }

        /// <summary>
        /// Monitors a specific SQL table with a condition for a specified timeout.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <param name="tableName">The name of the table to monitor.</param>
        /// <param name="whereStatement">The condition to monitor in the table.</param>
        /// <param name="timeout">The timeout period in seconds.</param>
        /// <returns>True if the condition is met within the timeout, otherwise false.</returns>
        public bool MonitorSQL(string connectionString, string tableName, string whereStatement, int timeout = 0)
        {
            if (!TableExists(connectionString, tableName))
            {
                Console.WriteLine($"Table: {tableName} does not exist.");
                return false;
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Please enter the connection string:");
                connectionString = Console.ReadLine();
                gpa.updateSettingsFile("SQLConnectionString", connectionString);
            }
            Console.WriteLine($"Monitoring SQL table {tableName} with condition {whereStatement}");
            var startTime = DateTime.Now;
            try
            {
                while (true)
                {
                    if (timeout > 0 && (DateTime.Now - startTime).TotalSeconds >= timeout)
                    {
                        Console.WriteLine("Timeout reached.");
                        return false;
                    }
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        string query = $"SELECT COUNT(*) FROM {tableName} WHERE {whereStatement}";

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            int count = (int)cmd.ExecuteScalar();
                            if (count > 0)
                            {
                                return true;
                            }
                        }
                    }
                    System.Threading.Thread.Sleep(1000);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed due to: " + e.Message.ToString());
                return false;
            }
        }

        /// <summary>
        /// Retrieves the code to execute for a specific command from the CaseCommands table.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <param name="command">The command to retrieve the code for.</param>
        /// <returns>The code to execute for the command, or null if not found.</returns>
        public string GetCodeToExecute(string connectionString, string command)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Please enter the connection string:");
                connectionString = Console.ReadLine();
                gpa.updateSettingsFile("SQLConnectionString", connectionString);
            }
            EnsureTablesExist(connectionString);
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT codeToExecute FROM dbo.CaseCommands WHERE Command = @Command";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Command", command);
                        object result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            return result.ToString();
                        }
                        else
                        {
                            Console.WriteLine("No matching command found.");
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving code to execute: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Retrieves Case Command Properties from Table.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <param name="command">The command to retrieve the code for.</param>
        /// <returns>A string of data corresponding with the command variables</returns>
        public FF_CaseCommands GetCommandProperties(string connectionString, string command)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Please enter the connection string:");
                connectionString = Console.ReadLine();
                gpa.updateSettingsFile("SQLConnectionString", connectionString);
            }

            EnsureTablesExist(connectionString);

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT [Command], [Keywords], [codeToExecute], [Software], [LastUsed], [Dynamic]
                        FROM dbo.CaseCommands
                        WHERE Command = @Command";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Command", command);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var commandProperties = new FF_CaseCommands
                                {
                                    Command = reader["Command"].ToString(),
                                    Keywords = reader["Keywords"].ToString(),
                                    CodeToExecute = reader["codeToExecute"].ToString(),
                                    Software = reader["Software"].ToString(),
                                    LastUsed = reader["LastUsed"] != DBNull.Value ? (DateTime)reader["LastUsed"] : DateTime.Now,
                                    Dynamic = reader["Dynamic"] != DBNull.Value ? (bool)reader["Dynamic"] : false
                                };

                                return commandProperties;
                            }
                            else
                            {
                                Console.WriteLine("No matching command found.");
                                return null;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving command properties: {ex.Message}");
                return null;
            }
        }


        /// <summary>
        /// Inserts a new log entry into the CaseInitializationLog table.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <param name="llm">The LLM value.</param>
        /// <param name="dt">The date and time of the log entry.</param>
        /// <param name="loading">Indicates if loading is true.</param>
        /// <param name="loaded">Indicates if loaded is true.</param>
        /// <param name="timeToLoad">The time taken to load.</param>
        public void InsertIntoCaseInitializationLog(string connectionString, string llm, DateTime dt, bool loading, bool loaded, string timeToLoad)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Please enter the connection string:");
                connectionString = Console.ReadLine();
                gpa.updateSettingsFile("SQLConnectionString", connectionString);
            }
            EnsureTablesExist(connectionString);
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO dbo.CaseInitializationLog (llm, dt, loading, loaded, timeToLoad) VALUES (@llm, @dt, @loading, @loaded, @timeToLoad)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@llm", llm);
                        cmd.Parameters.AddWithValue("@dt", dt);
                        cmd.Parameters.AddWithValue("@loading", loading);
                        cmd.Parameters.AddWithValue("@loaded", loaded);
                        cmd.Parameters.AddWithValue("@timeToLoad", timeToLoad);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting into CaseInitializationLog: {ex.Message}");
            }
        }

        /// <summary>
        /// Inserts a new interaction record into the CaseInteractions table.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <param name="username">The username associated with the interaction.</param>
        /// <param name="message">The message of the interaction.</param>
        /// <param name="screen">The screen associated with the interaction.</param>
        /// <param name="dt">The date and time of the interaction.</param>
        /// <param name="receiver">The receiver of the interaction.</param>
        /// <param name="submitted">Indicates if the interaction is submitted.</param>
        /// <param name="waitingOnResponse">Indicates if the interaction is waiting on a response.</param>
        /// <param name="answered">Indicates if the interaction is answered.</param>
        /// <param name="actionPerformed">Indicates if an action was performed.</param>
        /// <param name="timeToProcess">The time taken to process the interaction.</param>
        /// <param name="commandsActivated">The commands activated during the interaction.</param>
        /// <param name="id">The unique identifier of the interaction.</param>
        public void InsertIntoCaseInteractions(string connectionString, string username, string message, string screen, DateTime dt, string receiver, bool submitted, bool waitingOnResponse, bool answered, bool actionPerformed, string timeToProcess, string commandsActivated, Guid id)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Please enter the connection string:");
                connectionString = Console.ReadLine();
                gpa.updateSettingsFile("SQLConnectionString", connectionString);
            }
            EnsureTablesExist(connectionString);
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO dbo.CaseInteractions (Username, Message, Screen, DT, Receiver, Submitted, WaitingOnResponse, Answered, ActionPerformed, TimeToProcess, CommandsActivated, id) VALUES (@Username, @Message, @Screen, @DT, @Receiver, @Submitted, @WaitingOnResponse, @Answered, @ActionPerformed, @TimeToProcess, @CommandsActivated, @id)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Username", username);
                        cmd.Parameters.AddWithValue("@Message", message);
                        cmd.Parameters.AddWithValue("@Screen", screen);
                        cmd.Parameters.AddWithValue("@DT", dt);
                        cmd.Parameters.AddWithValue("@Receiver", receiver);
                        cmd.Parameters.AddWithValue("@Submitted", submitted);
                        cmd.Parameters.AddWithValue("@WaitingOnResponse", waitingOnResponse);
                        cmd.Parameters.AddWithValue("@Answered", answered);
                        cmd.Parameters.AddWithValue("@ActionPerformed", actionPerformed);
                        cmd.Parameters.AddWithValue("@TimeToProcess", timeToProcess);
                        cmd.Parameters.AddWithValue("@CommandsActivated", commandsActivated);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting into CaseInteractions: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the last used date for a specific command in the CaseCommands table.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <param name="commandName">The name of the command to update.</param>
        public void UpdateCommandLastUsedSQL(string connectionString, string commandName)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Please enter the connection string:");
                connectionString = Console.ReadLine();
                gpa.updateSettingsFile("SQLConnectionString", connectionString);
            }
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "UPDATE dbo.CaseCommands SET LastUsed = @LastUsed WHERE Command = @Command";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@LastUsed", DateTime.Now);
                        cmd.Parameters.AddWithValue("@Command", commandName);
                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            Console.WriteLine($"Last used date for command '{commandName}' updated successfully.");
                        }
                        else
                        {
                            Console.WriteLine($"Command '{commandName}' not found.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating command last used: {ex.Message}");
            }
        }

        /// <summary>
        /// Interactively creates a new command in the CaseCommands table.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        public void InteractiveCreateCaseCommands(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Please enter the connection string:");
                connectionString = Console.ReadLine();
                gpa.updateSettingsFile("SQLConnectionString", connectionString);
            }
            EnsureTablesExist(connectionString);

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

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO dbo.CaseCommands (Command, Keywords, codeToExecute, Software, LastUsed, Dynamic) VALUES (@Command, @Keywords, @codeToExecute, @Software, @LastUsed, @Dynamic)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Command", command);
                        cmd.Parameters.AddWithValue("@Keywords", keywords);
                        cmd.Parameters.AddWithValue("@codeToExecute", codeToExecute);
                        cmd.Parameters.AddWithValue("@Software", software);
                        cmd.Parameters.AddWithValue("@LastUsed", lastUsed);
                        cmd.Parameters.AddWithValue("@Dynamic", dynamic);
                        cmd.ExecuteNonQuery();
                    }
                }

                Console.WriteLine("Command created successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating command: {ex.Message}");
            }
        }

        /// <summary>
        /// Inserts a new log entry into the CaseLogs table.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <param name="sessionid">The session ID associated with the log entry.</param>
        /// <param name="logmessage">The log message to insert.</param>
        public void InsertIntoCaseLogs(string connectionString, Guid sessionid, string logmessage)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Please enter the connection string:");
                connectionString = Console.ReadLine();
                gpa.updateSettingsFile("SQLConnectionString", connectionString);
            }
            EnsureTablesExist(connectionString);
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO dbo.CaseLogs (sessionid, logmessage) VALUES (@sessionid, @logmessage)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@sessionid", sessionid);
                        cmd.Parameters.AddWithValue("@logmessage", logmessage);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting into CaseLogs: {ex.Message}");
            }
        }

        /// <summary>
        /// Inserts a new session into the CaseSessions table.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <param name="session">The session ID.</param>
        /// <param name="startDT">The start date and time of the session.</param>
        /// <param name="endDT">The end date and time of the session.</param>
        /// <param name="username">The username associated with the session.</param>
        public void InsertIntoCaseSessions(string connectionString, Guid session, DateTime startDT, DateTime endDT, string username)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Please enter the connection string:");
                connectionString = Console.ReadLine();
                gpa.updateSettingsFile("SQLConnectionString", connectionString);
            }
            EnsureTablesExist(connectionString);
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO dbo.CaseSessions (session, startDT, endDT, username) VALUES (@session, @startDT, @endDT, @username)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@session", session);
                        cmd.Parameters.AddWithValue("@startDT", startDT);
                        cmd.Parameters.AddWithValue("@endDT", endDT);
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting into CaseSessions: {ex.Message}");
            }
        }

        /// <summary>
        /// Interactively updates an existing command in the CaseCommands table.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        public void InteractiveUpdateCaseCommands(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Please enter the connection string:");
                connectionString = Console.ReadLine();
                gpa.updateSettingsFile("SQLConnectionString", connectionString);
            }
            EnsureTablesExist(connectionString);

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "SELECT Command, Keywords, codeToExecute, Software, LastUsed, Dynamic FROM dbo.CaseCommands";
                    var commands = new List<FF_CaseCommands>();

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var command = new FF_CaseCommands
                                {
                                    Command = reader["Command"].ToString(),
                                    Keywords = reader["Keywords"].ToString(),
                                    CodeToExecute = reader["codeToExecute"].ToString(),
                                    Software = reader["Software"].ToString(),
                                    LastUsed = reader.IsDBNull(reader.GetOrdinal("LastUsed")) ? new DateTime(1970, 1, 1) : reader.GetDateTime(reader.GetOrdinal("LastUsed")),
                                    Dynamic = reader.IsDBNull(reader.GetOrdinal("Dynamic")) ? false : reader.GetBoolean(reader.GetOrdinal("Dynamic"))
                                };
                                commands.Add(command);
                            }
                        }
                    }

                    Console.WriteLine("Available Commands:");
                    for (int i = 0; i < commands.Count; i++)
                    {
                        Console.WriteLine($"{i + 1}: {commands[i].Command}");
                    }

                    Console.WriteLine("Enter the number of the command you want to update or type 'cancel' to exit:");
                    string input = Console.ReadLine();

                    if (input.ToLower() == "cancel")
                    {
                        Console.WriteLine("Update canceled.");
                        return;
                    }

                    if (int.TryParse(input, out int commandNumber) && commandNumber > 0 && commandNumber <= commands.Count)
                    {
                        var recordToUpdate = commands[commandNumber - 1];

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

                        string updateQuery = "UPDATE dbo.CaseCommands SET Command = @Command, Keywords = @Keywords, codeToExecute = @codeToExecute, Software = @Software, LastUsed = @LastUsed, Dynamic = @Dynamic WHERE Command = @OriginalCommand";

                        using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@Command", recordToUpdate.Command);
                            cmd.Parameters.AddWithValue("@Keywords", recordToUpdate.Keywords);
                            cmd.Parameters.AddWithValue("@codeToExecute", recordToUpdate.CodeToExecute);
                            cmd.Parameters.AddWithValue("@Software", recordToUpdate.Software);
                            cmd.Parameters.AddWithValue("@LastUsed", (object)recordToUpdate.LastUsed ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Dynamic", (object)recordToUpdate.Dynamic ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@OriginalCommand", commands[commandNumber - 1].Command);

                            cmd.ExecuteNonQuery();
                        }

                        Console.WriteLine("Command updated successfully.");
                    }
                    else
                    {
                        Console.WriteLine("Invalid selection. Update canceled.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating command: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates an existing interaction record in the CaseInteractions table.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <param name="id">The unique identifier of the interaction.</param>
        /// <param name="username">The username associated with the interaction (optional).</param>
        /// <param name="message">The message of the interaction (optional).</param>
        /// <param name="screen">The screen associated with the interaction (optional).</param>
        /// <param name="dt">The date and time of the interaction (optional).</param>
        /// <param name="receiver">The receiver of the interaction (optional).</param>
        /// <param name="submitted">Indicates if the interaction is submitted (optional).</param>
        /// <param name="waitingOnResponse">Indicates if the interaction is waiting on a response (optional).</param>
        /// <param name="answered">Indicates if the interaction is answered (optional).</param>
        /// <param name="actionPerformed">Indicates if an action was performed (optional).</param>
        /// <param name="timeToProcess">The time taken to process the interaction (optional).</param>
        /// <param name="commandsActivated">The commands activated during the interaction (optional).</param>
        public void UpdateCaseInteractions(string connectionString, Guid id, string username = null, string message = null, string screen = null, DateTime? dt = null, string receiver = null, bool? submitted = null, bool? waitingOnResponse = null, bool? answered = null, bool? actionPerformed = null, string timeToProcess = null, string commandsActivated = null)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Please enter the connection string:");
                connectionString = Console.ReadLine();
                gpa.updateSettingsFile("SQLConnectionString", connectionString);
            }
            EnsureTablesExist(connectionString);
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    List<string> updates = new List<string>();
                    if (username != null) updates.Add("Username = @Username");
                    if (message != null) updates.Add("Message = @Message");
                    if (screen != null) updates.Add("Screen = @Screen");
                    if (dt.HasValue) updates.Add("DT = @DT");
                    if (receiver != null) updates.Add("Receiver = @Receiver");
                    if (submitted.HasValue) updates.Add("Submitted = @Submitted");
                    if (waitingOnResponse.HasValue) updates.Add("WaitingOnResponse = @WaitingOnResponse");
                    if (answered.HasValue) updates.Add("Answered = @Answered");
                    if (actionPerformed.HasValue) updates.Add("ActionPerformed = @ActionPerformed");
                    if (timeToProcess != null) updates.Add("TimeToProcess = @TimeToProcess");
                    if (commandsActivated != null) updates.Add("CommandsActivated = @CommandsActivated");

                    string updateQuery = string.Join(", ", updates);
                    string query = $"UPDATE dbo.CaseInteractions SET {updateQuery} WHERE id = @id";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        if (username != null) cmd.Parameters.AddWithValue("@Username", username);
                        if (message != null) cmd.Parameters.AddWithValue("@Message", message);
                        if (screen != null) cmd.Parameters.AddWithValue("@Screen", screen);
                        if (dt.HasValue) cmd.Parameters.AddWithValue("@DT", dt.Value);
                        if (receiver != null) cmd.Parameters.AddWithValue("@Receiver", receiver);
                        if (submitted.HasValue) cmd.Parameters.AddWithValue("@Submitted", submitted.Value);
                        if (waitingOnResponse.HasValue) cmd.Parameters.AddWithValue("@WaitingOnResponse", waitingOnResponse.Value);
                        if (answered.HasValue) cmd.Parameters.AddWithValue("@Answered", answered.Value);
                        if (actionPerformed.HasValue) cmd.Parameters.AddWithValue("@ActionPerformed", actionPerformed.Value);
                        if (timeToProcess != null) cmd.Parameters.AddWithValue("@TimeToProcess", timeToProcess);
                        if (commandsActivated != null) cmd.Parameters.AddWithValue("@CommandsActivated", commandsActivated);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating CaseInteractions: {ex.Message}");
            }
        }

        /// <summary>
        /// Detects keywords in the input string and returns a dictionary of matching commands with their match percentages.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <param name="input">The input string to detect keywords in.</param>
        /// <returns>A dictionary of commands and their match percentages.</returns>
        public Dictionary<string, float> DetectKeywordsInString(string connectionString, string input)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Please enter the connection string:");
                connectionString = Console.ReadLine();
                gpa.updateSettingsFile("SQLConnectionString", connectionString);
            }
            EnsureTablesExist(connectionString);

            var keywordMatches = new Dictionary<string, float>();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT Command, Keywords FROM dbo.CaseCommands";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string command = reader["Command"].ToString();
                                string keywords = reader["Keywords"].ToString();
                                string[] keywordArray = keywords.Split(';');

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
                                    keywordMatches[command] = matchPercentage;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting keywords: {ex.Message}");
            }

            return keywordMatches
                .OrderByDescending(kv => kv.Value)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        /// <summary>
        /// Updates the end date and time for a specific session in the CaseSessions table.
        /// </summary>
        /// <param name="connectionString">The connection string for the SQL server.</param>
        /// <param name="session">The session ID to update.</param>
        /// <param name="endDT">The new end date and time for the session.</param>
        public void UpdateCaseSessions(string connectionString, Guid session, DateTime endDT)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("Please enter the connection string:");
                connectionString = Console.ReadLine();
                gpa.updateSettingsFile("SQLConnectionString", connectionString);
            }
            EnsureTablesExist(connectionString);
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "UPDATE dbo.CaseSessions SET endDT = @endDT WHERE session = @session";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@session", session);
                        cmd.Parameters.AddWithValue("@endDT", endDT);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating CaseSessions: {ex.Message}");
            }
        }

        public List<CommandQueueRecord> GetCommandQueue(string connectionString)
        {
            string sql = "SELECT * FROM CommandQueue ORDER BY Timestamp";
            List<CommandQueueRecord> records = new List<CommandQueueRecord>();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlCommand cmd = new SqlCommand(sql, connection);
                    connection.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var record = new CommandQueueRecord
                            {
                                CommandName = reader["CommandName"].ToString(),
                                Command = reader["Command"].ToString(),
                                User = reader["User"].ToString(),
                                Timestamp = (DateTime)reader["Timestamp"]
                            };
                            records.Add(record);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving command queue: {ex.Message}");
            }
            return records;
        }

        public void EnqueueCommand(string commandName, string command, string user)
        {
            string sql = "INSERT INTO CommandQueue (CommandName, Command, User, Timestamp) VALUES (@CommandName, @Command, @User, @Timestamp)";
            try
            {
                GlobalParameters gpa = new GlobalParameters();
                string connString = gpa.getSetting("SQLConnectionString");
                using (SqlConnection connection = new SqlConnection(connString))
                {
                    SqlCommand cmd = new SqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@CommandName", commandName);
                    cmd.Parameters.AddWithValue("@Command", command);
                    cmd.Parameters.AddWithValue("@User", user);
                    cmd.Parameters.AddWithValue("@Timestamp", DateTime.Now);
                    connection.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enqueuing command: {ex.Message}");
            }
        }

        public string DequeueCommand()
        {
            string sql = "SELECT TOP 1 * FROM CommandQueue ORDER BY Timestamp";
            try
            {
                GlobalParameters gpa = new GlobalParameters();
                string connString = gpa.getSetting("SQLConnectionString");
                using (SqlConnection connection = new SqlConnection(connString))
                {
                    SqlCommand cmd = new SqlCommand(sql, connection);
                    connection.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string command = reader["Command"].ToString();
                            int id = (int)reader["ID"];
                            reader.Close();

                            string deleteSql = "DELETE FROM CommandQueue WHERE ID = @ID";
                            SqlCommand deleteCmd = new SqlCommand(deleteSql, connection);
                            deleteCmd.Parameters.AddWithValue("@ID", id);
                            deleteCmd.ExecuteNonQuery();

                            return command;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error dequeuing command: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Removes a command from the CommandQueue table by command name.
        /// </summary>
        /// <param name="commandName">The name of the command to remove.</param>
        public void RemoveCommand(string commandName)
        {
            string sql = "DELETE FROM CommandQueue WHERE CommandName = @CommandName";
            string connString = gpa.getSetting("SQLConnectionString");
            try
            {
                using (SqlConnection connection = new SqlConnection(connString))
                {
                    SqlCommand cmd = new SqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@CommandName", commandName);
                    connection.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing command: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes a monitor from the MonitorQueue table by monitor name.
        /// </summary>
        /// <param name="monitorName">The name of the monitor to remove.</param>
        public void RemoveMonitor(string monitorName)
        {
            string sql = "DELETE FROM MonitorQueue WHERE MonitorName = @MonitorName";
            string connString = gpa.getSetting("SQLConnectionString");
            try
            {
                using (SqlConnection connection = new SqlConnection(connString))
                {
                    SqlCommand cmd = new SqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@MonitorName", monitorName);
                    connection.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing monitor: {ex.Message}");
            }
        }

        /// <summary>
        /// Enqueues a new monitor in the MonitorQueue table.
        /// </summary>
        /// <param name="monitorName">The name of the monitor.</param>
        /// <param name="monitorType">The type of the monitor.</param>
        /// <param name="monitorData1">The first data value associated with the monitor.</param>
        /// <param name="monitorData2">The second data value associated with the monitor (optional).</param>
        /// <param name="command">The command to execute for the monitor.</param>
        /// <param name="user">The user associated with the monitor.</param>
        public void EnqueueMonitor(string monitorName, string monitorType, string monitorData1, string monitorData2, string command, string user)
        {
            string sql = "INSERT INTO MonitorQueue (MonitorName, MonitorType, MonitorData1, MonitorData2, Command, User, Activated, Timestamp) VALUES (@MonitorName, @MonitorType, @MonitorData1, @MonitorData2, @Command, @User, @Activated, @Timestamp)";
            string connectionString = gpa.getSetting("SQLConnectionString");
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlCommand cmd = new SqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@MonitorName", monitorName);
                    cmd.Parameters.AddWithValue("@MonitorType", monitorType);
                    cmd.Parameters.AddWithValue("@MonitorData1", monitorData1);
                    cmd.Parameters.AddWithValue("@MonitorData2", monitorData2);
                    cmd.Parameters.AddWithValue("@Command", command);
                    cmd.Parameters.AddWithValue("@User", user);
                    cmd.Parameters.AddWithValue("@Activated", false);
                    cmd.Parameters.AddWithValue("@Timestamp", DateTime.Now);
                    connection.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enqueuing monitor: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the monitor queue from the MonitorQueue table.
        /// </summary>
        /// <returns>A list of monitor queue records.</returns>
        public void FlushMonitorQueue()
        {
            List<dynamic> records = new List<dynamic>();
            string connectionString = gpa.getSetting("SQLConnectionString");
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "Delete From MonitorQueue";
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error flushing MonitorQueue: {ex.Message}");
            }
        }
        public void FlushCommandQueue()
        {
            List<dynamic> records = new List<dynamic>();
            string connectionString = gpa.getSetting("SQLConnectionString");
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "Delete From CommandQueue";
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error flushing MonitorQueue: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the monitor queue from the MonitorQueue table.
        /// </summary>
        /// <returns>A list of monitor queue records.</returns>
        public List<dynamic> GetMonitorQueue()
        {
            string sql = "SELECT * FROM MonitorQueue";
            List<dynamic> records = new List<dynamic>();
            string connectionString = gpa.getSetting("SQLConnectionString");
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlCommand cmd = new SqlCommand(sql, connection);
                    connection.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var record = new
                            {
                                MonitorName = reader["MonitorName"].ToString(),
                                MonitorType = reader["MonitorType"].ToString(),
                                MonitorData1 = reader["MonitorData1"].ToString(),
                                MonitorData2 = reader["MonitorData2"].ToString(),
                                Command = reader["Command"].ToString(),
                                User = reader["User"].ToString(),
                                Activated = (bool)reader["Activated"],
                                Timestamp = (DateTime)reader["Timestamp"]
                            };
                            records.Add(record);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving monitor queue: {ex.Message}");
            }
            return records;
        }

        /// <summary>
        /// Updates the activation status of a monitor in the MonitorQueue table.
        /// </summary>
        /// <param name="monitorName">The name of the monitor to update.</param>
        /// <param name="activated">The new activation status.</param>
        public void UpdateMonitorActivationStatus(string monitorName, bool activated)
        {
            string sql = "UPDATE MonitorQueue SET Activated = @Activated WHERE MonitorName = @MonitorName";
            string connectionString = gpa.getSetting("SQLConnectionString");
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlCommand cmd = new SqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@Activated", activated);
                    cmd.Parameters.AddWithValue("@MonitorName", monitorName);
                    connection.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating monitor activation status: {ex.Message}");
            }
        }
    }
}
