using System;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(AutoCADMCPPlugin.Core.Plugin))]
[assembly: CommandClass(typeof(AutoCADMCPPlugin.Core.Plugin))]

namespace AutoCADMCPPlugin.Core
{
    /// <summary>
    /// Main entry point for the AutoCAD MCP Plugin.
    /// Implements IExtensionApplication for automatic loading.
    /// Exposes MCPSTART / MCPSTOP commands for manual control.
    /// </summary>
    public class Plugin : IExtensionApplication
    {
        private static SocketServer _server;
        private static readonly object _lock = new object();

        public const int DefaultPort = 8081;
        public const string PluginName = "AutoCAD MCP Plugin";
        public const string Version = "1.0.0";

        public void Initialize()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage($"\n[MCP] {PluginName} v{Version} loaded.");
            ed?.WriteMessage("\n[MCP] Use MCPSTART to start the server, MCPSTOP to stop it.");
        }

        public void Terminate()
        {
            StopServer();
        }

        [CommandMethod("MCPSTART")]
        public static void StartCommand()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            lock (_lock)
            {
                if (_server != null && _server.IsRunning)
                {
                    ed?.WriteMessage($"\n[MCP] Server already running on port {_server.Port}.");
                    return;
                }

                int port = DefaultPort;

                // Allow user to specify a custom port
                PromptIntegerOptions opts = new PromptIntegerOptions("\n[MCP] Enter port number")
                {
                    DefaultValue = DefaultPort,
                    AllowNone = true,
                    AllowZero = false,
                    AllowNegative = false,
                    LowerLimit = 1024,
                    UpperLimit = 65535
                };

                PromptIntegerResult result = ed?.GetInteger(opts);
                if (result != null && result.Status == PromptStatus.OK)
                    port = result.Value;

                try
                {
                    _server = new SocketServer(port);
                    _server.Start();
                    ed?.WriteMessage($"\n[MCP] Server started on localhost:{port}");
                }
                catch (System.Exception ex)
                {
                    ed?.WriteMessage($"\n[MCP] Failed to start server: {ex.Message}");
                }
            }
        }

        [CommandMethod("MCPSTOP")]
        public static void StopCommand()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            lock (_lock)
            {
                if (_server == null || !_server.IsRunning)
                {
                    ed?.WriteMessage("\n[MCP] Server is not running.");
                    return;
                }

                StopServer();
                ed?.WriteMessage("\n[MCP] Server stopped.");
            }
        }

        [CommandMethod("MCPSTATUS")]
        public static void StatusCommand()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            if (_server != null && _server.IsRunning)
            {
                ed?.WriteMessage($"\n[MCP] Server running on localhost:{_server.Port}");
                ed?.WriteMessage($"\n[MCP] Active connections: {_server.ActiveConnections}");
            }
            else
            {
                ed?.WriteMessage("\n[MCP] Server is not running.");
            }
        }

        private static void StopServer()
        {
            try
            {
                _server?.Stop();
                _server = null;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MCP] Error stopping server: {ex.Message}");
            }
        }
    }
}
