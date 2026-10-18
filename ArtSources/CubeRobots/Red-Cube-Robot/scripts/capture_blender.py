import asyncio,base64,os,tomllib
from pathlib import Path
from datetime import timedelta
from mcp import ClientSession,StdioServerParameters
from mcp.client.stdio import stdio_client
async def main():
    conf=tomllib.loads((Path.home()/'.codex/config.toml').read_text(encoding='utf-8-sig'))['mcp_servers']['blender_official']
    params=StdioServerParameters(command=conf['command'],args=conf['args'],env={**os.environ,**conf['env']})
    async with stdio_client(params) as (r,w):
        async with ClientSession(r,w,read_timeout_seconds=timedelta(seconds=60)) as session:
            await session.initialize()
            out=await session.call_tool('get_screenshot_of_window_as_image',{})
            for c in out.content:
                if c.type=='image':
                    path=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot\previews\blender-window.png');path.write_bytes(base64.b64decode(c.data));print(path)
                elif c.type=='text':print(c.text)
asyncio.run(main())
