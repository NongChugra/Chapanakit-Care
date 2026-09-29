"""Check rendered fixture geometry independently of the PDF generator/text test helper."""
import hashlib
import json
import re
import subprocess
from pathlib import Path
import pdfplumber
from PIL import Image, ImageOps, ImageDraw

root = Path(__file__).resolve().parents[3]
folder = root / "tmp/pdfs/report-completion"
results = []
for pdf in sorted(folder.glob("*.pdf")):
    if not (pdf.stem.endswith("-empty") or pdf.stem.endswith("-multipage") or "-sequence-" in pdf.stem):
        continue
    with pdfplumber.open(pdf) as document:
        words = []
        thumbnails = []
        for number, page in enumerate(document.pages, 1):
            assert page.width > page.height
            assert all(c["x0"] >= 17 and c["x1"] <= page.width - 17 and c["top"] >= 17 and c["bottom"] <= page.height - 17 for c in page.chars), (pdf.name, number, "outside printable bounds")
            words.extend(w["text"] for w in page.extract_words(x_tolerance=1, y_tolerance=1))
            if pdf.stem.endswith("-multipage"):
                stream = "".join(c["text"] for c in page.chars)
                # Every member block includes both beneficiaries on its own page.
                ids = re.findall(r"นายกิตติพัฒน์(\d{3})", stream)
                for member in ids:
                    assert f"ผู้รับ{member}คน1" in stream and f"ผู้รับ{member}คน2" in stream, (pdf.name, number, member)
            image_path = folder / f"{pdf.stem}-page-{number}"
            subprocess.run(["pdftoppm", "-f", str(number), "-l", str(number), "-scale-to", "1500", "-png", "-singlefile", str(pdf), str(image_path)], check=True, capture_output=True)
            with Image.open(image_path.with_suffix(".png")) as source:
                thumb = ImageOps.contain(source.convert("RGB"), (600, 425))
                tile = Image.new("RGB", (620, 455), "#dddddd")
                tile.paste(thumb, (10, 20))
                ImageDraw.Draw(tile).text((10, 3), f"{pdf.stem}: page {number}", fill="black")
                thumbnails.append(tile)
        if pdf.stem.endswith("-multipage"):
            required = {"15/08/2569": 80, "31/12/2523": 80}
            if not pdf.stem.startswith("sak"):
                required["16/08/2569"] = 80
                for i in range(1, 81):
                    required[f"1234567890{i:03}"] = 1
            if pdf.stem.startswith(("all", "group")):
                required["12/02/2570"] = 80
            if pdf.stem.startswith("monthly"):
                required["0812345678"] = 80
            for value, expected in required.items():
                assert words.count(value) == expected, (pdf.name, "broken or missing numeric field", value, words.count(value), expected)
        if "-sequence-" in pdf.stem:
            sequence = pdf.stem.split("-sequence-")[1]
            assert words.count(sequence) == 1, (pdf.name, "broken sequence", sequence)
        sheet = Image.new("RGB", (620 * 2, 455 * ((len(thumbnails) + 1) // 2)), "white")
        for i, thumb in enumerate(thumbnails):
            sheet.paste(thumb, ((i % 2) * 620, (i // 2) * 455))
        sheet.save(folder / f"{pdf.stem}-overview.png")
        results.append({"file": pdf.name, "pages": len(document.pages), "sha256": hashlib.sha256(pdf.read_bytes()).hexdigest(), "bounds": "pass", "numericFields": "pass" if pdf.stem.endswith("-multipage") or "-sequence-" in pdf.stem else "not-applicable", "memberBlocks": "pass"})
out = Path(__file__).with_name("pdf-geometry.json")
out.write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
print(json.dumps(results, indent=2))
