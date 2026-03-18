using Newtonsoft.Json.Linq;

namespace AutoCADMCPPlugin.Models
{
    /// <summary>
    /// Interface for all MCP commands that can be executed against AutoCAD.
    /// Each command receives JSON parameters and returns a CommandResult.
    /// Commands run on AutoCAD's main thread (marshaled via IdleActionRunner).
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Unique method name for JSON-RPC routing (e.g., "create_line", "list_layers").
        /// </summary>
        string MethodName { get; }

        /// <summary>
        /// Execute the command with the given parameters.
        /// This always runs on AutoCAD's UI thread.
        /// </summary>
        CommandResult Execute(JObject parameters);
    }
}
