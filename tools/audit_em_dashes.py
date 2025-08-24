#!/usr/bin/env python3
"""
Zero-tolerance verification script to ensure no em dashes exist anywhere
in the repository code, comments, or documentation.
"""

import os
import sys

def main():
    root_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    violations = []
    ignored_dirs = {'.git', 'bin', 'obj', 'out', 'build', '__pycache__', '.pytest_cache'}

    for current_root, dirs, files in os.walk(root_dir):
        dirs[:] = [d for d in dirs if d not in ignored_dirs]
        for file_name in files:
            file_path = os.path.join(current_root, file_name)
            try:
                with open(file_path, 'r', encoding='utf-8') as fh:
                    for line_idx, line in enumerate(fh, 1):
                        if '\u2014' in line:
                            violations.append((file_path, line_idx, line.strip()))
            except (UnicodeDecodeError, IsADirectoryError, FileNotFoundError):
                pass

    if violations:
        print(f"FAILED: Found {len(violations)} prohibited em dash instance(s):", file=sys.stderr)
        for path, line_no, content in violations:
            rel_path = os.path.relpath(path, root_dir)
            print(f"  {rel_path}:{line_no}: {content}", file=sys.stderr)
        sys.exit(1)

    print("PASSED: Zero prohibited em dashes found across entire repository.")

if __name__ == '__main__':
    main()
