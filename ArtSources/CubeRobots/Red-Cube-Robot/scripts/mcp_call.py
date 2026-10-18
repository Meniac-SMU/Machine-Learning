import asyncio, json, os, sys, tomllib
from pathlib import Path
from datetime import timedelta
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

async def main():
    conf=tomllib.loads((Path.home()/'.codex/config.toml').read_text(encoding='utf-8-sig'))['mcp_servers']['blender_official']
    params=StdioServerParameters(command=conf['command'],args=conf['args'],env={**os.environ,**conf['env']})
    code=Path(sys.argv[1]).read_text(encoding='utf-8')
    async with stdio_client(params) as (r,w):
        async with ClientSession(r,w,read_timeout_seconds=timedelta(seconds=300)) as s:
            await s.initialize()
            out=await s.call_tool('execute_blender_code',{'code':code})
            data=out.structuredContent or [c.text for c in out.content if c.type=='text']
            print(json.dumps(data,ensure_ascii=False,indent=2))
            if out.isError or (isinstance(data,dict) and data.get('status')=='error'): sys.exit(1)
asyncio.run(main())
