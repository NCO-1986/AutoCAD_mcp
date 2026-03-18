using Newtonsoft.Json.Linq;

namespace AutoCADMCPPlugin.Models
{
    /// <summary>
    /// Unified result type for all command executions.
    /// Wraps success/failure state with optional JSON data.
    /// </summary>
    public class CommandResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public JToken Data { get; set; }

        public static CommandResult Ok(JToken data = null)
        {
            return new CommandResult
            {
                Success = true,
                Data = data ?? new JObject { ["success"] = true }
            };
        }

        public static CommandResult Ok(string message)
        {
            return new CommandResult
            {
                Success = true,
                Data = new JObject
                {
                    ["success"] = true,
                    ["message"] = message
                }
            };
        }

        public static CommandResult Fail(string error)
        {
            return new CommandResult
            {
                Success = false,
                Error = error
            };
        }
    }
}
