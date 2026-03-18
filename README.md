# AutoCAD MCP Plugin

AI-powered AutoCAD automation via the **Model Context Protocol (MCP)**. Enables Claude and other AI assistants to create, modify, and query AutoCAD drawings through natural language.

> "Draw a floor plan with 3 bedrooms" — and it does.

## Architecture

```
┌─────────────┐     stdio      ┌──────────────────┐     TCP socket      ┌──────────────────┐
│   Claude /   │ ──── MCP ────▶ │  Python MCP      │ ── JSON-RPC 2.0 ──▶│  C# Plugin       │
│   AI Client  │◀──────────────│  Server           │◀────────────────── │  (inside AutoCAD) │
└─────────────┘                └──────────────────┘   localhost:8081    └──────────────────┘
                                                                              │
                                                                       AutoCAD .NET API
```

| Component | Language | Location |
|-----------|----------|----------|
| **AutoCADMCPPlugin.dll** | C# | `src/AutoCADMCPPlugin/` |
| **MCP Server** | Python | `src/mcp_server/` |
| **Bundle Manifest** | XML | `config/AutoCADMCPPlugin.bundle/` |

### How It Works

1. The **C# plugin** loads inside AutoCAD as an addin and starts a TCP socket server on `localhost:8081`
2. The **Python MCP server** connects to the plugin socket and exposes 36 tools via the MCP protocol
3. **Claude** (or any MCP client) calls tools like `create_line`, `create_layer`, etc.
4. Commands are marshaled to AutoCAD's main UI thread via `Application.Idle` (similar to Revit's `ExternalEvent` pattern).

### Thread Safety

AutoCAD's .NET API is single-threaded. The plugin uses `Application.Idle` event + `DocumentLock` to safely execute commands from the socket handler threads on the main thread.

## Features — 36 MCP Tools

### System (5)
| Tool | Description |
|------|-------------|
| `system_status` | Plugin version, AutoCAD version, active document |
| `list_methods` | All available commands |
| `set_system_variable` | Set AutoCAD system variables (DIMTXT, LTSCALE, etc.) |
| `get_system_variable` | Read system variable values |
| `execute_command` | Run raw AutoCAD command strings |

### Drawing Management (4)
| Tool | Description |
|------|-------------|
| `drawing_new` | Create new drawing (optional template) |
| `drawing_open` | Open existing .dwg file |
| `drawing_save` | Save / Save As |
| `drawing_info` | Entity count, layers, file path |

### Entity Creation (9)
| Tool | Description |
|------|-------------|
| `create_line` | Line from start to end point |
| `create_circle` | Circle at center with radius |
| `create_arc` | Arc with center, radius, start/end angle |
| `create_polyline` | Polyline through points (open or closed) |
| `create_rectangle` | Rectangle from two corners |
| `create_ellipse` | Ellipse with major/minor radii |
| `create_text` | Single-line text |
| `create_mtext` | Multi-line text |
| `create_hatch` | Hatch with boundary and pattern |

### Entity Query & Modification (7)
| Tool | Description |
|------|-------------|
| `list_entities` | List entities with layer/type filters |
| `get_entity` | Detailed entity info by handle |
| `erase_entity` | Delete entity |
| `move_entity` | Move entity between points |
| `copy_entity` | Copy entity to new location |
| `rotate_entity` | Rotate around base point |
| `scale_entity` | Scale from base point |
| `mirror_entity` | Mirror across a line |

### Layers (4)
| Tool | Description |
|------|-------------|
| `list_layers` | All layers with properties |
| `create_layer` | New layer with color/linetype |
| `set_current_layer` | Switch active layer |
| `set_layer_properties` | Modify color, freeze, lock, etc. |

### Blocks (2)
| Tool | Description |
|------|-------------|
| `list_blocks` | Block definitions with attributes |
| `insert_block` | Insert with position/rotation/scale/attributes |

### Annotations (2)
| Tool | Description |
|------|-------------|
| `create_linear_dimension` | Horizontal/vertical dimension |
| `create_aligned_dimension` | Aligned dimension |

### View (2)
| Tool | Description |
|------|-------------|
| `zoom_extents` | Fit all entities |
| `zoom_window` | Zoom to rectangular area |

## Supported AutoCAD Versions

| AutoCAD Version | .NET Target | NuGet Package Version |
|----------------|-------------|----------------------|
| **2022, 2023, 2024** | .NET Framework 4.8 (`net48`) | AutoCAD.NET 24.2.x |
| **2025, 2026** | .NET 8 (`net8.0-windows`) | AutoCAD.NET 25.x |

Both targets are built simultaneously. The bundle manifest auto-selects the correct DLL.

## Installation

### Prerequisites

- **AutoCAD 2022–2026** (any edition including LT with .NET support)
- **Visual Studio 2022** or **.NET SDK 8.0+** (for building)
- **Python 3.10+** (for MCP server)
- **Windows** (AutoCAD is Windows-only)

### Step 1: Build the Plugin

```bash
cd autocad-plugin/src/AutoCADMCPPlugin
dotnet build -c Release
```

Output:
- `bin/Release/net48/AutoCADMCPPlugin.dll` — AutoCAD 2022–2024
- `bin/Release/net8.0-windows/AutoCADMCPPlugin.dll` — AutoCAD 2025–2026

### Step 2: Install the Plugin

**Option A: Auto-install (recommended)**

```bash
cd autocad-plugin
install.bat
```

This builds, copies the DLLs to `%APPDATA%\Autodesk\ApplicationPlugins\AutoCADMCPPlugin.bundle\`, and AutoCAD will load it automatically on startup.

**Option B: Manual NETLOAD**

1. Open AutoCAD
2. Type `NETLOAD`
3. Browse to the correct DLL for your version
4. Type `MCPSTART` to start the server

### Step 3: Start the Server in AutoCAD

```
Command: MCPSTART
[MCP] Server started on localhost:8081
```

Other commands:
- `MCPSTOP` — Stop the server
- `MCPSTATUS` — Show connection count

### Step 4: Install the MCP Server

```bash
cd autocad-plugin/src/mcp_server
pip install -r requirements.txt
```

### Step 5: Configure Your MCP Client

Create a `.mcp.json` in your project root (or configure Claude Desktop):

```json
{
  "mcpServers": {
    "autocad-mcp": {
      "command": "python",
      "args": ["<full-path-to>/autocad-plugin/src/mcp_server/server.py"],
      "env": {
        "AUTOCAD_MCP_HOST": "localhost",
        "AUTOCAD_MCP_PORT": "8081"
      }
    }
  }
}
```

For **Claude Desktop**, add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "autocad-mcp": {
      "command": "python",
      "args": ["C:/Users/YOUR_USER/path/to/autocad-plugin/src/mcp_server/server.py"]
    }
  }
}
```

### Step 6: Test

In Claude Code or Claude Desktop:

> "Draw a circle at (50, 50) with radius 25"

Or verify manually:

```python
import socket, json
sock = socket.socket()
sock.connect(('localhost', 8081))
sock.sendall(json.dumps({
    "jsonrpc": "2.0", "method": "system_status", "params": {}, "id": "1"
}).encode() + b"\n")
print(sock.recv(4096).decode())
```

## Uninstall

```bash
cd autocad-plugin
uninstall.bat
```

Or manually delete: `%APPDATA%\Autodesk\ApplicationPlugins\AutoCADMCPPlugin.bundle\`

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `AUTOCAD_MCP_HOST` | `localhost` | Plugin socket host |
| `AUTOCAD_MCP_PORT` | `8081` | Plugin socket port |

## Project Structure

```
autocad-plugin/
├── README.md
├── install.bat                    # Build + deploy to ApplicationPlugins
├── uninstall.bat                  # Remove from ApplicationPlugins
├── config/
│   └── AutoCADMCPPlugin.bundle/
│       └── PackageContents.xml    # AutoCAD auto-load manifest
└── src/
    ├── AutoCADMCPPlugin/          # C# .NET Plugin
    │   ├── AutoCADMCPPlugin.csproj
    │   ├── Core/
    │   │   ├── Plugin.cs          # IExtensionApplication entry point
    │   │   ├── SocketServer.cs    # TCP server (JSON-RPC 2.0)
    │   │   ├── JsonRpcHandler.cs  # Protocol handler
    │   │   ├── IdleActionRunner.cs# Thread marshaling
    │   │   └── CommandRegistry.cs # Command lookup
    │   ├── Commands/
    │   │   ├── EntityCommands.cs          # Line, circle, arc, polyline, etc.
    │   │   ├── EntityModifyCommands.cs    # Move, copy, rotate, scale, mirror
    │   │   ├── LayerCommands.cs           # Layer CRUD
    │   │   ├── BlockCommands.cs           # Block list/insert
    │   │   ├── AnnotationCommands.cs      # Dimensions
    │   │   ├── DrawingCommands.cs         # New, open, save, info
    │   │   ├── ViewCommands.cs            # Zoom
    │   │   ├── SystemCommands.cs          # Status, list methods
    │   │   └── SystemVariableCommand.cs   # Get/set vars, execute command
    │   └── Models/
    │       ├── ICommand.cs        # Command interface
    │       └── CommandResult.cs   # Result type
    └── mcp_server/                # Python MCP Server
        ├── server.py              # 36 MCP tools via FastMCP
        ├── autocad_client.py      # Async TCP client
        └── requirements.txt
```

## Adding New Commands

1. Create a class implementing `ICommand` in `Commands/`
2. Register it in `CommandRegistry.cs`
3. Add the corresponding MCP tool in `server.py`
4. Rebuild and reinstall

Example:

```csharp
// Commands/MyCommand.cs
public class MyCommand : ICommand
{
    public string MethodName => "my_command";

    public CommandResult Execute(JObject parameters)
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        using (EntityHelper.LockDoc())
        using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
        {
            // Your AutoCAD API code here
            tr.Commit();
        }
        return CommandResult.Ok("Done");
    }
}
```

## License

MIT
