import struct, zlib, math, os

def write_png(filepath, width, height, pixels):
    raw_data = bytearray()
    for y in range(height):
        raw_data.append(0)  # filter type 0
        start = y * width * 4
        raw_data.extend(pixels[start:start + width * 4])
    compressed = zlib.compress(raw_data)
    png = bytearray(b'\x89PNG\r\n\x1a\n')
    ihdr = struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)
    png.extend(struct.pack('>I', len(ihdr)) + b'IHDR' + ihdr + struct.pack('>I', zlib.crc32(b'IHDR' + ihdr)))
    png.extend(struct.pack('>I', len(compressed)) + b'IDAT' + compressed + struct.pack('>I', zlib.crc32(b'IDAT' + compressed)))
    png.extend(struct.pack('>I', 0) + b'IEND' + struct.pack('>I', zlib.crc32(b'IEND')))
    with open(filepath, 'wb') as f:
        f.write(png)

def draw_left_ornament(width, height):
    pix = bytearray(width * height * 4)
    cy = height / 2.0

    for y in range(height):
        for x in range(width):
            in_shape = False
            # Recolor to dark copper-amber background tone (#b84e16, #d46320, #f07e32)
            r_col, g_col, b_col = 192, 78, 22

            # Left vertical border bar
            if x <= 3 and 2 <= y <= height - 3:
                in_shape = True
                r_col, g_col, b_col = 220, 100, 32

            # Horizontal Center Spine Line running across full width to mid
            elif abs(y - cy) <= 1.2:
                in_shape = True
                if abs(y - cy) <= 0.6:
                    r_col, g_col, b_col = 240, 120, 42  # Spine highlight core

            # Acanthus Leaf / Palmette Filigree Motif (x <= 38)
            elif x <= 38:
                dy = y - cy
                abs_dy = abs(dy)
                # Upper & Lower Acanthus Leaf Curves
                leaf_outer_y = 6.0 + (x - 3) * 0.45
                if 4.0 <= abs_dy <= 22.0:
                    # Upper leaf path
                    if dy < 0 and abs(abs_dy - leaf_outer_y) <= 2.2:
                        in_shape = True
                    # Lower leaf path
                    elif dy > 0 and abs(abs_dy - leaf_outer_y) <= 2.2:
                        in_shape = True

                # Secondary inner foliage curls
                inner_leaf_y = 10.0 + (x - 8) * 0.3
                if 6.0 <= abs_dy <= 18.0 and x >= 8:
                    if abs(abs_dy - inner_leaf_y) <= 1.8:
                        in_shape = True
                        r_col, g_col, b_col = 210, 90, 28

                # Center Rosette / Floret Cluster (around x=14, y=cy)
                dist_rosette = math.sqrt((x - 14)**2 + (dy)**2)
                if dist_rosette <= 5.2:
                    in_shape = True
                    r_col, g_col, b_col = 230, 110, 36

            if in_shape:
                # Additive alpha fade out to mid (fading from 100% on left to 0% at width)
                progress = x / float(width)
                fade = max(0.0, 1.0 - progress)
                # Smooth non-linear fade curve
                fade_curve = math.pow(fade, 1.5)
                alpha = int(240 * fade_curve)

                idx = (y * width + x) * 4
                pix[idx] = r_col
                pix[idx + 1] = g_col
                pix[idx + 2] = b_col
                pix[idx + 3] = alpha

    return pix

def mirror_pixels_horizontal(width, height, pixels):
    mirrored = bytearray(len(pixels))
    for y in range(height):
        for x in range(width):
            src_x = width - 1 - x
            src_idx = (y * width + src_x) * 4
            dst_idx = (y * width + x) * 4
            mirrored[dst_idx:dst_idx+4] = pixels[src_idx:src_idx+4]
    return mirrored

def draw_center_diamond(size=36):
    pix = bytearray(size * size * 4)
    cx, cy = size / 2.0, size / 2.0
    for y in range(size):
        for x in range(size):
            dx = abs(x - cx)
            dy = abs(y - cy)
            if (dx / 14.0 + dy / 14.0) <= 1.0:
                # Faceted shading
                if x < cx and y < cy:
                    r, g, b = 255, 248, 214
                elif x >= cx and y < cy:
                    r, g, b = 240, 203, 114
                elif x < cx and y >= cy:
                    r, g, b = 210, 160, 70
                else:
                    r, g, b = 160, 110, 40
                
                idx = (y * size + x) * 4
                pix[idx] = r
                pix[idx+1] = g
                pix[idx+2] = b
                pix[idx+3] = 255
    return pix

def draw_backing_bar(width=200, height=32):
    pix = bytearray(width * height * 4)
    for y in range(height):
        for x in range(width):
            in_bar = False
            r, g, b = 240, 203, 114
            if 2 <= y <= 5 or height - 6 <= y <= height - 3:
                in_bar = True
                if y in (2, height - 6):
                    r, g, b = 255, 248, 214
            elif 6 <= y <= height - 7:
                in_bar = True
                r, g, b = 24, 18, 12
            
            if in_bar:
                # Soft fade at ends
                fade = max(0.0, min(1.0, min(x / 20.0, (width - x) / 20.0)))
                alpha = int(255 * fade)
                idx = (y * width + x) * 4
                pix[idx] = r
                pix[idx+1] = g
                pix[idx+2] = b
                pix[idx+3] = alpha
    return pix

def draw_end_cap(width=16, height=56):
    pix = bytearray(width * height * 4)
    for y in range(height):
        for x in range(width):
            if 1 <= x <= width - 2 and 1 <= y <= height - 2:
                r, g, b = 240, 203, 114
                if x in (1, 2) or y in (1, 2):
                    r, g, b = 255, 248, 214
                elif x >= width - 3 or y >= height - 3:
                    r, g, b = 160, 110, 40
                idx = (y * width + x) * 4
                pix[idx] = r
                pix[idx+1] = g
                pix[idx+2] = b
                pix[idx+3] = 255
    return pix

def main():
    target_dir = "/VisualAssets"
    os.makedirs(target_dir, exist_ok=True)

    w, h = 160, 56
    left_pix = draw_left_ornament(w, h)
    write_png(os.path.join(target_dir, "01_left_ornament.png"), w, h, left_pix)

    right_pix = mirror_pixels_horizontal(w, h, left_pix)
    write_png(os.path.join(target_dir, "02_right_ornament.png"), w, h, right_pix)

    diamond_pix = draw_center_diamond(36)
    write_png(os.path.join(target_dir, "03_center_diamond_with_mount.png"), 36, 36, diamond_pix)

    bar_pix = draw_backing_bar(200, 32)
    write_png(os.path.join(target_dir, "04_straight_backing_bar.png"), 200, 32, bar_pix)

    cap_pix = draw_end_cap(16, 56)
    write_png(os.path.join(target_dir, "05_vertical_end_cap.png"), 16, 56, cap_pix)

    print("Successfully generated all visual assets in /VisualAssets/")

if __name__ == "__main__":
    main()
