import ast
import ctypes
import json
import logging
import os
from pathlib import Path
import tempfile
import time
import unittest
from unittest.mock import patch

# Exercise the actual helper without importing or initializing the PPO runtime.
tree = ast.parse(Path(__file__).with_name('mng_v2_formal_learn.py').read_text())
helper = next(n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name == 'write_progress')
scope = dict(json=json, os=os, time=time, logging=logging)
exec(compile(ast.Module(body=[helper], type_ignores=[]), '<progress helper>', 'exec'), scope)
write_progress = scope['write_progress']


class ProgressTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / 'progress.json'
        self.path.write_text('{"step": 1}')

    def test_transient_collision_recovers(self):
        original = os.replace
        calls = []
        def replace(a, b):
            calls.append(1)
            if len(calls) < 3:
                raise PermissionError('reader holds destination')
            return original(a, b)
        with patch.object(os, 'replace', side_effect=replace):
            self.assertTrue(write_progress(self.path, {'step': 2}))
        self.assertEqual(json.loads(self.path.read_text()), {'step': 2})
        self.assertEqual(len(calls), 3)

    def test_other_io_failure_propagates(self):
        with patch.object(os, 'replace', side_effect=OSError('disk failure')):
            with self.assertRaises(OSError):
                write_progress(self.path, {'step': 2})

    @unittest.skipUnless(os.name == 'nt', 'Windows file sharing regression')
    def test_real_windows_reader_lock_is_nonfatal_and_next_update_recovers(self):
        from ctypes import wintypes
        create = ctypes.windll.kernel32.CreateFileW
        create.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD, ctypes.c_void_p,
                           wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
        create.restype = wintypes.HANDLE
        close = ctypes.windll.kernel32.CloseHandle
        close.argtypes = [wintypes.HANDLE]
        handle = create(str(self.path), 0x80000000, 1, None, 3, 0, None)
        self.assertNotEqual(handle, ctypes.c_void_p(-1).value)
        try:
            self.assertFalse(write_progress(self.path, {'step': 2}))
            self.assertEqual(json.loads(self.path.read_text()), {'step': 1})
            self.assertEqual(json.loads(self.path.with_suffix('.tmp').read_text()), {'step': 2})
        finally:
            close(handle)
        self.assertTrue(write_progress(self.path, {'step': 3}))
        self.assertEqual(json.loads(self.path.read_text()), {'step': 3})


if __name__ == '__main__':
    unittest.main()
