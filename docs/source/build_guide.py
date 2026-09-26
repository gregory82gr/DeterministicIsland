"""Builds docs/NEXUS-1_Deterministic_Islands_Engineer_Guide.pdf.

Usage:
    python docs/source/build_guide.py          # uses docs/source/sample_run.txt for console excerpts
    python docs/source/build_guide.py --run    # runs the demo first and refreshes sample_run.txt
"""

import os
import re
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import figures  # noqa: E402
import part1_orientation  # noqa: E402
import part2_core  # noqa: E402
import part3_islands  # noqa: E402
import part4_trust  # noqa: E402
import part5_handson  # noqa: E402
from guide_style import GuideDoc, NextPageTemplate, PageBreak, Story  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
OUTPUT = os.path.join(REPO, "docs", "NEXUS-1_Deterministic_Islands_Engineer_Guide.pdf")
SAMPLE_RUN = os.path.join(HERE, "sample_run.txt")


def refresh_sample_run():
    project = os.path.join(REPO, "DeterministicIsland")
    out = subprocess.run(["dotnet", "run", "--project", project], capture_output=True, text=True, check=True).stdout
    out = re.sub(r"\S*/bin/Debug/net\d+\.\d+/", "./", out)
    with open(SAMPLE_RUN, "w", encoding="utf-8") as f:
        f.write(out)


def main():
    if "--run" in sys.argv:
        refresh_sample_run()
    with open(SAMPLE_RUN, encoding="utf-8") as f:
        run_output = f.read()

    with tempfile.TemporaryDirectory() as fig_dir:
        F = figures.build_all(fig_dir)
        story = Story()
        story.append(NextPageTemplate("content"))
        story.append(PageBreak())
        part1_orientation.build(story, F)
        part2_core.build(story, F)
        part3_islands.build(story, F)
        part4_trust.build(story, F)
        part5_handson.build(story, F, run_output)

        doc = GuideDoc(OUTPUT, title="Deterministic Islands in Practice",
                       author="DeterministicIsland POC",
                       subject="An engineer-to-engineer guide to the NEXUS-1 Deterministic Islands proof of concept",
                       creator="docs/source/build_guide.py")
        doc.multiBuild(story)
    print(f"wrote {OUTPUT}")


if __name__ == "__main__":
    main()
