﻿#region Using Statements

using System;
using System.IO;
using System.Text;
using GameAnalyticsSDK.Net;

#if WINDOWS
using System.Windows.Forms;
using Microsoft.Xna.Framework.Graphics;
#endif

#endregion

namespace Barotrauma
{
#if WINDOWS || LINUX
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        private static int restartAttempts;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            using (var game = new GameMain())
            {     
#if DEBUG
                game.Run();
#else
                bool attemptRestart = false;

                do
                {
                    try
                    {                        
                        game.Run();
                        attemptRestart = false;
                    }
                    catch (Exception e)
                    {                   
                        if (restartAttempts < 5 && CheckException(game, e))
                        {
                            attemptRestart = true;
                            restartAttempts++;
                        }
                        else
                        {
                            CrashDump(game, "crashreport.txt", e);
                            attemptRestart = false;
                        }

                    }
                } while (attemptRestart);
#endif
            }
        }

        private static bool CheckException(GameMain game, Exception e)
        {
#if WINDOWS

            if (e is SharpDX.SharpDXException sharpDxException)
            {
                DebugConsole.NewMessage("SharpDX exception caught. ("
                    + e.Message + ", " + sharpDxException.ResultCode.Code.ToString("X") + "). Attempting to fix...", Microsoft.Xna.Framework.Color.Red);

                switch ((UInt32)sharpDxException.ResultCode.Code)
                {
                    case 0x887A0022: //DXGI_ERROR_NOT_CURRENTLY_AVAILABLE
                        switch (restartAttempts)
                        {
                            case 0:
                                //just wait and try again
                                DebugConsole.NewMessage("Retrying after 100 ms...", Microsoft.Xna.Framework.Color.Red);
                                System.Threading.Thread.Sleep(100);
                                return true;
                            case 1:
                                //force focus to this window
                                DebugConsole.NewMessage("Forcing focus to the window and retrying...", Microsoft.Xna.Framework.Color.Red);
                                var myForm = (Form)Control.FromHandle(game.Window.Handle);
                                myForm.Focus();
                                return true;
                            case 2:
                                //try disabling hardware mode switch
                                if (GameMain.Config.WindowMode == WindowMode.Fullscreen)
                                {
                                    DebugConsole.NewMessage("Failed to set fullscreen mode, switching configuration to borderless windowed.", Microsoft.Xna.Framework.Color.Red);
                                    GameMain.Config.WindowMode = WindowMode.BorderlessWindowed;
                                    GameMain.Config.Save("config.xml");
                                }
                                return false;
                            default:
                                DebugConsole.NewMessage("Failed to resolve the DXGI_ERROR_NOT_CURRENTLY_AVAILABLE exception. Give up and let it crash :(", Microsoft.Xna.Framework.Color.Red);
                                return false;
                            
                        }
                    case 0x80070057: //E_INVALIDARG/Invalid Arguments
                        DebugConsole.NewMessage("Invalid graphics settings, attempting to fix...", Microsoft.Xna.Framework.Color.Red);

                        GameMain.Config.GraphicsWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                        GameMain.Config.GraphicsHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;

                        DebugConsole.NewMessage("Display size set to " + GameMain.Config.GraphicsWidth + "x" + GameMain.Config.GraphicsHeight, Microsoft.Xna.Framework.Color.Red);

                        game.ApplyGraphicsSettings();

                        return true;
                    default:
                        DebugConsole.NewMessage("Unknown SharpDX exception code (" + sharpDxException.ResultCode.Code.ToString("X") + ")", Microsoft.Xna.Framework.Color.Red);
                        return false;
                }
            }

#endif
                
            return false;            
        }

        public static void CrashMessageBox(string message)
        {
#if WINDOWS
            MessageBox.Show(message, "Oops! Barotrauma just crashed.", MessageBoxButtons.OK, MessageBoxIcon.Error);
#endif         
        }

        static void CrashDump(GameMain game, string filePath, Exception exception)
        {
            DebugConsole.DequeueMessages();

            StreamWriter sw = new StreamWriter(filePath);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Barotrauma Client crash report (generated on " + DateTime.Now + ")");
            sb.AppendLine("\n");
            sb.AppendLine("Barotrauma seems to have crashed. Sorry for the inconvenience! ");
            sb.AppendLine("\n");
#if DEBUG
            sb.AppendLine("Game version " + GameMain.Version + " (debug build)");
#else
            sb.AppendLine("Game version " + GameMain.Version);
#endif
            sb.AppendLine("Graphics mode: " + GameMain.Config.GraphicsWidth + "x" + GameMain.Config.GraphicsHeight + " (" + GameMain.Config.WindowMode.ToString() + ")");
            sb.AppendLine("Selected content package: " + GameMain.SelectedPackage.Name);
            sb.AppendLine("Level seed: " + ((Level.Loaded == null) ? "no level loaded" : Level.Loaded.Seed));
            sb.AppendLine("Loaded submarine: " + ((Submarine.MainSub == null) ? "None" : Submarine.MainSub.Name + " (" + Submarine.MainSub.MD5Hash + ")"));
            sb.AppendLine("Selected screen: " + (Screen.Selected == null ? "None" : Screen.Selected.ToString()));

            if (GameMain.Server != null)
            {
                sb.AppendLine("Server (" + (GameMain.Server.GameStarted ? "Round had started)" : "Round hadn't been started)"));
            }
            else if (GameMain.Client != null)
            {
                sb.AppendLine("Client (" + (GameMain.Client.GameStarted ? "Round had started)" : "Round hadn't been started)"));
            }

            sb.AppendLine("\n");
            sb.AppendLine("System info:");
            sb.AppendLine("    Operating system: " + System.Environment.OSVersion + (System.Environment.Is64BitOperatingSystem ? " 64 bit" : " x86"));
            
            if (game.GraphicsDevice == null)
            {
                sb.AppendLine("    Graphics device not set");
            }
            else
            {
                if (game.GraphicsDevice.Adapter == null)
                {
                    sb.AppendLine("    Graphics adapter not set");
                }
                else
                {
                    sb.AppendLine("    GPU name: " + game.GraphicsDevice.Adapter.Description);
                    sb.AppendLine("    Display mode: " + game.GraphicsDevice.Adapter.CurrentDisplayMode);
                }

                sb.AppendLine("    GPU status: " + game.GraphicsDevice.GraphicsDeviceStatus);
            }

            sb.AppendLine("\n");
            sb.AppendLine("Exception: " + exception.Message);
            sb.AppendLine("Target site: " + exception.TargetSite.ToString());
            sb.AppendLine("Stack trace: ");
            sb.AppendLine(exception.StackTrace);
            sb.AppendLine("\n");

            sb.AppendLine("Last debug messages:");
            for (int i = DebugConsole.Messages.Count - 1; i >= 0; i--)
            {
                sb.AppendLine("[" + DebugConsole.Messages[i].Time + "] " + DebugConsole.Messages[i].Text);
            }

            string crashReport = sb.ToString();
            
            sw.WriteLine(crashReport);
            sw.Close();

            if (GameSettings.SaveDebugConsoleLogs) DebugConsole.SaveLogs();

            if (GameSettings.SendUserStatistics)
            {
                CrashMessageBox( "A crash report (\"crashreport.log\") was saved in the root folder of the game and sent to the developers.");
                GameAnalytics.AddErrorEvent(EGAErrorSeverity.Critical, crashReport);
                GameAnalytics.OnStop();
            }
            else
            {
                CrashMessageBox("A crash report (\"crashreport.log\") was saved in the root folder of the game. The error was not sent to the developers because user statistics have been disabled, but" +
                    " if you'd like to help fix this bug, you may post it on Barotrauma's GitHub issue tracker: https://github.com/Regalis11/Barotrauma/issues/");
            }
        }
    }
#endif
}
