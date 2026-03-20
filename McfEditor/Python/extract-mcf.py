#!/usr/bin/env python3
import argparse
import json
import os
import shutil
import struct
import sys
import zlib
from pathlib import Path

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    print("Missing dependency: Pillow. Install it with: pip install Pillow", file=sys.stderr)
    sys.exit(1)


def load_font():
    try:
        return ImageFont.truetype("Arial", 14)
    except OSError:
        try:
            windows_fonts_folder = os.path.join(os.environ["SystemRoot"], "Fonts")
            return ImageFont.truetype(os.path.join(windows_fonts_folder, "arial.ttf"), 14)
        except Exception:
            return ImageFont.load_default()


def parse_args():
    parser = argparse.ArgumentParser(description="Extract PNG images from an MCF archive.")
    parser.add_argument("filename", help="Path to source .mcf file")
    parser.add_argument("outdir", help="Output working directory")
    parser.add_argument("--parse-idmap", choices=["auto", "yes", "no"], default="auto")
    parser.add_argument("--print-number", choices=["yes", "no"], default="no")
    parser.add_argument("--json-report", dest="json_report", default=None)
    return parser.parse_args()


def parse_idmap_file(idmap_path):
    filename_array = []
    mifid_array = []

    with open(idmap_path, "rb") as idmap_file:
        seek = idmap_file.seek
        read = idmap_file.read

        seek(12)
        data = read(4)
        if data != b"Skr0":
            raise ValueError("Invalid imageidmap.res header")

        seek(24)
        (num_mifids,) = struct.unpack("<I", read(4))
        seek(32)

        for _ in range(num_mifids):
            (path_len,) = struct.unpack("<I", read(4))
            path_len *= 2
            path = read(path_len).decode("utf-16").replace("/", os.sep)
            filename_array.append(path)
            seek(4, 1)

        (num_mifids_2,) = struct.unpack("<I", read(4))

        for _ in range(num_mifids_2):
            (mif_id,) = struct.unpack("<I", read(4))
            mifid_array.append(mif_id - 1)

    return filename_array, mifid_array


def main():
    args = parse_args()

    out_dir = Path(args.outdir)
    unsorted_dir = out_dir / "Unsorted"
    mapped_root = out_dir / "Images"
    unsorted_dir.mkdir(parents=True, exist_ok=True)

    mcf_path = Path(args.filename)
    if not mcf_path.exists():
        raise FileNotFoundError(f"MCF file not found: {mcf_path}")

    mcf_data = mcf_path.read_bytes()

    offset = 1
    (magic,) = struct.unpack_from("<3s", mcf_data, offset)
    if magic != b"MCF":
        raise ValueError("This is not a correct MCF file.")

    parse_idmap_used = False
    filename_array = []
    mifid_array = []
    warnings = []

    idmap_path = mcf_path.parent / "imageidmap.res"
    if args.parse_idmap in ("auto", "yes") and idmap_path.exists():
        filename_array, mifid_array = parse_idmap_file(idmap_path)
        parse_idmap_used = True
    elif args.parse_idmap == "yes" and not idmap_path.exists():
        warnings.append("imageidmap.res requested but not found.")

    offset = 32
    (size_of_toc,) = struct.unpack_from("<I", mcf_data, offset)
    data_start = size_of_toc + 56

    offset = 48
    (num_files,) = struct.unpack_from("<L", mcf_data, offset)

    offset = data_start
    print_number = args.print_number == "yes"
    font = load_font() if print_number else None

    entries = []
    counter_l = 0
    counter_rgba = 0

    for image_id in range(int(num_files)):
        (
            _img_type,
            file_id,
            _always_8,
            zsize,
            _max_pixel_count,
            _always_1,
            _unknown_16,
            width,
            height,
            image_mode,
            _always__1,
        ) = struct.unpack_from("<4sIIIIIIhhhh", mcf_data, offset)

        zlib_data_offset = offset + 36
        zlib_image = mcf_data[zlib_data_offset:zlib_data_offset + zsize]
        zlib_decompress = zlib.decompress(zlib_image)

        if image_mode == 4096:
            im = Image.frombuffer("L", (width, height), zlib_decompress, "raw", "L", 0, 1)
            mode_name = "L"
            counter_l += 1
        elif image_mode == 4356:
            im = Image.frombuffer("RGBA", (width, height), zlib_decompress, "raw", "RGBA", 0, 1)
            mode_name = "RGBA"
            counter_rgba += 1
        else:
            warnings.append(f"Unsupported image mode {image_mode} for image {image_id}; skipped.")
            offset = offset + zsize + 40
            continue

        if print_number:
            draw = ImageDraw.Draw(im)
            draw.text((width / 2, height / 2), f"{image_id}", 255, font=font)

        file_name = f"img_{image_id}.png"
        out_path = unsorted_dir / file_name
        im.save(out_path)

        mapped_path = None
        if parse_idmap_used:
            for idx, mif_id in enumerate(mifid_array):
                if mif_id != image_id:
                    continue
                mapped_path = filename_array[idx]
                mapped_target = mapped_root / mapped_path
                mapped_target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(out_path, mapped_target)
                break

        relative_path = os.path.join("Unsorted", file_name)
        if mapped_path:
            relative_path = os.path.join("Images", mapped_path)

        entries.append(
            {
                "index": image_id,
                "fileName": file_name,
                "width": width,
                "height": height,
                "imageMode": mode_name,
                "offset": int(zlib_data_offset),
                "zsize": int(zsize),
                "extractedPath": str(out_path.resolve()),
                "mappedPath": mapped_path,
                "relativePath": relative_path,
            }
        )

        offset = offset + zsize + 40

    manifest = {
        "sourceFile": str(mcf_path.resolve()),
        "workingDirectory": str(out_dir.resolve()),
        "imageCount": len(entries),
        "parseIdMapUsed": parse_idmap_used,
        "entries": entries,
        "warnings": warnings,
        "counts": {
            "RGBA": counter_rgba,
            "L": counter_l,
        },
    }

    if args.json_report:
        Path(args.json_report).write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    print(f"Extracted {len(entries)} image(s) from {mcf_path.name} into {out_dir}")
    if warnings:
        print(f"Warnings: {len(warnings)}")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(str(exc), file=sys.stderr)
        sys.exit(1)
