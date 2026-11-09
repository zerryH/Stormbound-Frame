"""Build the local public-font package with the assigned app identity."""
import json
from pathlib import Path
import sys
import zipfile

from font_assets import ROOT, write_widget_archive
from local_config import manifest_identity
from private_files import check_widget


output = Path(sys.argv[1]).resolve()
identity = json.loads((ROOT / '.local/install-identity.json').read_text())
write_widget_archive(output, include_private=False, identity=identity)
with zipfile.ZipFile(output, 'a', zipfile.ZIP_DEFLATED) as archive:
    for notice in ('LICENSE', 'THIRD_PARTY_NOTICES.md'):
        archive.write(ROOT / notice, notice)
    if manifest_identity(archive.read('config.xml')) != identity or archive.testzip() is not None:
        raise SystemExit('Archive did not match the selected app identity.')
check_widget(output)
print(f'Validated {identity["app_id"]} ({output.stat().st_size} bytes).')
