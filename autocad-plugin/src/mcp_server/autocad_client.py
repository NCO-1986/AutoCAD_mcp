"""
TCP client that connects to the AutoCAD MCP Plugin's socket server.
Implements JSON-RPC 2.0 protocol for sending commands and receiving results.
"""

import asyncio
import json
import time
import logging

logger = logging.getLogger("autocad-mcp")

DEFAULT_HOST = "localhost"
DEFAULT_PORT = 8081
TIMEOUT = 30.0


class AutoCADClient:
    """Async TCP client for communicating with the AutoCAD MCP Plugin."""

    def __init__(self, host: str = DEFAULT_HOST, port: int = DEFAULT_PORT):
        self.host = host
        self.port = port
        self._reader: asyncio.StreamReader | None = None
        self._writer: asyncio.StreamWriter | None = None
        self._lock = asyncio.Lock()
        self._request_id = 0

    async def connect(self) -> None:
        """Establish TCP connection to the AutoCAD plugin."""
        self._reader, self._writer = await asyncio.wait_for(
            asyncio.open_connection(self.host, self.port),
            timeout=5.0
        )
        logger.info(f"Connected to AutoCAD plugin at {self.host}:{self.port}")

    async def disconnect(self) -> None:
        """Close the TCP connection."""
        if self._writer:
            try:
                self._writer.close()
                await self._writer.wait_closed()
            except Exception:
                pass
        self._reader = None
        self._writer = None

    async def send_command(self, method: str, params: dict | None = None) -> dict:
        """
        Send a JSON-RPC 2.0 request and wait for the response.
        Automatically connects if not already connected.
        Serializes access via lock to prevent interleaved messages.
        """
        async with self._lock:
            # Auto-connect if needed
            if self._writer is None or self._writer.is_closing():
                await self.connect()

            self._request_id += 1
            request = {
                "jsonrpc": "2.0",
                "method": method,
                "params": params or {},
                "id": str(self._request_id)
            }

            request_json = json.dumps(request) + "\n"

            try:
                self._writer.write(request_json.encode("utf-8"))
                await self._writer.drain()

                # Read response (newline-delimited JSON)
                response_line = await asyncio.wait_for(
                    self._reader.readline(),
                    timeout=TIMEOUT
                )

                if not response_line:
                    raise ConnectionError("Connection closed by AutoCAD plugin")

                response = json.loads(response_line.decode("utf-8"))

                if "error" in response:
                    error = response["error"]
                    raise RuntimeError(f"AutoCAD error ({error.get('code', '?')}): {error.get('message', 'Unknown')}")

                return response.get("result", {})

            except asyncio.TimeoutError:
                await self.disconnect()
                raise TimeoutError(
                    f"AutoCAD did not respond within {TIMEOUT}s. "
                    "Check that AutoCAD is not in a modal state."
                )
            except (ConnectionError, BrokenPipeError, OSError) as e:
                await self.disconnect()
                raise ConnectionError(
                    f"Lost connection to AutoCAD plugin: {e}. "
                    "Ensure the plugin is running (MCPSTART command)."
                )


# Singleton client instance
_client: AutoCADClient | None = None


async def get_client(host: str = DEFAULT_HOST, port: int = DEFAULT_PORT) -> AutoCADClient:
    """Get or create the singleton AutoCAD client."""
    global _client
    if _client is None:
        _client = AutoCADClient(host, port)
    return _client
