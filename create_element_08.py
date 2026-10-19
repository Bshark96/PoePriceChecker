import struct
import zlib
import os

def process_png(input_path, output_path_left, output_path_right):
    with open(input_path, 'rb') as f:
        data = f.read()

    assert data[:8] == b'\x89PNG\r\n\x1a\n', "Not a valid PNG"
    
    pos = 8
    chunks = []
    while pos < len(data):
        length = struct.unpack('>I', data[pos:pos+4])[0]
        chunk_type = data[pos+4:pos+8]
        chunk_data = data[pos+8:pos+8+length]
        crc = data[pos+8+length:pos+12+length]
        chunks.append((chunk_type, chunk_data))
        pos += 12 + length

    ihdr = [c[1] for c in chunks if c[0] == b'IHDR'][0]
    width, height, bitdepth, colortype, compression, filter_method, interlace = struct.unpack('>IIBBBBB', ihdr)

    idat_data = b''.join([c[1] for c in chunks if c[0] == b'IDAT'])
    decompressed = zlib.decompress(idat_data)

    bpp = 4 if colortype == 6 else 3
    stride = 1 + width * bpp

    prev_row = bytearray(width * bpp)
    raw_rows = []
    for y in range(height):
        row_data = bytearray(decompressed[y * stride + 1 : (y + 1) * stride])
        filter_type = decompressed[y * stride]
        
        row = bytearray(width * bpp)
        if filter_type == 0:
            row = row_data
        elif filter_type == 1:
            for x in range(width * bpp):
                left = row[x - bpp] if x >= bpp else 0
                row[x] = (row_data[x] + left) & 0xff
        elif filter_type == 2:
            for x in range(width * bpp):
                up = prev_row[x]
                row[x] = (row_data[x] + up) & 0xff
        elif filter_type == 3:
            for x in range(width * bpp):
                left = row[x - bpp] if x >= bpp else 0
                up = prev_row[x]
                row[x] = (row_data[x] + ((left + up) >> 1)) & 0xff
        elif filter_type == 4:
            for x in range(width * bpp):
                left = row[x - bpp] if x >= bpp else 0
                up = prev_row[x]
                corner = prev_row[x - bpp] if x >= bpp else 0
                p = left + up - corner
                pa = abs(p - left)
                pb = abs(p - up)
                pc = abs(p - corner)
                if pa <= pb and pa <= pc:
                    pr = left
                elif pb <= pc:
                    pr = up
                else:
                    pr = corner
                row[x] = (row_data[x] + pr) & 0xff
        prev_row = row
        raw_rows.append(row)

    min_x, min_y, max_x, max_y = width, height, 0, 0
    for y in range(height):
        for x in range(width):
            alpha = raw_rows[y][x * bpp + 3] if bpp == 4 else 255
            if alpha > 15:
                if x < min_x: min_x = x
                if x > max_x: max_x = x
                if y < min_y: min_y = y
                if y > max_y: max_y = y

    crop_w = max_x - min_x + 1
    crop_h = max_y - min_y + 1

    cropped_grid = []
    for y in range(min_y, max_y + 1):
        row_pixels = []
        for x in range(min_x, max_x + 1):
            pixel = bytearray(raw_rows[y][x * bpp : (x + 1) * bpp])
            # Make visible body pixels 100% solid fully opaque (alpha = 255)
            if pixel[3] > 30:
                pixel[3] = 255
            row_pixels.append(bytes(pixel))
        cropped_grid.append(row_pixels)

    rot90_grid = []
    for y in range(crop_w):
        new_row = []
        for x in range(crop_h):
            orig_x = y
            orig_y = crop_h - 1 - x
            new_row.append(cropped_grid[orig_y][orig_x])
        rot90_grid.append(new_row)

    rot270_grid = []
    for y in range(crop_w):
        new_row = []
        for x in range(crop_h):
            orig_x = crop_w - 1 - y
            orig_y = x
            new_row.append(cropped_grid[orig_y][orig_x])
        rot270_grid.append(new_row)

    def write_png(out_path, grid, w, h):
        raw_payload = bytearray()
        for y in range(h):
            raw_payload.append(0)
            for x in range(w):
                raw_payload.extend(grid[y][x])
        
        compressed = zlib.compress(bytes(raw_payload))
        
        ihdr_data = struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)
        ihdr_crc = zlib.crc32(b'IHDR' + ihdr_data)
        ihdr_chunk = struct.pack('>I', len(ihdr_data)) + b'IHDR' + ihdr_data + struct.pack('>I', ihdr_crc)
        
        idat_crc = zlib.crc32(b'IDAT' + compressed)
        idat_chunk = struct.pack('>I', len(compressed)) + b'IDAT' + compressed + struct.pack('>I', idat_crc)
        
        iend_crc = zlib.crc32(b'IEND')
        iend_chunk = struct.pack('>I', 0) + b'IEND' + struct.pack('>I', iend_crc)
        
        png_data = b'\x89PNG\r\n\x1a\n' + ihdr_chunk + idat_chunk + iend_chunk
        with open(out_path, 'wb') as out_f:
            out_f.write(png_data)
        print(f"Saved solid opaque {out_path} ({w}x{h})")

    write_png(output_path_left, rot90_grid, crop_h, crop_w)
    write_png(output_path_right, rot270_grid, crop_h, crop_w)

if __name__ == '__main__':
    process_png('VisualAssets/04_straight_backing_bar.png', 'VisualAssets/08_left_endcap_trimmed.png', 'VisualAssets/08_right_endcap_trimmed.png')
