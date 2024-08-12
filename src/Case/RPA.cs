//----------------------------------------------------------------------------
//  Copyright (C) 2024 by SCADA Solve LLC. All rights reserved.              
//----------------------------------------------------------------------------
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace Case
{
    /// <summary>
    /// The RPA class contains methods to perform various RPA actions such as mouse clicks, keyboard inputs, and image recognition.
    /// </summary>
    public class RPA
    {
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, UIntPtr dwExtraInfo);
        private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        private const uint MOUSEEVENTF_LEFTUP = 0x04;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const uint MOUSEEVENTF_RIGHTUP = 0x10;
        private static int spacing = 500;
        [DllImport("user32.dll")]
        protected static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]  
        public static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);
        public static string monitorResult = "";
        // Define constants
        private const int WM_DRAWCLIPBOARD = 0x0308;
        private const int WM_CHANGECBCHAIN = 0x030D;
        private static Dictionary<char, Func<string, string>> commandDescriptions = new Dictionary<char, Func<string, string>>
        {
            { 'e', _ => "(Hit windows button)" },
            { 's', arg => $"type \"{arg}\"" },
            { 'f', arg => $"find and click on the {arg.Replace(".png", "")}" },
            { 'j', arg => $"find and right click on the {arg.Replace(".png", "")}" },
            { 'k', arg => $"find and double right click on the {arg.Replace(".png", "")}" },
            { 'x', _ => "maximize the window" },
            { 'y', _ => "minimize the window" },
            { 'c', _ => "take a screenshot" },
            { 'l', arg => $"click at coordinates ({arg.Split(':')[0]}, {arg.Split(':')[1]})" },
            { 'r', arg => $"right-click at coordinates ({arg.Split(':')[0]}, {arg.Split(':')[1]})" },
            { 't', arg => $"wait for {arg} milliseconds" },
            { 'u', arg => $"update settings with value {arg}" },
            { 'd', arg => {
                var coords = arg.Split(',');
                return $"drag from ({coords[0]}) to ({coords[1]})";
            }},
            { 'g', arg => $"double-click on the {arg.Replace(".png", "")}" },
            { 'h', arg => $"wait for image {arg.Replace(".png", "")} to appear" },
            { 'v', arg => $"wait for image {arg.Replace(".png", "")} to appear, timeout if it doesn't show up." },
            { 'q', arg => $"find and click the last instance of {arg.Replace(".png", "")}" }
        };
        public static string findabledirectory = "";

        // Commands
        /*
         * s = send keys to keyboard
         * at = alt + tab
         * l = left click on coordinates
         * r = right click on coordinates
         * d = click and drag from start coordinates to end coordinates
         * x = maximize active window
         * y = minimize active window
         * e = open windows start menu
         * t = sleep before executing next command
         * c = capture screen shot
         * f = left click on image in active screenshot given image provided
         * g = double click on image in active screenshot given image provided
         * n = navigational tab, either single "n" or "n5" given you hit it 5 times
         * us = update thread sleep between command executions to give computer time to respond
         * z = activate another established command set
        */
        private static readonly string[] NoArgumentMethods = { "e", "x", "y", "c", "p", "o", "i" };
        private static readonly string[] SingleArgumentMethods = { "s", "l", "r", "t", "u", "z", "a" };
        private static readonly string[] DoubleArgumentMethods = { "d" };
        private static readonly string[] ImageDetectionMethods = { "f", "g", "h", "j", "k", "v" };
        private static readonly string[] DuplicateImageDetectionMethods = { "q" };
        private static readonly string[] ClickParameters = { "tl", "tm", "tr", "ml", "mr", "bl", "bm", "br" };
        private static readonly Regex CustomOffsetRegex = new Regex(@"<\d+:\d+>");

        private static readonly Dictionary<string, string> CommandTranslations = new Dictionary<string, string>
        {
            { "s", "Type: " },
            { "a", "Alt+Tab: " },
            { "l", "Left Click: " },
            { "r", "Right Click: " },
            { "d", "Click And Drag: " },
            { "t", "Sleep: " },
            { "f", "Find And Click: " },
            { "g", "Double Click On Image: " },
            { "j", "Find And Right Click: " },
            { "k", "Double Right Click On Image: " },
            { "h", "Wait For Image: " },
            { "v", "Wait For Image With Timeout: " },
            { "n", "Tab: " },
            { "u", "Update Command Timer: " },
            { "z", "Run Command: " },
            { "x", "Maximize" },
            { "y", "Minimize" },
            { "e", "Start Menu" },
            { "p", "Copy To Clipboard"},
            { "o", "Paste From Clipboard"},
            { "i", "Select All" }
        };
        private static readonly Dictionary<string, string> CommandParameters = new Dictionary<string, string>
        {
            { "s", "Text to Type {ENTER}" },
            { "a", "5" },
            { "l", "100,100" },
            { "r", "100,100" },
            { "d", "100,100,200,200" },
            { "t", "1000" },
            { "f", "image.png" },
            { "g", "image.png" },
            { "j", "image.png" },
            { "k", "image.png" },
            { "h", "image.png" },
            { "v", "image.png,1000" },
            { "n", "5" },
            { "u", "1000" },
            { "z", "NameOfCommand" },
            { "x", "" },
            { "y", "" },
            { "e", "" },
            { "p", ""},
            { "o", ""},
            { "i", "" }
        };
        public static void ShowCommands()
        {
            Console.WriteLine("The following are a series of commands you can use in the -command and -execute options.");
            Console.WriteLine("There are verbose commands and short code commands.");
            Console.WriteLine("They are organized as follows:");
            Console.WriteLine("");
            Console.WriteLine("     {0,-11} {1,-30} {2,0}", "ShortCode", "Verbose", "Parameters");
            Console.WriteLine("     ============================================================");
            foreach (var entry in CommandTranslations)
            {
                if (CommandParameters[entry.Key].Length > 0)
                    Console.WriteLine("         {0,-7} {1,-30} {2,0}",entry.Key,entry.Value,CommandParameters[entry.Key]);
                else
                    Console.WriteLine("         {0,-7} {1,-30} {2,0}", entry.Key, entry.Value, "No Parameters");
            }
            Console.WriteLine("     ============================================================");
            Console.WriteLine("");
            Console.WriteLine("     Examples:");
            Console.WriteLine("     Verbose: Start Menu;Type: explorer.exe{ENTER}");
            Console.WriteLine("          -execute -verbose Start Menu;Type: explorer.exe{ENTER}");
            Console.WriteLine("     ShortCode: e;sexplorer.exe{ENTER}");
            Console.WriteLine("          -execute e;sexplorer.exe{ENTER}");

        }
        public static string TranslateCommand(string command, bool toUserFriendly = true)
        {
            if (toUserFriendly)
            {
                // Convert short form to user-friendly form
                foreach (var entry in CommandTranslations)
                {
                    if (command.Contains(entry.Key))
                    {
                        command = command.Replace(entry.Key, entry.Value);
                    }
                }
            }
            else
            {
                // Convert user-friendly form to short form
                foreach (var entry in CommandTranslations)
                {
                    if (command.Contains(entry.Value))
                    {
                        command = command.Replace(entry.Value, entry.Key);
                    }
                }
            }

            return command;
        }

    // Example usage:
    /*
    public static void Main(string[] args)
    {
        string shortCommand = "s";
        string userFriendlyCommand = TranslateCommand(shortCommand, true);
        Console.WriteLine($"User-friendly: {userFriendlyCommand}");

        string reversedCommand = TranslateCommand(userFriendlyCommand, false);
        Console.WriteLine($"Short form: {reversedCommand}");
        }
    }
    */

        /// <summary>
        /// Executes the specified RPA command.
        /// </summary>
        /// <param name="command">The RPA command to execute.</param>
        public void runRPA(string command)
        {
            DistributionManager dm = new DistributionManager();
            autoRun(dm.collectRPACommands(command));
        }

        /// <summary>
        /// Validates the provided RPA commands.
        /// </summary>
        /// <param name="commands">The commands to validate.</param>
        /// <returns>A string indicating whether the commands are valid or contain errors.</returns>
        public string ValidateCommands(string commands)
        {
            if (commands.EndsWith(";"))
            {
                commands = commands.Substring(0, commands.Length - 1);
            }

            string[] execute = commands.Split(';');
            for (int se = 0; se < execute.Length; se++)
            {
                string wholeExecutionString = execute[se];
                string cmd = wholeExecutionString.Substring(0, 1);

                if (cmd == "[")
                {
                    if (!ValidateLoopCommand(wholeExecutionString, out string loopError))
                    {
                        return loopError;
                    }
                    continue;
                }

                if (NoArgumentMethods.Contains(cmd))
                {
                    if (wholeExecutionString.Length > 1)
                    {
                        return $"Error at command {se + 1}: No-argument method '{cmd}' should not have additional parameters.";
                    }
                }
                else if (SingleArgumentMethods.Contains(cmd))
                {
                    if (cmd == "l" || cmd == "r")
                    {
                        if (!wholeExecutionString.Contains(":"))
                        {
                            return $"Error at command {se + 1}: left and right click '{cmd}' requires coordinates.";
                        }
                        else
                        {
                            string xcords = wholeExecutionString.Replace(cmd, "").Split(':')[0];
                            string ycords = wholeExecutionString.Replace(cmd, "").Split(':')[0];
                            if (!TryParseInteger(xcords, out int resx))
                                return $"Error at command {se + 1}: x coordinates not a number";
                            else if (!TryParseInteger(ycords, out int resy))
                                return $"Error at command {se + 1}: y coordinates not a number";
                        }
                    }
                    else if (cmd == "t")
                    {
                        if (!TryParseInteger(wholeExecutionString.Replace(cmd, ""), out int result))
                        {
                            return $"Error at command {se + 1}: '{cmd}' requires an integer.";
                        }
                    }
                    else if (cmd == "z")
                    {
                        DistributionManager dm = new DistributionManager();
                        string commandCode = dm.collectRPACommands(wholeExecutionString.Substring(1, wholeExecutionString.Length-1));
                        if (string.IsNullOrEmpty(commandCode))
                        {
                            return $"Error at command {se + 1}: '{cmd}' does not exist in the command storage structure.";
                        }
                    }
                    else if (cmd == "u")
                    {
                        if (!wholeExecutionString.Contains(":"))
                        {
                            return $"Error at command {se + 1}: '{cmd}' needs a setting to update and a value. e.g.: Settings:1";
                        }
                        else
                        {
                            GlobalParameters gp = new GlobalParameters();
                            string setting = wholeExecutionString.Split(':')[0];
                            if (gp.getSetting(setting) == "")
                            {
                                return $"Error at command {se + 1}: Setting does not exist.";
                            }
                        }
                    }
                    // Skip "s" as it is a send string operation.
                }
                else if (DoubleArgumentMethods.Contains(cmd))
                {
                    if (!Regex.IsMatch(wholeExecutionString.Substring(1), @"^\d+:\d+$"))
                    {
                        return $"Error at command {se + 1}: Double-argument method '{cmd}' requires parameters in the format 'x:y'.";
                    }
                }
                else if (ImageDetectionMethods.Contains(cmd) || DuplicateImageDetectionMethods.Contains(cmd))
                {
                    string parameter = wholeExecutionString.Substring(1);
                    if (!parameter.Contains(".png"))
                    {
                        return $"Error at command {se + 1}: Image detection method '{cmd}' requires a .png file as a parameter.";
                    }
                    if (parameter.Contains("<") && !ValidateClickParameter(parameter, out string clickError))
                    {
                        return clickError;
                    }
                }
                else
                {
                    return $"Error at command {se + 1}: Unknown command '{cmd}'.";
                }
            }

            return "Valid";
        }

        /// <summary>
        /// Attempts to parse an integer from the provided input string.
        /// </summary>
        /// <param name="input">The input string to parse.</param>
        /// <param name="result">The parsed integer result.</param>
        /// <returns>True if parsing was successful; otherwise, false.</returns>
        private static bool TryParseInteger(string input, out int result)
        {
            if (int.TryParse(input, out result))
            {
                return true;
            }
            else
            {
                result = 0;
                return false;
            }
        }

        /// <summary>
        /// Validates loop commands.
        /// </summary>
        /// <param name="command">The loop command to validate.</param>
        /// <param name="error">The error message if validation fails.</param>
        /// <returns>True if the loop command is valid; otherwise, false.</returns>
        private static bool ValidateLoopCommand(string command, out string error)
        {
            error = string.Empty;

            if (!command.Contains("]"))
            {
                error = "Error: Loop command missing closing bracket ']'.";
                return false;
            }

            string loopPart = command.Substring(1, command.IndexOf("]") - 1);
            if (!loopPart.Contains(":") && !int.TryParse(loopPart, out _))
            {
                error = "Error: Invalid loop syntax.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates click parameters.
        /// </summary>
        /// <param name="parameter">The click parameter to validate.</param>
        /// <param name="error">The error message if validation fails.</param>
        /// <returns>True if the click parameter is valid; otherwise, false.</returns>
        private static bool ValidateClickParameter(string parameter, out string error)
        {
            error = string.Empty;

            string clickPart = parameter.Substring(parameter.IndexOf("<") + 1).TrimEnd('>');
            if (ClickParameters.Contains(clickPart))
            {
                return true;
            }
            if (CustomOffsetRegex.IsMatch($"<{clickPart}>"))
            {
                return true;
            }

            error = $"Error: Invalid click parameter '{clickPart}' in image detection method.";
            return false;
        }



        /// <summary>
        /// Automatically runs a set of RPA commands.
        /// </summary>
        /// <param name="commands">The commands to execute.</param>
        public void autoRun(string commands, int timeout = 0)
        {
            if (string.IsNullOrEmpty(commands))
            {
                Console.WriteLine("Command does not exist, not able to execute.");
                return;
            }

            GlobalParameters gpa = new GlobalParameters();
            if (commands.Substring(commands.Length - 1, 1) == ";")
                commands = commands.Substring(0, commands.Length - 1);
            string[] execute = commands.Split(';');
            for (int se = 0; se < execute.Length; se++)
            {
                string wholeExecutionString = execute[se];
                string cmd = execute[se].Substring(0, 1);
                int loopMCmd = 0;
                int loopCounter = 1;
                if (cmd == "[")
                {
                    int pos = execute[se].IndexOf("]");
                    cmd = execute[se].Split(']')[1];
                    string innerString = execute[se].Split(']')[0].Replace("[", "").Replace("]", "");
                    if (innerString.Contains(":"))
                    {
                        loopMCmd = Convert.ToInt32(innerString.Split(':')[0]);
                        loopCounter = Convert.ToInt32(innerString.Split(':')[1]);
                        execute[se] = execute[se].Replace("[" + loopMCmd.ToString() + ":" + loopCounter.ToString() + "]", "");
                    }
                    else
                    {
                        loopCounter = Convert.ToInt32(innerString);
                        execute[se] = execute[se].Replace("[" + loopCounter.ToString() + "]", "");
                    }
                }
                int x = 0;
                while (x != loopCounter)
                {
                    int loopM = 0;
                    if (loopMCmd > 0)
                        loopM = se + (loopMCmd - 1);
                    else
                        loopM = se;
                    int baseM = se;
                    while (baseM <= loopM)
                    {
                        cmd = execute[baseM].Substring(0, 1);
                        if (NoArgumentMethods.Contains(cmd))
                        {
                            typeof(RPA).GetMethod(cmd, Type.EmptyTypes).Invoke(null, null);
                        }
                        else if (SingleArgumentMethods.Contains(cmd))
                        {
                            string cmdParameter = execute[baseM].Substring(1, execute[baseM].Length - 1);
                            typeof(RPA).GetMethod(cmd, new Type[] { typeof(string) }).Invoke(null, new object[] { cmdParameter });
                        }
                        else if (DoubleArgumentMethods.Contains(cmd))
                        {
                            string cmdParameter1 = execute[baseM].Substring(1, execute[baseM].Length - 1).Split(',')[0];
                            string cmdParameter2 = execute[baseM].Substring(1, execute[baseM].Length - 1).Split(',')[1];
                            typeof(RPA).GetMethod(cmd).Invoke(null, new[] { collectCoordinates(captureScreenshot(), findabledirectory + cmdParameter1), collectCoordinates(captureScreenshot(), findabledirectory + cmdParameter2) });
                        }
                        else if (ImageDetectionMethods.Contains(cmd))
                        {
                            string cmdParameter = execute[baseM].Substring(1, execute[baseM].Length - 1);
                            string screenLocation = "";
                            if (cmd == "v")
                            {
                                try
                                {
                                    string[] getTimeout = cmdParameter.Split(',');
                                    cmdParameter = getTimeout[0];
                                    timeout = Convert.ToInt32(getTimeout);
                                    screenLocation = collectCoordinatesWithTimeout(captureScreenshot(), findabledirectory + cmdParameter, timeout);
                                    if (screenLocation == "Not Found")
                                    {
                                        monitorResult = screenLocation;
                                        return;
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine("Error executing code execution of("+cmd+") due to: " + e.Message);
                                    return;
                                }
                            }
                            else if (execute[baseM].Contains(">"))
                            {
                                string clickParameter = execute[baseM].Split('<')[1].Replace(">", "");
                                screenLocation = collectCoordinates(captureScreenshot(), findabledirectory + cmdParameter, clickParameter);
                            }
                            else
                            {
                                screenLocation = showCaseClickArea(collectCoordinatesWithOrigin(captureScreenshot(), findabledirectory + cmdParameter), findabledirectory + cmdParameter);
                            }

                            if (cmd == "f")
                            {
                                l(screenLocation);
                            }
                            else if (cmd == "g")
                            {
                                Thread.Sleep(250);
                                l(screenLocation);
                                Thread.Sleep(75);
                                l(screenLocation);
                            }
                            else if (cmd == "j")
                            {
                                r(screenLocation);
                            }
                            else if (cmd == "k")
                            {
                                Thread.Sleep(250);
                                r(screenLocation);
                                Thread.Sleep(75);
                                r(screenLocation);
                            }
                        }
                        else if (DuplicateImageDetectionMethods.Contains(cmd))
                        {
                            string cmdParameter = execute[baseM].Substring(1, execute[baseM].Length - 1);
                            string screenLocation = collectAllCoordinates(captureScreenshot(), findabledirectory + cmdParameter);
                            Thread.Sleep(100);
                            l(screenLocation);
                            Thread.Sleep(75);
                            l(screenLocation);
                        }
                        else if (cmd == "z")
                        {
                            string cmdParameter = execute[baseM].Substring(1, execute[baseM].Length - 1);
                            runRPA(cmdParameter);
                        }
                        baseM++;
                        Thread.Sleep(spacing);
                    }
                    x++;
                }
            }
        }

        /// <summary>
        /// Method to get the description function for a command.
        /// </summary>
        /// <param name="commandKey">The command key.</param>
        /// <returns>The description function.</returns>
        public static Func<string, string> GetCommandDescription(char commandKey)
        {
            if (commandDescriptions.TryGetValue(commandKey, out Func<string, string> descriptionFunc))
            {
                return descriptionFunc;
            }
            return null;
        }

        /// <summary>
        /// Method to set or replace the description function for a command.
        /// </summary>
        /// <param name="commandKey">The command key.</param>
        /// <param name="descriptionFunc">The description function.</param>
        public static void SetCommandDescription(char commandKey, Func<string, string> descriptionFunc)
        {
            commandDescriptions[commandKey] = descriptionFunc;
        }

        /// <summary>
        /// Method to add a new command description.
        /// </summary>
        /// <param name="commandKey">The command key.</param>
        /// <param name="descriptionFunc">The description function.</param>
        public static void AddCommandDescription(char commandKey, Func<string, string> descriptionFunc)
        {
            if (!commandDescriptions.ContainsKey(commandKey))
            {
                commandDescriptions.Add(commandKey, descriptionFunc);
            }
            else
            {
                throw new ArgumentException("Command key already exists. Use SetCommandDescription to overwrite.");
            }
        }

        /// <summary>
        /// Translates RPA command to readable format using the global dictionary.
        /// </summary>
        /// <param name="rpaCommand">The RPA command to translate.</param>
        /// <returns>The readable format of the command.</returns>
        public string TranslateRPAtoReadable(string rpaCommand)
        {
            var readable = new StringBuilder();
            string[] commands = rpaCommand.Split(';');

            foreach (var command in commands)
            {
                if (string.IsNullOrEmpty(command)) continue;

                char action = command[0];
                string argument = command.Length > 1 ? command.Substring(1) : string.Empty;

                var descriptionFunc = GetCommandDescription(action);
                if (descriptionFunc != null)
                {
                    readable.Append($"{descriptionFunc(argument)}, ");
                }
                else
                {
                    readable.Append("execute a custom command, ");
                }
            }

            if (readable.Length > 2)
                readable.Remove(readable.Length - 2, 2); // Remove the last comma and space

            return readable.ToString();
        }

        /// <summary>
        /// Captures a screenshot and saves it to a file.
        /// </summary>
        /// <returns>The file path of the saved screenshot.</returns>
        public static string captureScreenshot()
        {
            string path = "m" + DateTime.Now.Day.ToString() + DateTime.Now.Month.ToString() + DateTime.Now.Year.ToString() + DateTime.Now.Hour.ToString() + DateTime.Now.Minute.ToString() + DateTime.Now.Second.ToString() + ".png";
            Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
            Bitmap screenshot = new Bitmap(screenBounds.Width, screenBounds.Height, PixelFormat.Format32bppArgb);
            Graphics grph = Graphics.FromImage(screenshot);
            grph.CopyFromScreen(screenBounds.X, screenBounds.Y, 0, 0, screenBounds.Size, CopyPixelOperation.SourceCopy);
            GlobalParameters globalParameters = new GlobalParameters();
            string fPath = globalParameters.getSetting("TemporaryScreenCapturePath") + path;
            screenshot.Save(fPath);
            return fPath;
        }

        /// <summary>
        /// Collects all coordinates of matching images on the screen.
        /// </summary>
        /// <param name="csPath">The path of the screenshot.</param>
        /// <param name="findablePath">The path of the image to find.</param>
        /// <returns>The coordinates of the last matching image.</returns>
        public static string collectAllCoordinates(string csPath, string findablePath)
        {
            double threshold = 0.9;
            List<string> coordinatesList = new List<string>();

            // Load images
            Image<Bgr, byte> mainImage = new Image<Bgr, byte>(csPath);
            Image<Bgr, byte> templateImage = new Image<Bgr, byte>(findablePath);
            Image<Gray, byte> mainGray = mainImage.Convert<Gray, byte>();
            Image<Gray, byte> templateGray = templateImage.Convert<Gray, byte>();

            try
            {
                // Perform template matching
                using (Image<Gray, float> result = mainGray.MatchTemplate(templateGray, TemplateMatchingType.CcoeffNormed))
                {
                    result._ThresholdBinary(new Gray(threshold), new Gray(1.0)); // Threshold the results to find matches above the threshold

                    // Iterate over the result matrix to find all matches
                    for (int y = 0; y < result.Rows; y++)
                    {
                        for (int x = 0; x < result.Cols; x++)
                        {
                            if (result.Data[y, x, 0] >= threshold)
                            {
                                // Calculate center of the found match
                                int centerx = x + templateImage.Width / 2;
                                int centery = y + templateImage.Height / 2;

                                // Add to results list
                                coordinatesList.Add($"{centerx}:{centery}");
                            }
                        }
                    }
                }

            }
            finally
            {
                if (File.Exists(csPath))
                {
                    File.Delete(csPath);
                }
            }
            return coordinatesList.LastOrDefault();
        }

        public static string showCaseClickArea(string result, string findablePath)
        {
            string coords = result;
            GlobalParameters gp = new GlobalParameters();
            string findableHost = gp.getSetting("ImageStorageDirectory");
            Image<Bgr, byte> templateImage = new Image<Bgr, byte>(findableHost + "\\" + findablePath);
            string widthHeight = templateImage.Width.ToString() + ":" + templateImage.Height.ToString();

            string baseDir = gp.getSetting("BaseDirectoryPath");

            // Construct the arguments
            string[] coordsParts = coords.Split(',');
            string[] hostparts = coordsParts[1].Split(':');
            string[] widthHeightParts = widthHeight.Split(':');
            string arguments = $"{hostparts[0]} {hostparts[1]} {widthHeightParts[0]} {widthHeightParts[1]}";

            // Log the arguments for debugging
            Console.WriteLine("Arguments: " + arguments);

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = baseDir + "\\CaseRPAShell.exe",
                        Arguments = arguments,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    }
                };

                process.Start();
                //process.WaitForExit();
            }
            catch (Exception ex)
            {
                // Log any errors
                Console.WriteLine("Error starting process: " + ex.Message);
            }

            return coordsParts[0];
        }

        /// <summary>
        /// Collects coordinates of a matching image on the screen.
        /// </summary>
        /// <param name="csPath">The path of the screenshot.</param>
        /// <param name="findablePath">The path of the image to find.</param>
        /// <param name="clickPosOverride">The click position override.</param>
        /// <returns>The coordinates of the matching image.</returns>
        public static string collectCoordinates(string csPath, string findablePath, string clickPosOverride = "none")
        {
            bool notFound = true;
            int retx = 0;
            int rety = 0;
            double threshold = 0.9;
            try
            {
                while (notFound)
                {
                    Image<Bgr, byte> mainImage = new Image<Bgr, byte>(csPath);
                    GlobalParameters gp = new GlobalParameters();
                    string findableHost = gp.getSetting("ImageStorageDirectory");
                    Image<Bgr, byte> templateImage = new Image<Bgr, byte>(findableHost + "\\" + findablePath);
                    Image<Gray, byte> mainGray = mainImage.Convert<Gray, byte>();
                    Image<Gray, byte> templateGray = templateImage.Convert<Gray, byte>();
                    double probability = 0.0;
                    using (Image<Gray, float> result = mainGray.MatchTemplate(templateGray, TemplateMatchingType.CcoeffNormed))
                    {
                        double[] minValues, maxValues;
                        Point[] minLocations, maxLocations;
                        result.MinMax(out minValues, out maxValues, out minLocations, out maxLocations);
                        probability = maxValues[0];
                        Point topLeft = maxLocations[0];
                        Point bottomRight = new Point(topLeft.X + templateImage.Width, topLeft.Y + templateImage.Height);
                        retx = Convert.ToInt32((topLeft.X + bottomRight.X) / 2);
                        rety = Convert.ToInt32((topLeft.Y + bottomRight.Y) / 2);
                        if (clickPosOverride != "none")
                        {
                            if (clickPosOverride.Contains(":")) // Custom offset
                            {
                                retx = Convert.ToInt32(clickPosOverride.Split(':')[0]) + topLeft.X;
                                rety = Convert.ToInt32(clickPosOverride.Split(':')[1]) + topLeft.Y;
                            }
                            else if (clickPosOverride == "tl") // Top left
                            {
                                retx = topLeft.X;
                                rety = topLeft.Y;
                            }
                            else if (clickPosOverride == "tm") // Top middle
                            {
                                retx = Convert.ToInt32((topLeft.X + bottomRight.X) / 2);
                                rety = topLeft.Y;
                            }
                            else if (clickPosOverride == "tr") // Top right
                            {
                                retx = bottomRight.X;
                                rety = topLeft.Y;
                            }
                            else if (clickPosOverride == "ml") // Middle left
                            {
                                retx = topLeft.X;
                                rety = Convert.ToInt32((topLeft.Y + bottomRight.Y) / 2);
                            }
                            else if (clickPosOverride == "mr") // Middle right
                            {
                                retx = bottomRight.X;
                                rety = Convert.ToInt32((topLeft.Y + bottomRight.Y) / 2);
                            }
                            else if (clickPosOverride == "bl") // Bottom left
                            {
                                retx = topLeft.X;
                                rety = bottomRight.Y;
                            }
                            else if (clickPosOverride == "bm") // Bottom middle
                            {
                                retx = Convert.ToInt32((topLeft.X + bottomRight.X) / 2);
                                rety = bottomRight.Y;
                            }
                            else if (clickPosOverride == "br") // Bottom right
                            {
                                retx = bottomRight.X;
                                rety = bottomRight.Y;
                            }
                        }
                        CvInvoke.WaitKey(0);
                        CvInvoke.DestroyAllWindows();
                    }
                    if (probability >= threshold)
                    {
                        notFound = false;
                    }
                    else
                    {
                        // Console.WriteLine("object not found, retrying");
                        Thread.Sleep(1000);
                    }
                    File.Delete(csPath);
                    csPath = captureScreenshot();
                }
            }
            finally
            {
                if (File.Exists(csPath))
                {
                    File.Delete(csPath);
                }
            }
            return retx.ToString() + ":" + rety.ToString();
        }
        /// <summary>
        /// Collects coordinates of a matching image on the screen.
        /// </summary>
        /// <param name="csPath">The path of the screenshot.</param>
        /// <param name="findablePath">The path of the image to find.</param>
        /// <param name="clickPosOverride">The click position override.</param>
        /// <returns>The coordinates of the matching image.</returns>
        public static string collectCoordinatesWithOrigin(string csPath, string findablePath, string clickPosOverride = "none")
        {
            bool notFound = true;
            int retx = 0;
            int rety = 0;
            int hostx = 0;
            int hosty = 0;
            double threshold = 0.9;
            try
            {
                while (notFound)
                {
                    Image<Bgr, byte> mainImage = new Image<Bgr, byte>(csPath);
                    GlobalParameters gp = new GlobalParameters();
                    string findableHost = gp.getSetting("ImageStorageDirectory");
                    Image<Bgr, byte> templateImage = new Image<Bgr, byte>(findableHost + "\\" + findablePath);
                    Image<Gray, byte> mainGray = mainImage.Convert<Gray, byte>();
                    Image<Gray, byte> templateGray = templateImage.Convert<Gray, byte>();
                    double probability = 0.0;
                    using (Image<Gray, float> result = mainGray.MatchTemplate(templateGray, TemplateMatchingType.CcoeffNormed))
                    {
                        double[] minValues, maxValues;
                        Point[] minLocations, maxLocations;
                        result.MinMax(out minValues, out maxValues, out minLocations, out maxLocations);
                        probability = maxValues[0];
                        Point topLeft = maxLocations[0];
                        Point bottomRight = new Point(topLeft.X + templateImage.Width, topLeft.Y + templateImage.Height);
                        hostx = topLeft.X;
                        hosty = topLeft.Y;
                        retx = Convert.ToInt32((topLeft.X + bottomRight.X) / 2);
                        rety = Convert.ToInt32((topLeft.Y + bottomRight.Y) / 2);
                        if (clickPosOverride != "none")
                        {
                            if (clickPosOverride.Contains(":")) // Custom offset
                            {
                                retx = Convert.ToInt32(clickPosOverride.Split(':')[0]) + topLeft.X;
                                rety = Convert.ToInt32(clickPosOverride.Split(':')[1]) + topLeft.Y;
                            }
                            else if (clickPosOverride == "tl") // Top left
                            {
                                retx = topLeft.X;
                                rety = topLeft.Y;
                            }
                            else if (clickPosOverride == "tm") // Top middle
                            {
                                retx = Convert.ToInt32((topLeft.X + bottomRight.X) / 2);
                                rety = topLeft.Y;
                            }
                            else if (clickPosOverride == "tr") // Top right
                            {
                                retx = bottomRight.X;
                                rety = topLeft.Y;
                            }
                            else if (clickPosOverride == "ml") // Middle left
                            {
                                retx = topLeft.X;
                                rety = Convert.ToInt32((topLeft.Y + bottomRight.Y) / 2);
                            }
                            else if (clickPosOverride == "mr") // Middle right
                            {
                                retx = bottomRight.X;
                                rety = Convert.ToInt32((topLeft.Y + bottomRight.Y) / 2);
                            }
                            else if (clickPosOverride == "bl") // Bottom left
                            {
                                retx = topLeft.X;
                                rety = bottomRight.Y;
                            }
                            else if (clickPosOverride == "bm") // Bottom middle
                            {
                                retx = Convert.ToInt32((topLeft.X + bottomRight.X) / 2);
                                rety = bottomRight.Y;
                            }
                            else if (clickPosOverride == "br") // Bottom right
                            {
                                retx = bottomRight.X;
                                rety = bottomRight.Y;
                            }
                        }
                        CvInvoke.WaitKey(0);
                        CvInvoke.DestroyAllWindows();
                    }
                    if (probability >= threshold)
                    {
                        notFound = false;
                    }
                    else
                    {
                        // Console.WriteLine("object not found, retrying");
                        Thread.Sleep(1000);
                    }
                    File.Delete(csPath);
                    csPath = captureScreenshot();
                }
            }
            finally
            {
                if (File.Exists(csPath))
                {
                    File.Delete(csPath);
                }
            }
            return retx.ToString() + ":" + rety.ToString()+","+hostx.ToString()+":"+hosty.ToString();
        }
        /// <summary>
        /// Collects coordinates of a matching image on the screen, times out if not found.
        /// </summary>
        /// <param name="csPath">The path of the screenshot.</param>
        /// <param name="findablePath">The path of the image to find.</param>
        /// <param name="timeout">The timeout period in milliseconds.</param>
        /// <param name="clickPosOverride">The click position override.</param>
        /// <returns>The coordinates of the matching image.</returns>
        public static string collectCoordinatesWithTimeout(string csPath, string findablePath, int timeout, string clickPosOverride = "none")
        {
            bool notFound = true;
            int retx = 0;
            int rety = 0;
            double threshold = 0.9;
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);

            try
            {
                while (notFound)
                {
                    cts.Token.ThrowIfCancellationRequested();

                    Image<Bgr, byte> mainImage = new Image<Bgr, byte>(csPath);
                    GlobalParameters gp = new GlobalParameters();
                    string findableHost = gp.getSetting("ImageStorageDirectory");
                    Image<Bgr, byte> templateImage = new Image<Bgr, byte>(findableHost + "\\" + findablePath);
                    Image<Gray, byte> mainGray = mainImage.Convert<Gray, byte>();
                    Image<Gray, byte> templateGray = templateImage.Convert<Gray, byte>();
                    double probability = 0.0;
                    using (Image<Gray, float> result = mainGray.MatchTemplate(templateGray, TemplateMatchingType.CcoeffNormed))
                    {
                        double[] minValues, maxValues;
                        Point[] minLocations, maxLocations;
                        result.MinMax(out minValues, out maxValues, out minLocations, out maxLocations);
                        probability = maxValues[0];
                        Point topLeft = maxLocations[0];
                        Point bottomRight = new Point(topLeft.X + templateImage.Width, topLeft.Y + templateImage.Height);
                        retx = Convert.ToInt32((topLeft.X + bottomRight.X) / 2);
                        rety = Convert.ToInt32((topLeft.Y + bottomRight.Y) / 2);
                        if (clickPosOverride != "none")
                        {
                            if (clickPosOverride.Contains(":")) // Custom offset
                            {
                                retx = Convert.ToInt32(clickPosOverride.Split(':')[0]) + topLeft.X;
                                rety = Convert.ToInt32(clickPosOverride.Split(':')[1]) + topLeft.Y;
                            }
                            else if (clickPosOverride == "tl") // Top left
                            {
                                retx = topLeft.X;
                                rety = topLeft.Y;
                            }
                            else if (clickPosOverride == "tm") // Top middle
                            {
                                retx = Convert.ToInt32((topLeft.X + bottomRight.X) / 2);
                                rety = topLeft.Y;
                            }
                            else if (clickPosOverride == "tr") // Top right
                            {
                                retx = bottomRight.X;
                                rety = topLeft.Y;
                            }
                            else if (clickPosOverride == "ml") // Middle left
                            {
                                retx = topLeft.X;
                                rety = Convert.ToInt32((topLeft.Y + bottomRight.Y) / 2);
                            }
                            else if (clickPosOverride == "mr") // Middle right
                            {
                                retx = bottomRight.X;
                                rety = Convert.ToInt32((topLeft.Y + bottomRight.Y) / 2);
                            }
                            else if (clickPosOverride == "bl") // Bottom left
                            {
                                retx = topLeft.X;
                                rety = bottomRight.Y;
                            }
                            else if (clickPosOverride == "bm") // Bottom middle
                            {
                                retx = Convert.ToInt32((topLeft.X + bottomRight.X) / 2);
                                rety = bottomRight.Y;
                            }
                            else if (clickPosOverride == "br") // Bottom right
                            {
                                retx = bottomRight.X;
                                rety = bottomRight.Y;
                            }
                        }
                        CvInvoke.WaitKey(0);
                        CvInvoke.DestroyAllWindows();
                    }
                    if (probability >= threshold)
                    {
                        notFound = false;
                    }
                    else
                    {
                        // Console.WriteLine("object not found, retrying");
                        Thread.Sleep(1000);
                    }
                    csPath = captureScreenshot();
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Timeout reached. Image not found.");
                return "Not Found";
            }
            finally
            {
                if (File.Exists(csPath))
                {
                    File.Delete(csPath);
                }
            }
            return retx.ToString() + ":" + rety.ToString();
        }


        /// <summary>
        /// Updates settings.
        /// </summary>
        /// <param name="updateSettings">The settings to update.</param>
        public static void u(string updateSettings)
        {
            if (updateSettings.Substring(1, 1) == "s")
            {
                if (int.TryParse(updateSettings.Replace("us", ""), out int n))
                {
                    spacing = n;
                }
            }
        }

        /// <summary>
        /// Sends keys to the keyboard.
        /// </summary>
        /// <param name="keys">The keys to send.</param>
        public static void s(string keys)//, bool relacementSemicolon = false, char replacement = '~')
        {
            //check for file load.
            if (keys.Substring(0, 1) == "(")
            {
                string fPath = keys.Substring(1, keys.Length - 2);
                RPA rPA = new RPA();
                keys = rPA.translateTextFiletoSK(fPath);
                if (keys == "File does not exist.")
                {
                    Console.WriteLine("Exiting s string, file does not exist.");
                    return;
                }
            }
            SendKeys.SendWait(keys);
        }

        /// <summary>
        /// Performs a left mouse click.
        /// </summary>
        /// <param name="input">The coordinates for the click.</param>
        public static void l(string input)
        {
            sendMouseLeftclick(new Point(Convert.ToInt32(input.Split(':')[0]), Convert.ToInt32(input.Split(':')[1])));
        }

        /// <summary>
        /// Performs a right mouse click.
        /// </summary>
        /// <param name="input">The coordinates for the click.</param>
        public static void r(string input)
        {
            sendMouseRightclick(new Point(Convert.ToInt32(input.Split(':')[0]), Convert.ToInt32(input.Split(':')[1])));
        }

        /// <summary>
        /// Performs a click and drag operation.
        /// </summary>
        /// <param name="startinput">The start coordinates.</param>
        /// <param name="endinput">The end coordinates.</param>
        public static void d(string startinput, string endinput)
        {
            sendMouseDown(new Point(Convert.ToInt32(startinput.Split(':')[0]), Convert.ToInt32(startinput.Split(':')[1])));
            Thread.Sleep(500);
            sendMouseUp(new Point(Convert.ToInt32(endinput.Split(':')[0]), Convert.ToInt32(endinput.Split(':')[1])));
        }

        /// <summary>
        /// Sends a tab key press.
        /// </summary>
        /// <param name="fullcmd">The command specifying the number of tabs.</param>
        public static void n(string fullcmd = "")
        {
            if (fullcmd != "")
            {
                if (int.TryParse(fullcmd.Replace("nt", ""), out int n))
                {
                    for (int i = 0; i < n; i++)
                    {
                        s("{Tab}");
                        Thread.Sleep(spacing);
                    }
                }
            }
            else
            {
                s("{Tab}");
            }
        }

        /// <summary>
        /// Sends an Alt+Tab key press.
        /// </summary>
        /// <param name="fullcmd">The command specifying the number of Alt+Tab presses.</param>
        public static void a(string fullcmd = "")
        {
            if (fullcmd != "")
            {
                if (int.TryParse(fullcmd.Replace("at", ""), out int n))
                {
                    string tabs = "";
                    for (int i = 0; i < n; i++)
                    {
                        tabs += "{Tab}";
                    }
                    s("%("+tabs+")");
                }
            }
            else
            {
                s("%({Tab})");
            }
        }

        /// <summary>
        /// Maximizes the window.
        /// </summary>
        public static void x()
        {
            s("%( x)");
        }

        /// <summary>
        /// Minimizes the window.
        /// </summary>
        public static void y()
        {
            s("%( n)");
        }

        /// <summary>
        /// Runs a nested command.
        /// </summary>
        /// <param name="command">The command to run.</param>
        public static void z(string command)
        {
            RPA rp = new RPA();
            rp.runRPA(command);
        }

        /// <summary>
        /// Opens the Start menu.
        /// </summary>
        public static void e()
        {
            s("^{ESC}");
        }

        /// <summary>
        /// Copies selected data to the clipboard.
        /// </summary>
        public static void p()
        {
            s("^c");
        }

        /// <summary>
        /// Pastes data from the clipboard.
        /// </summary>
        public static void o()
        {
            s("^v");
        }

        /// <summary>
        /// Selects all data in the current context.
        /// </summary>
        public static void i()
        {
            s("^a");
        }

        /// <summary>
        /// Forces the system to sleep before proceeding.
        /// </summary>
        /// <param name="time">The duration to sleep in milliseconds.</param>
        public static void t(string time)
        {
            Thread.Sleep(Convert.ToInt32(time));
        }

        // Core system functions

        /// <summary>
        /// Sends a right mouse click event.
        /// </summary>
        /// <param name="p">The coordinates for the click.</param>
        static void sendMouseRightclick(Point p)
        {
            SetCursorPos(p.X, p.Y);
            mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, (uint)p.X, (uint)p.Y, 0, (UIntPtr)0);
        }

        /// <summary>
        /// Sends a left mouse click event.
        /// </summary>
        /// <param name="p">The coordinates for the click.</param>
        static void sendMouseLeftclick(Point p)
        {
            SetCursorPos(p.X, p.Y);
            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)p.X, (uint)p.Y, 0, (UIntPtr)0);
        }

        /// <summary>
        /// Sends a mouse down event.
        /// </summary>
        /// <param name="p">The coordinates for the mouse down.</param>
        static void sendMouseDown(Point p)
        {
            SetCursorPos(p.X, p.Y);
            Thread.Sleep(spacing);
            mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)p.X, (uint)p.Y, 0, (UIntPtr)0);
            Thread.Sleep(spacing);
            mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)p.X, (uint)p.Y, 0, (UIntPtr)0);
            Thread.Sleep(spacing);
        }

        /// <summary>
        /// Sends a mouse up event.
        /// </summary>
        /// <param name="p">The coordinates for the mouse up.</param>
        static void sendMouseUp(Point p)
        {
            SetCursorPos(p.X, p.Y);
            Thread.Sleep(spacing);
            mouse_event(MOUSEEVENTF_LEFTUP, (uint)p.X, (uint)p.Y, 0, (UIntPtr)0);
            Thread.Sleep(spacing);
        }

        /// <summary>
        /// Initializes clipboard monitoring.
        /// </summary>
        /// <param name="sess">The session GUID.</param>
        /// <param name="timer">The timer interval.</param>
        public void initClipboardMonitor(Guid sess, int timer)
        {
            ClipboardMonitor monitor = new ClipboardMonitor();
            monitor.Start(sess, timer);
            Console.WriteLine("Clipboard monitor started. Press Enter to exit...");
            Console.ReadLine();
            monitor.Stop();
        }

        public string translateTextFiletoSK(string fPath)
        {
            if (File.Exists(fPath))
            { 
                return TranslateToSendKeysText(File.ReadAllText(fPath));
            }
            else
            {
                return "File does not exist.";
            }
        }

        private static readonly Dictionary<char, string> SendKeysSpecialCharacters = new Dictionary<char, string>
        {
            { '{', "{{}" },
            { '}', "{}}" },
            { '(', "{(}" },
            { ')', "{)}" },
            { '+', "{+}" },
            { '^', "{^}" },
            { '%', "{%}" },
            { '~', "{~}" },
            { '[', "{[}" },
            { ']', "{]}" },
            { ';', "{;}"},
        };

        // List of valid SendKeys commands
        private static readonly HashSet<string> SendKeysCommands = new HashSet<string>
        {
            "{ENTER}", "{UP}", "{DOWN}", "{LEFT}", "{RIGHT}", "{HOME}", "{END}", "{PGUP}", "{PGDN}",
            "{INSERT}", "{DELETE}", "{TAB}", "{ESC}", "{BACKSPACE}", "{BREAK}", "{CAPSLOCK}",
            "{CLEAR}", "{NUMLOCK}", "{SCROLLLOCK}", "{F1}", "{F2}", "{F3}", "{F4}", "{F5}", "{F6}",
            "{F7}", "{F8}", "{F9}", "{F10}", "{F11}", "{F12}"
        };

        /// <summary>
        /// Translates text to SendKeys format.
        /// </summary>
        /// <param name="input">The input text to translate.</param>
        /// <returns>The translated text in SendKeys format.</returns>
        public string TranslateToSendKeysText(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            StringBuilder result = new StringBuilder();
            int i = 0;

            while (i < input.Length)
            {
                if (input[i] == '{')
                {
                    // Look ahead to find the closing brace
                    int closingBraceIndex = input.IndexOf('}', i);
                    if (closingBraceIndex > i)
                    {
                        // Extract the potential command
                        string potentialCommand = input.Substring(i, closingBraceIndex - i + 1);
                        if (SendKeysCommands.Contains(potentialCommand))
                        {
                            // If it's a valid command, add it as is
                            result.Append(potentialCommand);
                            i = closingBraceIndex + 1;
                            continue;
                        }
                    }
                }

                // Handle \r and \n characters
                if (input[i] == '\r' || input[i] == '\n')
                {
                    if (input[i] == '\r' && (i + 1 < input.Length && input[i + 1] == '\n'))
                    {
                        // Handle \r\n as a single {ENTER}
                        result.Append("{ENTER}");
                        i += 2;
                    }
                    else
                    {
                        // Handle \r or \n individually as {ENTER}
                        result.Append("{ENTER}");
                        i++;
                    }
                    continue;
                }

                // If it's a special character that needs escaping
                if (SendKeysSpecialCharacters.ContainsKey(input[i]))
                {
                    result.Append(SendKeysSpecialCharacters[input[i]]);
                }
                else
                {
                    result.Append(input[i]);
                }
                i++;
            }

            return result.ToString();
        }
    }
}
