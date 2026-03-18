"""
AutoCAD MCP Server

Exposes AutoCAD operations as MCP tools that AI assistants (Claude) can call.
Communicates with the AutoCAD .NET plugin via TCP socket using JSON-RPC 2.0.

Architecture:
    Claude (MCP Client) -> stdio -> This Server -> TCP socket -> AutoCAD Plugin -> AutoCAD API
"""

import os
import logging
from mcp.server.fastmcp import FastMCP
from autocad_client import get_client

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("autocad-mcp")

HOST = os.environ.get("AUTOCAD_MCP_HOST", "localhost")
PORT = int(os.environ.get("AUTOCAD_MCP_PORT", "8081"))

mcp = FastMCP("AutoCAD MCP Server")


# =============================================================================
# Helper
# =============================================================================

async def _call(method: str, params: dict | None = None) -> str:
    """Send a command to AutoCAD and return the result as formatted text."""
    client = await get_client(HOST, PORT)
    result = await client.send_command(method, params)
    import json
    return json.dumps(result, indent=2)


# =============================================================================
# System Tools
# =============================================================================

@mcp.tool()
async def system_status() -> str:
    """Get the AutoCAD plugin status, version, and active document info."""
    return await _call("system_status")


@mcp.tool()
async def list_methods() -> str:
    """List all available methods/commands the AutoCAD plugin supports."""
    return await _call("list_methods")


@mcp.tool()
async def set_system_variable(name: str, value: float | int | str) -> str:
    """Set an AutoCAD system variable (e.g., DIMTXT, LTSCALE, OSMODE). Value can be number or string."""
    return await _call("set_system_variable", {"name": name, "value": value})


@mcp.tool()
async def get_system_variable(name: str) -> str:
    """Get the current value of an AutoCAD system variable."""
    return await _call("get_system_variable", {"name": name})


@mcp.tool()
async def execute_command(command: str) -> str:
    """Execute a raw AutoCAD command string (e.g., 'ZOOM E', 'PURGE'). Use for commands without a dedicated tool."""
    return await _call("execute_command", {"command": command})


# =============================================================================
# Drawing / Document Tools
# =============================================================================

@mcp.tool()
async def drawing_new(template: str = "") -> str:
    """Create a new drawing, optionally from a template file path."""
    return await _call("drawing_new", {"template": template})


@mcp.tool()
async def drawing_open(path: str) -> str:
    """Open an existing .dwg file. Provide the full file path."""
    return await _call("drawing_open", {"path": path})


@mcp.tool()
async def drawing_save(path: str = "") -> str:
    """Save the current drawing. Optionally provide a path for Save As."""
    return await _call("drawing_save", {"path": path})


@mcp.tool()
async def drawing_info() -> str:
    """Get info about the current drawing: name, path, entity count, layers."""
    return await _call("drawing_info")


# =============================================================================
# Entity Creation Tools
# =============================================================================

@mcp.tool()
async def create_line(
    start: list[float],
    end: list[float],
    layer: str = "",
    color: int = -1
) -> str:
    """Draw a line from start [x,y] to end [x,y]. Optionally set layer and color (0-255 ACI)."""
    params = {"start": start, "end": end}
    if layer: params["layer"] = layer
    if color >= 0: params["color"] = color
    return await _call("create_line", params)


@mcp.tool()
async def create_circle(
    center: list[float],
    radius: float,
    layer: str = "",
    color: int = -1
) -> str:
    """Draw a circle at center [x,y] with given radius."""
    params = {"center": center, "radius": radius}
    if layer: params["layer"] = layer
    if color >= 0: params["color"] = color
    return await _call("create_circle", params)


@mcp.tool()
async def create_arc(
    center: list[float],
    radius: float,
    start_angle: float,
    end_angle: float,
    degrees: bool = True,
    layer: str = ""
) -> str:
    """Draw an arc. Angles in degrees by default. Set degrees=false for radians."""
    params = {
        "center": center, "radius": radius,
        "start_angle": start_angle, "end_angle": end_angle,
        "degrees": degrees
    }
    if layer: params["layer"] = layer
    return await _call("create_arc", params)


@mcp.tool()
async def create_polyline(
    points: list[list[float]],
    closed: bool = False,
    layer: str = ""
) -> str:
    """Draw a polyline through a list of points [[x1,y1], [x2,y2], ...]. Set closed=true to close it."""
    params = {"points": points, "closed": closed}
    if layer: params["layer"] = layer
    return await _call("create_polyline", params)


@mcp.tool()
async def create_rectangle(
    corner1: list[float],
    corner2: list[float],
    layer: str = ""
) -> str:
    """Draw a rectangle from corner1 [x,y] to corner2 [x,y]."""
    params = {"corner1": corner1, "corner2": corner2}
    if layer: params["layer"] = layer
    return await _call("create_rectangle", params)


@mcp.tool()
async def create_ellipse(
    center: list[float],
    major_radius: float,
    minor_radius: float,
    layer: str = ""
) -> str:
    """Draw an ellipse at center with major and minor radii."""
    params = {"center": center, "major_radius": major_radius, "minor_radius": minor_radius}
    if layer: params["layer"] = layer
    return await _call("create_ellipse", params)


@mcp.tool()
async def create_text(
    text: str,
    position: list[float],
    height: float = 2.5,
    rotation: float = 0,
    layer: str = ""
) -> str:
    """Place single-line text at position [x,y]. Height in drawing units, rotation in degrees."""
    params = {"text": text, "position": position, "height": height, "rotation": rotation}
    if layer: params["layer"] = layer
    return await _call("create_text", params)


@mcp.tool()
async def create_mtext(
    text: str,
    position: list[float],
    height: float = 2.5,
    width: float = 0,
    layer: str = ""
) -> str:
    """Place multi-line text at position [x,y]. Width=0 means auto-width."""
    params = {"text": text, "position": position, "height": height, "width": width}
    if layer: params["layer"] = layer
    return await _call("create_mtext", params)


@mcp.tool()
async def create_hatch(
    boundary: list[list[float]],
    pattern: str = "ANSI31",
    scale: float = 1.0,
    layer: str = ""
) -> str:
    """Create a hatch inside a boundary defined by points. Pattern examples: ANSI31, SOLID, DOTS."""
    params = {"boundary": boundary, "pattern": pattern, "scale": scale}
    if layer: params["layer"] = layer
    return await _call("create_hatch", params)


# =============================================================================
# Entity Query & Modification Tools
# =============================================================================

@mcp.tool()
async def list_entities(
    layer: str = "",
    type: str = "",
    limit: int = 500
) -> str:
    """List entities in model space. Filter by layer name and/or entity type (Line, Circle, etc.)."""
    params = {"limit": limit}
    if layer: params["layer"] = layer
    if type: params["type"] = type
    return await _call("list_entities", params)


@mcp.tool()
async def get_entity(handle: str) -> str:
    """Get detailed info about a specific entity by its handle ID."""
    return await _call("get_entity", {"handle": handle})


@mcp.tool()
async def erase_entity(handle: str) -> str:
    """Delete an entity by its handle ID."""
    return await _call("erase_entity", {"handle": handle})


@mcp.tool()
async def move_entity(
    handle: str,
    from_point: list[float],
    to_point: list[float]
) -> str:
    """Move an entity from one point to another."""
    return await _call("move_entity", {"handle": handle, "from": from_point, "to": to_point})


@mcp.tool()
async def copy_entity(
    handle: str,
    from_point: list[float],
    to_point: list[float]
) -> str:
    """Copy an entity from one point to another. Returns the new entity's handle."""
    return await _call("copy_entity", {"handle": handle, "from": from_point, "to": to_point})


@mcp.tool()
async def rotate_entity(
    handle: str,
    base_point: list[float],
    angle: float
) -> str:
    """Rotate an entity around a base point by an angle in degrees."""
    return await _call("rotate_entity", {"handle": handle, "base_point": base_point, "angle": angle})


@mcp.tool()
async def scale_entity(
    handle: str,
    base_point: list[float],
    factor: float
) -> str:
    """Scale an entity from a base point by a scale factor."""
    return await _call("scale_entity", {"handle": handle, "base_point": base_point, "factor": factor})


@mcp.tool()
async def mirror_entity(
    handle: str,
    mirror_line_start: list[float],
    mirror_line_end: list[float],
    erase_source: bool = False
) -> str:
    """Mirror an entity across a line. Set erase_source=true to remove the original."""
    return await _call("mirror_entity", {
        "handle": handle,
        "mirror_line_start": mirror_line_start,
        "mirror_line_end": mirror_line_end,
        "erase_source": erase_source
    })


# =============================================================================
# Layer Tools
# =============================================================================

@mcp.tool()
async def list_layers() -> str:
    """List all layers with their properties (color, frozen, locked, current)."""
    return await _call("list_layers")


@mcp.tool()
async def create_layer(
    name: str,
    color: int = 7,
    set_current: bool = False,
    linetype: str = ""
) -> str:
    """Create a new layer with optional color (ACI 0-255) and linetype."""
    params = {"name": name, "color": color, "set_current": set_current}
    if linetype: params["linetype"] = linetype
    return await _call("create_layer", params)


@mcp.tool()
async def set_current_layer(name: str) -> str:
    """Set the active/current layer by name."""
    return await _call("set_current_layer", {"name": name})


@mcp.tool()
async def set_layer_properties(
    name: str,
    color: int = -1,
    is_off: bool | None = None,
    is_frozen: bool | None = None,
    is_locked: bool | None = None
) -> str:
    """Modify layer properties. Only specified parameters are changed."""
    params: dict = {"name": name}
    if color >= 0: params["color"] = color
    if is_off is not None: params["is_off"] = is_off
    if is_frozen is not None: params["is_frozen"] = is_frozen
    if is_locked is not None: params["is_locked"] = is_locked
    return await _call("set_layer_properties", params)


# =============================================================================
# Block Tools
# =============================================================================

@mcp.tool()
async def list_blocks() -> str:
    """List all block definitions in the drawing with their attributes."""
    return await _call("list_blocks")


@mcp.tool()
async def insert_block(
    name: str,
    position: list[float],
    rotation: float = 0,
    scale_x: float = 1.0,
    scale_y: float = 1.0,
    scale_z: float = 1.0,
    layer: str = "",
    attributes: dict | None = None
) -> str:
    """Insert a block by name at position [x,y]. Rotation in degrees. Attributes as {TAG: value}."""
    params: dict = {
        "name": name, "position": position, "rotation": rotation,
        "scale_x": scale_x, "scale_y": scale_y, "scale_z": scale_z
    }
    if layer: params["layer"] = layer
    if attributes: params["attributes"] = attributes
    return await _call("insert_block", params)


# =============================================================================
# Annotation Tools
# =============================================================================

@mcp.tool()
async def create_linear_dimension(
    point1: list[float],
    point2: list[float],
    dimension_line_position: list[float],
    rotation: float = 0,
    text: str = "",
    layer: str = ""
) -> str:
    """Create a linear (horizontal/vertical) dimension between two points."""
    params: dict = {
        "point1": point1, "point2": point2,
        "dimension_line_position": dimension_line_position,
        "rotation": rotation
    }
    if text: params["text"] = text
    if layer: params["layer"] = layer
    return await _call("create_linear_dimension", params)


@mcp.tool()
async def create_aligned_dimension(
    point1: list[float],
    point2: list[float],
    dimension_line_position: list[float],
    text: str = "",
    layer: str = ""
) -> str:
    """Create an aligned dimension between two points (measures along the line between them)."""
    params: dict = {
        "point1": point1, "point2": point2,
        "dimension_line_position": dimension_line_position
    }
    if text: params["text"] = text
    if layer: params["layer"] = layer
    return await _call("create_aligned_dimension", params)


# =============================================================================
# View Tools
# =============================================================================

@mcp.tool()
async def zoom_extents() -> str:
    """Zoom to show all entities in the drawing."""
    return await _call("zoom_extents")


@mcp.tool()
async def zoom_window(min_point: list[float], max_point: list[float]) -> str:
    """Zoom to a specific rectangular area defined by min [x,y] and max [x,y] corners."""
    return await _call("zoom_window", {"min": min_point, "max": max_point})


# =============================================================================
# Entry point
# =============================================================================

if __name__ == "__main__":
    mcp.run(transport="stdio")
