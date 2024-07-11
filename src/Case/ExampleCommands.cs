//----------------------------------------------------------------------------
//  Copyright (C) 2024 by SCADA Solve LLC. All rights reserved.              
//----------------------------------------------------------------------------
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;

namespace Case
{
    public class ExampleCommands
    {
        public void exampleCommands()
        {
            Program.executingSim = true;
            string[][] testArgs = new string[][]
            {

                // Command-related simulations
                //new string[] { "-command", "-create" },
                //new string[] { "-command", "-update" },
                //new string[] { "-command", "-view" },
                //new string[] { "-command", "-export" },
                //new string[] { "-command", "-import", "C:\\data80.case" },
                //new string[] { "-command", "-view" },
                //new string[] { "-command", "Keyword,examples", "-p", "-v", "-e" },

                // Execute command simulation
                //new string[] { "-execute", "e;schrome.exe{ENTER};t100" },

                // Talk command simulation
                //new string[] { "-talk" },

                // Monitor-related simulations
                //new string[] { "-monitor", "-image", "\"image.png\"", "\"execute image command\"" },
                //new string[] { "-monitor", "-file", "\"file.txt\"", "\"directory\"", "\"execute file command\"" },
                //new string[] { "-monitor", "-database", "\"table\"", "\"condition\"", "\"execute sql command\"" },
                //new string[] { "-monitor", "-c" },

                // Queue-related simulations
                //new string[] { "-queue", "-command", "-add", "findVS" },
                //new string[] { "-viewQueue", "-command" },
                //new string[] { "-queue", "-monitor", "-flush" },
                //new string[] { "-queue", "-command", "-flush" },
                //new string[] { "-queue", "-monitor", "image", "Screen 1", "testImage.png", "1000", "testCommand" },
                //new string[] { "-viewQueue", "-command" },
                //new string[] { "-queue", "-monitor", "file", "File Scanner", "testImage.png", @"C:\testenv", "testCommand" },
                //new string[] { "-execQueue", "-monitor" },
                //new string[] { "-queue", "-command", "-remove", "testCommand" },
                //new string[] { "-queue", "-monitor", "-remove", "testMonitor" },

                // Analyze-related simulations
                //new string[] { "-analyze", "-file", "\"filepath\"", "\"analyze this file\"" },
                //new string[] { "-analyze", "-database", "\"SELECT * FROM table\"", "\"analyze this\"" },

                // Settings-related simulations
                //new string[] { "-settings", "-unlock" },
                //new string[] { "-settings", "-password", "-c" },
                //new string[] { "-settings", "-password", "-k" },
                //new string[] { "-settings", "-password", "-h" },
                //new string[] { "-settings", "-update", "Setting1:true" },
                //new string[] { "-settings", "-show" },
                //new string[] { "-settings", "-gpu" },

                // Help command simulation
                //new string[] { "-help" },
            };

            foreach (var args in testArgs)
            {
                Console.WriteLine($"Simulating: {string.Join(" ", args)}");
                Program.Main(args);
                //if (args[0] == "-execQueue")
                Console.ReadLine();
            }
        }

    }
}
