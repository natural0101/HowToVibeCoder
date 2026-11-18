"""Render the four hotbar icons from the current exported viewmodel props."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from generate_props_and_weapons import render_hotbar_icons

if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument("--slot", choices=("belt","solder","uzi","shotgun"))
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    render_hotbar_icons(args.slot)
