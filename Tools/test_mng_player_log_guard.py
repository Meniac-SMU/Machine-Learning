import tempfile
import unittest
from pathlib import Path
from mng_player_log_guard import PlayerLogGuard


class PlayerLogGuardTests(unittest.TestCase):
    def test_incremental_split_exception_and_no_duplicate(self):
        with tempfile.TemporaryDirectory() as folder:
            p = Path(folder) / 'Player-28.log'
            guard = PlayerLogGuard(folder)
            p.write_text('normal startup\nIOException: Shar')
            self.assertEqual(guard.scan(), [])
            with p.open('a') as f: f.write('ing violation\nstack trace\n')
            self.assertEqual(guard.scan()[0]['error'], 'IOException: Sharing violation')
            self.assertEqual(guard.scan(), [])

    def test_resume_truncated_log_is_scanned(self):
        with tempfile.TemporaryDirectory() as folder:
            p = Path(folder) / 'Player-0.log'
            p.write_text('old clean log\n' * 20)
            guard = PlayerLogGuard(folder)
            self.assertEqual(guard.scan(), [])
            p.write_text('NullReferenceException: failure\n')
            self.assertEqual(len(guard.scan()), 1)

    def test_benign_warning_does_not_stop(self):
        with tempfile.TemporaryDirectory() as folder:
            (Path(folder) / 'Player-1.log').write_text('Unable to save timers to file\n[WARNING] Initializing optimizer\n')
            self.assertEqual(PlayerLogGuard(folder).scan(), [])
