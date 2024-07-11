//----------------------------------------------------------------------------
//  Copyright (C) 2024 by SCADA Solve LLC. All rights reserved.              
//----------------------------------------------------------------------------
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Threading;

namespace Case
{
    /// <summary>
    /// Manages the initialization and execution of LLM models for conversational and analytical purposes.
    /// </summary>
    public class runLLM
    {
        /// <summary>
        /// Initializes the LLM model based on the specified method and analysis parameters.
        /// </summary>
        /// <param name="method">The method to initialize ("Conversational" or "Analysis").</param>
        /// <param name="AnalysisType">The type of analysis ("none", "File", or "SQL").</param>
        /// <param name="AnalysisParameter">The parameter for the analysis.</param>
        /// <param name="analysisPrompt">The prompt for the analysis.</param>
        /// <param name="currentSession">The current session GUID.</param>
        public void initCase(string method, string AnalysisType = "none", string AnalysisParameter = "none", string analysisPrompt = "none", Guid currentSession = new Guid())
        {
            try
            {
                // Set up the start info for the Python script including the script path
                string llmConfiguration = "";
                if (method == "Conversational")
                {
                    llmConfiguration = updateModelPython("ConversationalTrainingModel");
                }
                else if (method == "Analysis")
                {
                    if (AnalysisType == "File")
                    {
                        AnalysisFileEngine(AnalysisParameter, analysisPrompt, currentSession);
                    }
                    else if (AnalysisType == "SQL")
                    {
                        GlobalParameters gPA = new GlobalParameters();
                        string connStr = gPA.getSetting("SQLConnectionString");
                        AnalyzeSQL(connStr, AnalysisParameter, analysisPrompt, currentSession);
                    }
                    return;
                }

                ProcessStartInfo psi = new ProcessStartInfo("python.exe")
                {
                    Arguments = llmConfiguration,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process pythonProcess = new Process { StartInfo = psi };
                pythonProcess.Start();

                Console.WriteLine("Loading model into memory...");
                string processingData = ReadInitialOutputs(pythonProcess);

                DistributionManager DM = new DistributionManager();
                DM.distributeInitLogs(processingData, method);

                Console.WriteLine("Model Loaded, entering into discussion with Case.");
                Console.WriteLine("Type 'exit' to quit at any time.");
                if (method == "Conversational")
                {
                    while (true)
                    {
                        Console.Write("UserPrompt> ");
                        string userInput = Console.ReadLine();
                        if (userInput.ToLower() == "exit")
                        {
                            return;
                        }
                        if (string.IsNullOrEmpty(userInput))
                            continue;

                        // Send input to the Python script
                        pythonProcess.StandardInput.WriteLine(userInput);
                        pythonProcess.StandardInput.Flush();

                        // Read response from the Python process
                        List<string> response = ReadFileResponse(pythonProcess);
                        foreach (string s in response)
                        {
                            foreach (char c in s)
                            {
                                Console.Write(c);
                                Thread.Sleep(10);
                            }
                        }
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing case: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles file-based analysis by processing multiple files.
        /// </summary>
        /// <param name="AnalysisFileLocation">The location of the analysis files.</param>
        /// <param name="analysisPrompt">The prompt for the analysis.</param>
        /// <param name="currentSession">The current session GUID.</param>
        private void AnalysisFileEngine(string AnalysisFileLocation, string analysisPrompt, Guid currentSession)
        {
            try
            {
                List<string> analysisFiles = updateModelPythonForAnalysis(AnalysisFileLocation, analysisPrompt);
                var templateFile = analysisFiles[analysisFiles.Count - 1];
                int totalAmountOfFiles = (analysisFiles.Count - 2) + 1;
                int position = 1;
                Console.WriteLine("Performing Analysis with Prompt: " + analysisPrompt);
                foreach (var filePath in analysisFiles)
                {
                    if (position <= totalAmountOfFiles)
                    {
                        Console.WriteLine("Analyzing " + position + " of " + totalAmountOfFiles + " file(s)");
                        if (filePath != templateFile)
                            ProcessAnalysisFile(filePath, templateFile, false, currentSession);
                        File.Delete(filePath);
                    }
                    position++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in file-based analysis: {ex.Message}");
            }
        }

        /// <summary>
        /// Analyzes SQL data by executing a query and processing the results.
        /// </summary>
        /// <param name="connectionString">The SQL connection string.</param>
        /// <param name="query">The SQL query to execute.</param>
        /// <param name="analysisPrompt">The prompt for the analysis.</param>
        /// <param name="currentSession">The current session GUID.</param>
        private void AnalyzeSQL(string connectionString, string query, string analysisPrompt, Guid currentSession)
        {
            GlobalParameters gPA = new GlobalParameters();
            string baseDirectory = gPA.getSetting("BaseDirectoryPath");
            string opPath = "SQLData.txt";
            string AnalysisFileLocation = Path.Combine(baseDirectory, opPath);
            string columns = "";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            using (StreamWriter writer = new StreamWriter(opPath))
                            {
                                // Write column names with label
                                columns = "Columns,";
                                writer.Write("Columns,");
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    writer.Write(reader.GetName(i) + ",");
                                    columns += reader.GetName(i) + ",";
                                }
                                writer.WriteLine();

                                // Write rows with labels
                                int rowCount = 1;
                                while (reader.Read())
                                {
                                    writer.Write("Row " + rowCount + ",");
                                    for (int i = 0; i < reader.FieldCount; i++)
                                    {
                                        writer.Write(reader[i].ToString() + ",");
                                    }
                                    writer.WriteLine();
                                    rowCount++;
                                }
                            }
                        }
                    }
                }

                // Get list of analysis output files
                List<string> analysisFiles = updateModelPythonForAnalysis(AnalysisFileLocation, analysisPrompt, columns);
                // Process each analysis file
                var templateFile = analysisFiles[analysisFiles.Count - 1];
                int totalAmountOfFiles = (analysisFiles.Count - 2) + 1;
                int position = 1;
                Console.WriteLine("Performing SQL Analysis with Prompt: " + analysisPrompt);
                foreach (var filePath in analysisFiles)
                {
                    if (position <= totalAmountOfFiles)
                    {
                        Console.WriteLine("Analyzing " + position + " of " + totalAmountOfFiles + " file(s)");
                        if (filePath != templateFile)
                            ProcessAnalysisFile(filePath, templateFile, true, currentSession);
                        File.Delete(filePath);
                    }
                    position++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SQL analysis: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes an analysis file using the specified configuration.
        /// </summary>
        /// <param name="filePath">The path to the analysis file.</param>
        /// <param name="llmConfiguration">The LLM configuration file.</param>
        /// <param name="isSQL">Indicates if the analysis is SQL-based.</param>
        /// <param name="currentSession">The current session GUID.</param>
        private void ProcessAnalysisFile(string filePath, string llmConfiguration, bool isSQL = false, Guid currentSession = new Guid())
        {
            try
            {
                string pythonScript = File.ReadAllText(llmConfiguration);
                pythonScript = pythonScript.Replace("{trainingPath}", filePath.Replace("\\", "\\\\"));
                string temporaryFile = llmConfiguration.Replace(".py", "_Generated.py");
                File.WriteAllText(temporaryFile, pythonScript);

                ProcessStartInfo psi = new ProcessStartInfo("python.exe")
                {
                    Arguments = temporaryFile,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                // Start the Python process
                Process pythonProcess = new Process { StartInfo = psi };
                pythonProcess.Start();

                // Wait for the model to load by reading initial outputs
                List<string> processingData = isSQL ? ReadSQLResponse(pythonProcess) : ReadFileResponse(pythonProcess);
                Console.WriteLine("Analysis results");
                foreach (string r in processingData)
                {
                    Console.WriteLine(r);
                }

                DistributionManager DM = new DistributionManager();
                Console.WriteLine("======================================");
                DM.distributeInitLogs(String.Join(",", processingData), "Analyze");
                Console.WriteLine("Would you like to discuss the analysis? (Y/N)");
                string answer = Console.ReadLine();
                if (answer.ToLower() == "y")
                {
                    // Infinite loop to interact with the Python process
                    while (true)
                    {
                        Console.Write("UserPrompt> ");
                        string userInput = Console.ReadLine();

                        if (string.IsNullOrEmpty(userInput))
                            break;
                        if (userInput == "exit" || userInput == "quit")
                            break;

                        // Send input to the Python script
                        pythonProcess.StandardInput.WriteLine(userInput);
                        pythonProcess.StandardInput.Flush();
                        DM.distributeCaseLogs(currentSession, userInput);
                        DM.distributeInitLogs(userInput, "AnalyzeConv_User");

                        // Read response from the Python process
                        List<string> response = isSQL ? ReadSQLResponse(pythonProcess) : ReadFileResponse(pythonProcess);

                        foreach (string s in response)
                            Console.WriteLine(s);

                        DM.distributeCaseLogs(currentSession, "Case Response: " + string.Join(" ", response));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing analysis file: {ex.Message}");
            }
        }

        /// <summary>
        /// Splits a large file into smaller chunks and returns the paths of the generated files.
        /// </summary>
        /// <param name="templateFilePath">The path to the template file.</param>
        /// <param name="largeFilePath">The path to the large file.</param>
        /// <param name="maxTokens">The maximum number of tokens per chunk.</param>
        /// <param name="userPrompt">The user prompt for the analysis.</param>
        /// <param name="dbColumns">The database columns (if applicable).</param>
        /// <returns>A list of paths to the generated files.</returns>
        private List<string> ProcessFilesAndReturnGeneratedFiles(string templateFilePath, string largeFilePath, int maxTokens, string userPrompt, string dbColumns = "")
        {
            List<string> generatedFiles = new List<string>();
            try
            {
                string templateText = File.ReadAllText(templateFilePath);
                templateText = templateText.Replace("{Prompt}", userPrompt);
                string largeText = File.ReadAllText(largeFilePath);
                int chunkSize = maxTokens - templateText.Length - dbColumns.Length;
                int chunkIndex = 1;
                int largeTextLength = largeText.Length;

                for (int i = 0; i < largeTextLength; i += chunkSize)
                {
                    string chunkText = largeText.Substring(i, Math.Min(chunkSize, largeTextLength - i));
                    string combinedText = templateText + "\n" + dbColumns + chunkText;
                    string chunkFilePath = Path.Combine(Path.GetDirectoryName(templateFilePath), $"chunk_{chunkIndex}.txt");
                    File.WriteAllText(chunkFilePath, combinedText);
                    generatedFiles.Add(chunkFilePath);
                    chunkIndex++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing files: {ex.Message}");
            }

            return generatedFiles;
        }

        /// <summary>
        /// Updates the Python model configuration for analysis.
        /// </summary>
        /// <param name="analysisFileLocation">The location of the analysis files.</param>
        /// <param name="analysisPrompt">The prompt for the analysis.</param>
        /// <param name="columns">The database columns (if applicable).</param>
        /// <returns>A list of paths to the generated files.</returns>
        private List<string> updateModelPythonForAnalysis(string analysisFileLocation, string analysisPrompt, string columns = "")
        {
            List<string> generatedFiles = new List<string>();
            try
            {
                GlobalParameters gPA = new GlobalParameters();
                string model = gPA.getSetting("Model");
                string threads = gPA.getSetting("ThreadCount");
                string GPU = gPA.getSetting("GPU");
                string baseDirectory = gPA.getSetting("BaseDirectoryPath");
                string trainingPath = gPA.getSetting("AnalysisTrainingModelData");
                string pythonTemplatePath = gPA.getSetting("AnalysisTrainingModel");
                string pythonScript = File.ReadAllText(pythonTemplatePath);
                pythonScript = pythonScript.Replace("{model}", model);
                pythonScript = pythonScript.Replace("{threads}", threads);
                if (string.IsNullOrEmpty(GPU))
                {
                    Console.WriteLine("WARNING: No GPU configured will consume more CPU resources and slow down the machine.");
                    pythonScript = pythonScript.Replace(", device='{GPU}'", "");
                }
                else
                {
                    pythonScript = pythonScript.Replace("{GPU}", GPU);
                }
                string outputPythonPath = Path.Combine(baseDirectory, "ConfiguredAnalysisTrainingModel.py");
                File.WriteAllText(outputPythonPath, pythonScript);
                generatedFiles = ProcessFilesAndReturnGeneratedFiles(trainingPath, analysisFileLocation, 2000, analysisPrompt, columns + "\n");
                generatedFiles.Add(outputPythonPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating Python model for analysis: {ex.Message}");
            }

            return generatedFiles;
        }

        /// <summary>
        /// Updates the Python model configuration.
        /// </summary>
        /// <param name="modelToUpdate">The model to update.</param>
        /// <returns>The path to the updated Python script.</returns>
        private static string updateModelPython(string modelToUpdate)
        {
            try
            {
                GlobalParameters gPA = new GlobalParameters();
                string model = gPA.getSetting("Model");
                string threads = gPA.getSetting("ThreadCount");
                string GPU = gPA.getSetting("GPU");
                string baseDirectory = gPA.getSetting("BaseDirectoryPath");
                string trainingPath = gPA.getSetting(modelToUpdate + "Data");

                // Read and update Python script template
                string pythonTemplatePath = gPA.getSetting(modelToUpdate);
                string pythonScript = File.ReadAllText(pythonTemplatePath);

                // Replace placeholders with actual values
                pythonScript = pythonScript.Replace("{model}", model);
                pythonScript = pythonScript.Replace("{threads}", threads);
                if (string.IsNullOrEmpty(GPU))
                {
                    Console.WriteLine("WARNING: No GPU configured will consume more CPU resources and slow down the machine.");
                    pythonScript = pythonScript.Replace(", device='{GPU}'", "");
                }
                else
                {
                    pythonScript = pythonScript.Replace("{GPU}", GPU);
                }
                pythonScript = pythonScript.Replace("{trainingPath}", trainingPath.Replace("\\", "\\\\"));

                // Define the output path for the updated script
                string outputPythonPath = Path.Combine(baseDirectory, "Configured" + modelToUpdate + ".py");
                File.WriteAllText(outputPythonPath, pythonScript);
                return outputPythonPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating Python model: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Reads the initial output from the Python process.
        /// </summary>
        /// <param name="process">The Python process.</param>
        /// <returns>The initial output as a string.</returns>
        private static string ReadInitialOutputs(Process process)
        {
            try
            {
                List<string> data = new List<string>();
                string line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    data.Add(line);
                    break;
                }
                return string.Join(" ", data.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading initial outputs: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Reads the response from the Python process for file-based analysis.
        /// </summary>
        /// <param name="process">The Python process.</param>
        /// <returns>A list of strings containing the response.</returns>
        private static List<string> ReadFileResponse(Process process)
        {
            List<string> allLines = new List<string>();
            List<string> rawData = new List<string>();
            var response = new StringWriter();
            string line;
            int x = 0;
            try
            {
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    rawData.Add(line);
                    if (x == 1)
                    {
                        if (line.Contains('\f'))
                            allLines.Add(line.Split('\f')[1]);
                        else
                            allLines.Add(line);
                    }
                    else if (x > 1)
                    {
                        if (line.StartsWith("--Done--"))
                            break;
                        allLines.Add(line);
                    }
                    x++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file response: {ex.Message}");
            }
            return allLines;
        }

        /// <summary>
        /// Reads the response from the Python process for SQL-based analysis.
        /// </summary>
        /// <param name="process">The Python process.</param>
        /// <returns>A list of strings containing the response.</returns>
        private static List<string> ReadSQLResponse(Process process)
        {
            List<string> allLines = new List<string>();
            var response = new StringWriter();
            string line;
            int x = 0;
            try
            {
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    if (x > 0)
                    {
                        if (line.StartsWith("--Done--"))
                            break;
                        allLines.Add(line);
                    }
                    x++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading SQL response: {ex.Message}");
            }
            return allLines;
        }
    }
}
