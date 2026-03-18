using System;
using System.Collections.Generic;
using AutoCADMCPPlugin.Models;
using AutoCADMCPPlugin.Commands;

namespace AutoCADMCPPlugin.Core
{
    /// <summary>
    /// Registry that maps JSON-RPC method names to command implementations.
    /// All commands are registered at startup. New commands can be added
    /// by implementing ICommand and registering here.
    /// </summary>
    public static class CommandRegistry
    {
        private static readonly Dictionary<string, ICommand> _commands = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase);
        private static volatile bool _initialized;
        private static readonly object _initLock = new object();

        /// <summary>
        /// Get a command by its JSON-RPC method name.
        /// Returns null if the method is not registered.
        /// </summary>
        public static ICommand GetCommand(string method)
        {
            EnsureInitialized();
            _commands.TryGetValue(method, out ICommand command);
            return command;
        }

        /// <summary>
        /// Get all registered method names.
        /// </summary>
        public static IEnumerable<string> GetAllMethods()
        {
            EnsureInitialized();
            return _commands.Keys;
        }

        private static void Register(ICommand command)
        {
            _commands[command.MethodName] = command;
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (_initLock)
            {
                if (_initialized) return;
                RegisterAllCommands();
                _initialized = true;
            }
        }

        private static void RegisterAllCommands()
        {
            // === System ===
            Register(new SystemStatusCommand());
            Register(new ListMethodsCommand());
            Register(new SetSystemVariableCommand());
            Register(new GetSystemVariableCommand());
            Register(new ExecuteCommandCommand());

            // === Drawing / Document ===
            Register(new DrawingNewCommand());
            Register(new DrawingOpenCommand());
            Register(new DrawingSaveCommand());
            Register(new DrawingInfoCommand());

            // === Entity Creation ===
            Register(new CreateLineCommand());
            Register(new CreateCircleCommand());
            Register(new CreateArcCommand());
            Register(new CreatePolylineCommand());
            Register(new CreateRectangleCommand());
            Register(new CreateEllipseCommand());
            Register(new CreateTextCommand());
            Register(new CreateMTextCommand());
            Register(new CreateHatchCommand());

            // === Entity Query & Modification ===
            Register(new ListEntitiesCommand());
            Register(new GetEntityCommand());
            Register(new EraseEntityCommand());
            Register(new MoveEntityCommand());
            Register(new CopyEntityCommand());
            Register(new RotateEntityCommand());
            Register(new ScaleEntityCommand());
            Register(new MirrorEntityCommand());

            // === Layers ===
            Register(new ListLayersCommand());
            Register(new CreateLayerCommand());
            Register(new SetCurrentLayerCommand());
            Register(new SetLayerPropertiesCommand());

            // === Blocks ===
            Register(new ListBlocksCommand());
            Register(new InsertBlockCommand());

            // === Annotations ===
            Register(new CreateLinearDimensionCommand());
            Register(new CreateAlignedDimensionCommand());

            // === View ===
            Register(new ZoomExtentsCommand());
            Register(new ZoomWindowCommand());
        }
    }
}
