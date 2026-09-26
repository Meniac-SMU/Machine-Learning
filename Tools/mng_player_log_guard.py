"""Incremental Unity Player failure detection; requests the existing safe-save path."""
import re
from pathlib import Path


class PlayerLogGuard:
    failure = re.compile(r'^(?:[\w.]*Exception:|Assertion failed|\[MNG FATAL\])', re.MULTILINE)

    def __init__(self, directory):
        self.directory = Path(directory)
        self.offsets = {}
        self.pending = {}

    def scan(self):
        failures = []
        for path in self.directory.glob('Player*.log'):
            offset = self.offsets.get(path, 0)
            if path.stat().st_size < offset:
                offset = 0
                self.pending.pop(path, None)
            with path.open('rb') as handle:
                handle.seek(offset)
                data = self.pending.get(path, b'') + handle.read()
                self.offsets[path] = handle.tell()
            complete, _, tail = data.rpartition(b'\n')
            self.pending[path] = tail if b'\n' in data else data
            for line in complete.decode('utf-8', errors='replace').splitlines():
                if self.failure.match(line):
                    failures.append(dict(path=str(path), error=line))
        return failures
