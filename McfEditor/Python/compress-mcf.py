#!/usr/bin/env python3
import argparse
import json
import struct
import sys
import zlib
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("Missing dependency: Pillow. Install it with: pip install Pillow", file=sys.stderr)
    sys.exit(1)


def parse_args():
    parser = argparse.ArgumentParser(description="Rebuild an MCF archive from extracted PNG files.")
    parser.add_argument("original_file", help="Original MCF file")
    parser.add_argument("new_file", help="Output rebuilt MCF file")
    parser.add_argument("imagesdir", help="Directory containing img_<id>.png files")
    parser.add_argument("--json-report", dest="json_report", default=None)
    return parser.parse_args()


def main():
    args = parse_args()

    images_dir = Path(args.imagesdir)
    if not images_dir.exists():
        raise FileNotFoundError(f"Images folder does not exist: {images_dir}")

    data = Path(args.original_file).read_bytes()
    offset = 32
    (size_of_toc,) = struct.unpack_from("<I", data, offset)

    offset = 48
    (num_files,) = struct.unpack_from("<I", data, offset)

    offset_data_start = size_of_toc + 56
    offset_original = offset_data_start
    offset_new = offset_data_start

    original_header = data[0:48]

    struct_toc = bytes()
    struct_data = bytes()
    warnings = []

    for image_id in range(int(num_files)):
        (
            _original_type,
            _original_file_id,
            _original_always_8,
            original_zsize,
            _original_max_pixel_count,
            _original_always_1,
            _original_hash1,
            _original_width,
            _original_height,
            _original_image_mode,
            _original_always__1,
        ) = struct.unpack_from("<4sIIIIIIhhhh", data, offset_original)

        png_path = images_dir / f"img_{image_id}.png"
        if not png_path.exists():
            raise FileNotFoundError(f"Missing PNG file: {png_path}")

        im = Image.open(png_path)
        image_bytes = im.tobytes("raw")
        image_zlib = zlib.compress(image_bytes, 9)

        img_type = b"IMG "
        file_id = image_id + 1
        always_8 = 8
        zsize = len(image_zlib)
        always_1 = 1
        width = im.size[0]
        height = im.size[1]

        if im.mode == "L":
            image_mode = 4096
            max_pixel_count = width * height
        elif im.mode == "RGBA":
            image_mode = 4356
            max_pixel_count = width * height * 4
        else:
            raise ValueError(
                f"Invalid image mode {im.mode} in {png_path.name}. Only L and RGBA are supported."
            )

        always__1 = 1

        mod = zsize % 4
        if mod == 3:
            zsize += 1
            image_zlib += b"\x00"
        elif mod == 2:
            zsize += 2
            image_zlib += b"\x00\x00"
        elif mod == 1:
            zsize += 3
            image_zlib += b"\x00\x00\x00"

        header_part1 = struct.pack("<4sIIIII", img_type, file_id, always_8, zsize, max_pixel_count, always_1)
        hash_1 = zlib.crc32(header_part1)
        hash_2 = zlib.crc32(struct.pack("<hhhh", width, height, image_mode, always__1) + image_zlib)

        struct_data += (
            struct.pack(
                "<4sIiiiiIhhhh",
                img_type,
                file_id,
                always_8,
                zsize,
                max_pixel_count,
                always_1,
                hash_1,
                width,
                height,
                image_mode,
                always__1,
            )
            + image_zlib
            + struct.pack("<I", hash_2)
        )

        struct_toc += struct.pack("<4sIII", img_type, file_id, offset_new, zsize + 40)

        offset_original = offset_original + original_zsize + 40
        offset_new = offset_new + zsize + 40

    toc = struct.pack("<I", num_files) + struct_toc
    toc_checksum = struct.pack("<I", zlib.crc32(toc))

    output_path = Path(args.new_file)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_bytes(original_header + toc + toc_checksum + struct_data)

    report = {
        "success": True,
        "originalFile": str(Path(args.original_file).resolve()),
        "outputFile": str(output_path.resolve()),
        "imagesDirectory": str(images_dir.resolve()),
        "imageCount": int(num_files),
        "warnings": warnings,
    }

    if args.json_report:
        Path(args.json_report).write_text(json.dumps(report, indent=2), encoding="utf-8")

    print(f"Rebuilt {num_files} image(s) into {output_path}")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(str(exc), file=sys.stderr)
        sys.exit(1)
