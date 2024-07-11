//----------------------------------------------------------------------------
//  Copyright (C) 2024 by SCADA Solve LLC. All rights reserved.              
//----------------------------------------------------------------------------
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.Dynamic;

namespace Case
{
    public class CaseFileManager
    {
        private GlobalParameters gp;
        private DistributionManager dm;

        /// <summary>
        /// Initializes a new instance of the CaseFileManager class.
        /// </summary>
        /// <param name="globalParameters">The global parameters.</param>
        /// <param name="distributionManager">The distribution manager.</param>
        public CaseFileManager(GlobalParameters globalParameters, DistributionManager distributionManager)
        {
            gp = globalParameters;
            dm = distributionManager;
        }

        /// <summary>
        /// Creates a .case file containing selected commands and associated images.
        /// </summary>
        public void CreateCaseFile()
        {
            var caseFileContent = new CaseFileContent
            {
                Commands = new List<CaseCommand>(),
                Images = new List<CaseImage>()
            };

            try
            {
                // List available commands
                DistributionManager dm = new DistributionManager();
                var availableCommands = dm.GetAvailableCommands(); // Assume this method exists and returns a List<CaseCommand>
                if (availableCommands.Count == 0)
                {
                    Console.WriteLine("No Commands Detected.");
                    Console.WriteLine("Use -create to create a new command.");
                    Console.WriteLine("Use -import to import a series of commands.");
                    return;
                }
                else
                {
                    Console.WriteLine("Available commands:");
                    for (int i = 0; i < availableCommands.Count; i++)
                    {
                        Console.WriteLine($"{i + 1}. {availableCommands[i].Command}");
                    }

                    // Ask user to select commands to export
                    Console.WriteLine("Enter the numbers of the commands to export, separated by commas:");
                    string input = Console.ReadLine();
                    var selectedIndices = input.Split(',').Select(int.Parse).ToArray();

                    foreach (var index in selectedIndices)
                    {
                        if (index > 0 && index <= availableCommands.Count)
                        {
                            var selectedCommand = availableCommands[index - 1];
                            dynamic properties = dm.collectCommandProperties(selectedCommand.Command);

                            string[] subCommands = selectedCommand.CodeToExecute.Split(';');
                            var commandToAdd = new CaseCommand
                            {
                                Command = properties.Command,
                                CommandBytes = Encoding.UTF8.GetBytes(properties.CodeToExecute),
                                CodeToExecute = properties.CodeToExecute,
                                LastUsed = properties.LastUsed,
                                Dynamic = properties.Dynamic,
                                Keywords = properties.Keywords
                            };
                            caseFileContent.Commands.Add(commandToAdd);

                            foreach (var subCommand in subCommands)
                            {
                                string trimmedCommand = subCommand.Trim();

                                // Example image handling, modify as per actual RPA logic
                                if (trimmedCommand.Length > 0 && "fghjk".Contains(trimmedCommand.Substring(0, 1)))
                                {
                                    var imagePath = ExtractImagePathFromCommand(trimmedCommand);
                                    if (File.Exists(imagePath))
                                    {
                                        var caseImage = new CaseImage
                                        {
                                            ImagePath = imagePath,
                                            ImageBytes = File.ReadAllBytes(imagePath)
                                        };
                                        caseFileContent.Images.Add(caseImage);
                                    }
                                    else
                                    {
                                        throw new FileNotFoundException($"Image file '{imagePath}' not found for command '{trimmedCommand}'.");
                                    }
                                }
                            }
                        }
                    }

                    // Serialize and save the case file
                    Console.WriteLine("Enter the name for the .case file:");
                    string caseFileName = Console.ReadLine();
                    try
                    {
                        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                        var json = JsonSerializer.Serialize(caseFileContent, jsonOptions);
                        File.WriteAllText($"{caseFileName}.case", json);
                        Console.WriteLine($"Case file '{caseFileName}.case' created successfully.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating case file: {ex.Message}");
                    }
                }
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine($"Error: {e.Message}");
                // Clean up any partially added commands or images if necessary
                caseFileContent.Commands.Clear();
                caseFileContent.Images.Clear();
            }
            catch (FormatException e)
            {
                Console.WriteLine("Error: Invalid input format. Please enter numbers separated by commas.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unexpected error: {e.Message}");
            }
        }

        /// <summary>
        /// Loads a .case file, importing commands and images into the system.
        /// </summary>
        /// <param name="caseFilePath">The path to the .case file.</param>
        public void LoadCaseFile(string caseFilePath)
        {
            if (!File.Exists(caseFilePath))
            {
                Console.WriteLine("The specified .case file does not exist.");
                return;
            }

            CaseFileContent caseFileContent;
            try
            {
                var json = File.ReadAllText(caseFilePath);
                caseFileContent = JsonSerializer.Deserialize<CaseFileContent>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading case file: {ex.Message}");
                return;
            }

            foreach (var command in caseFileContent.Commands)
            {
                try
                {
                    SaveCommand(command);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving command '{command.Command}': {ex.Message}");
                }
            }

            foreach (var image in caseFileContent.Images)
            {
                try
                {
                    GlobalParameters gp = new GlobalParameters();
                    string imageDir = gp.getSetting("ImageStorageDirectory"); // Use appropriate directory
                    if (!Directory.Exists(imageDir))
                    {
                        Directory.CreateDirectory(imageDir);
                    }
                    string imagePath = Path.Combine(imageDir, Path.GetFileName(image.ImagePath));
                    File.WriteAllBytes(imagePath, image.ImageBytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving image {image.ImagePath}: {ex.Message}");
                }
            }
            Console.WriteLine("Case file loaded successfully.");
        }

        /// <summary>
        /// Saves a command to the appropriate storage method.
        /// </summary>
        /// <param name="command">The command to save.</param>
        private static void SaveCommand(CaseCommand command)
        {
            GlobalParameters gp = new GlobalParameters();
            DistributionManager dm = new DistributionManager();
            string storageMethod = gp.getSetting("StorageMethod");
            try
            {
                if (storageMethod == "Database")
                {
                    dm.distributeCaseCommands(command.Command, command.CodeToExecute, command.Keywords, command.Software, command.LastUsed, command.Dynamic);
                }
                else if (storageMethod == "FlatFile")
                {
                    dm.distributeCaseCommands(command.Command, command.CodeToExecute, command.Keywords, command.Software, command.LastUsed, command.Dynamic);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving command: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts the image path from a given command.
        /// </summary>
        /// <param name="command">The command containing the image path.</param>
        /// <returns>The extracted image path.</returns>
        private static string ExtractImagePathFromCommand(string command)
        {
            GlobalParameters gp = new GlobalParameters();
            string imageDir = gp.getSetting("ImageStorageDirectory");
            string imageName = command.Substring(1, command.Length - 1);
            return Path.Combine(imageDir, imageName).Replace("\\\\", "\\");
        }
    }

    [Serializable]
    public class CaseCommand
    {
        public string Command { get; set; }
        public string CodeToExecute { get; set; }
        public string Keywords { get; set; }
        public string Software { get; set; }
        public DateTime LastUsed { get; set; }
        public bool Dynamic { get; set; }
        public byte[] CommandBytes { get; set; }
    }

    public class CaseFileContent
    {
        public List<CaseCommand> Commands { get; set; }
        public List<CaseImage> Images { get; set; }
    }

    public class CaseImage
    {
        public string ImagePath { get; set; }
        public byte[] ImageBytes { get; set; }
    }
}
