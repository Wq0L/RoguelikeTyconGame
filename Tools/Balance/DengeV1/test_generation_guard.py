import tempfile
import unittest
from pathlib import Path
import generation_guard as guard


class GenerationGuardTests(unittest.TestCase):
    def test_inspector_edit_is_rejected_without_overwrite(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            asset = root / 'ScriptableObjects/Balance/CoreStat_DengeV1.asset'
            asset.parent.mkdir(parents=True)
            asset.write_text('value: 10\n', encoding='utf-8')
            manifest = root / 'manifest.json'
            guard.record(root, manifest)
            guard.verify(root, manifest)
            asset.write_text('value: 15\n', encoding='utf-8')
            with self.assertRaises(SystemExit): guard.verify(root, manifest)
            self.assertEqual(asset.read_text(), 'value: 15\n')

    def test_missing_and_untracked_generated_files_are_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            manifest = root / 'manifest.json'
            guard.record(root, manifest)
            asset = root / 'ScriptableObjects/Balance/CoreStat_DengeV1.asset'
            asset.parent.mkdir(parents=True)
            asset.write_text('value: 10\n')
            with self.assertRaises(SystemExit): guard.verify(root, manifest)
            guard.record(root, manifest)
            asset.unlink()
            with self.assertRaises(SystemExit): guard.verify(root, manifest)

    def test_line_endings_do_not_count_as_inspector_edits(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            asset = root / 'ScriptableObjects/Balance/CoreStat_DengeV1.asset'
            asset.parent.mkdir(parents=True)
            asset.write_bytes(b'value: 10\n')
            manifest = root / 'manifest.json'
            guard.record(root, manifest)
            asset.write_bytes(b'value: 10\r\n')
            guard.verify(root, manifest)


if __name__ == '__main__': unittest.main()
