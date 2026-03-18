using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoCADMCPPlugin.Core
{
    /// <summary>
    /// Handles JSON-RPC 2.0 message parsing and routing.
    /// Delegates command execution to the CommandRegistry.
    /// </summary>
    public static class JsonRpcHandler
    {
        // JSON-RPC 2.0 error codes
        private const int ParseError = -32700;
        private const int InvalidRequest = -32600;
        private const int MethodNotFound = -32601;
        private const int InvalidParams = -32602;
        private const int InternalError = -32603;

        /// <summary>
        /// Process a raw JSON-RPC request string and return the response JSON.
        /// </summary>
        public static string ProcessRequest(string requestJson)
        {
            JObject request;
            object id = null;

            try
            {
                request = JObject.Parse(requestJson);
            }
            catch (JsonReaderException)
            {
                return CreateErrorResponse(null, ParseError, "Parse error: Invalid JSON");
            }

            // Extract request ID (can be string, number, or null)
            id = request["id"]?.ToObject<object>();

            // Validate JSON-RPC 2.0 structure
            string jsonrpc = request["jsonrpc"]?.ToString();
            if (jsonrpc != "2.0")
            {
                return CreateErrorResponse(id, InvalidRequest, "Invalid Request: jsonrpc must be '2.0'");
            }

            string method = request["method"]?.ToString();
            if (string.IsNullOrEmpty(method))
            {
                return CreateErrorResponse(id, InvalidRequest, "Invalid Request: method is required");
            }

            JObject parameters = request["params"] as JObject ?? new JObject();

            // Look up command in registry
            var command = CommandRegistry.GetCommand(method);
            if (command == null)
            {
                return CreateErrorResponse(id, MethodNotFound, $"Method not found: {method}");
            }

            // Execute on AutoCAD's main thread via IdleActionRunner
            try
            {
                var result = IdleActionRunner.RunOnMainThread(() =>
                {
                    return command.Execute(parameters);
                });

                if (result.Success)
                {
                    return CreateSuccessResponse(id, result.Data);
                }
                else
                {
                    return CreateErrorResponse(id, InternalError, result.Error);
                }
            }
            catch (TimeoutException)
            {
                return CreateErrorResponse(id, InternalError,
                    "Timeout: AutoCAD did not process the command within the allowed time. " +
                    "Ensure AutoCAD is not in a modal state (dialog box, command prompt).");
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(id, InternalError, $"Internal error: {ex.Message}");
            }
        }

        private static string CreateSuccessResponse(object id, JToken result)
        {
            var response = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["result"] = result ?? JValue.CreateNull(),
                ["id"] = id != null ? JToken.FromObject(id) : JValue.CreateNull()
            };
            return response.ToString(Formatting.None);
        }

        private static string CreateErrorResponse(object id, int code, string message)
        {
            var response = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["error"] = new JObject
                {
                    ["code"] = code,
                    ["message"] = message
                },
                ["id"] = id != null ? JToken.FromObject(id) : JValue.CreateNull()
            };
            return response.ToString(Formatting.None);
        }
    }
}
