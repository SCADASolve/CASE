//----------------------------------------------------------------------------
//  Copyright (C) 2024 by SCADA Solve LLC. All rights reserved.              
//----------------------------------------------------------------------------
using System;
using System.Windows.Forms;
using System.Threading;

namespace Case
{
    /// <summary>
    /// Monitors the clipboard for changes and executes commands based on the clipboard content.
    /// </summary>
    class ClipboardMonitor
    {
        private System.Threading.Timer _timer;
        private static string lastExecuted = "";
        private static bool isExecutingCommand = false;
        private Guid currentSession;

        /// <summary>
        /// Starts the clipboard monitoring.
        /// </summary>
        /// <param name="sess">The current session GUID.</param>
        /// <param name="timerInterval">The interval at which to check the clipboard, in milliseconds.</param>
        public void Start(Guid sess, int timerInterval)
        {
            currentSession = sess;
            _timer = new System.Threading.Timer(CheckClipboard, null, 0, timerInterval);
        }

        /// <summary>
        /// Checks the clipboard for new content.
        /// </summary>
        /// <param name="state">The state object (unused).</param>
        private void CheckClipboard(object state)
        {
            if (isExecutingCommand)
            {
                return; // Skip this check if a command is already being executed
            }

            try
            {
                string collectedData = GetClipboardText();

                if (!string.IsNullOrEmpty(collectedData) && collectedData != lastExecuted)
                {
                    lastExecuted = collectedData;
                    ExecuteCommand(collectedData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking clipboard: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the current text content from the clipboard.
        /// </summary>
        /// <returns>The clipboard text content, or null if no text is found.</returns>
        private string GetClipboardText()
        {
            string collectedData = null;
            Thread staThread = new Thread(
                delegate ()
                {
                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            collectedData = Clipboard.GetText();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error accessing clipboard: {ex.Message}");
                    }
                });
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();

            return collectedData;
        }

        /// <summary>
        /// Executes a command based on the provided clipboard text.
        /// </summary>
        /// <param name="clipboardText">The text from the clipboard to execute as a command.</param>
        private void ExecuteCommand(string clipboardText)
        {
            isExecutingCommand = true;

            Thread commandThread = new Thread(() =>
            {
                try
                {
                    DistributionManager dM = new DistributionManager();
                    string commands = dM.collectRPACommands(clipboardText);

                    if (!string.IsNullOrEmpty(commands))
                    {
                        Console.WriteLine("Executing: " + commands);
                        RPA rPA = new RPA();
                        rPA.autoRun(rPA.TranslateToSendKeysText(commands));
                        dM.distributeCaseLogs(currentSession, "Executed: " + clipboardText);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing command: {ex.Message}");
                }
                finally
                {
                    isExecutingCommand = false;
                }
            });

            commandThread.Start();
        }

        /// <summary>
        /// Stops the clipboard monitoring.
        /// </summary>
        public void Stop()
        {
            _timer?.Dispose();
        }
    }
}
