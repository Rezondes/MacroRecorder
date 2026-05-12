"""Remove automatic Cursor co-author lines from the commit message file (Git commit-msg hook)."""
from __future__ import annotations

import re
import sys
from pathlib import Path


def main() -> None:
    if len(sys.argv) < 2:
        return
    path = Path(sys.argv[1])
    if not path.is_file():
        return
    text = path.read_text(encoding="utf-8")
    cleaned = re.sub(r"(?mi)^Co-authored-by:\s*Cursor\s*<[^>]+>\s*\r?\n?", "", text)
    if cleaned != text:
        path.write_text(cleaned, encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
