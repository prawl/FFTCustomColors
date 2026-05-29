#!/usr/bin/env python3
"""Crop a row of FrameLayout cells from a monster HD BMP and render them side-by-side, each
labeled with its column index, so a human can confirm which column holds which pose.
Usage: python crop_cells.py <hd.bmp> <out.png> <offsetX> <offsetY> <frameW> <frameH> <ncols> [scale=3]
"""
import sys, struct, zlib
bmp, outp = sys.argv[1], sys.argv[2]
ox, oy, fw, fh, ncols = map(int, sys.argv[3:8])
scale = int(sys.argv[8]) if len(sys.argv) > 8 else 3
b = open(bmp, 'rb').read()
off = struct.unpack('<I', b[10:14])[0]
W = struct.unpack('<i', b[18:22])[0]; H = struct.unpack('<i', b[22:26])[0]
dib = struct.unpack('<I', b[14:18])[0]; paloff = 14 + dib
emb = [(b[paloff+i*4+2], b[paloff+i*4+1], b[paloff+i*4]) for i in range(16)]
rowb = ((W*4+31)//32)*4
def idx(x, y):
    sy = H-1-y
    byte = b[off + sy*rowb + x//2]
    return (byte>>4)&0xF if x%2==0 else byte&0xF
FONT = {'0':["111","101","101","101","111"],'1':["010","110","010","010","111"],'2':["111","001","111","100","111"],
        '3':["111","001","111","001","111"],'4':["101","101","111","001","001"],'5':["111","100","111","001","111"],
        '6':["111","100","111","101","111"],'7':["111","001","001","001","001"],'8':["111","101","111","101","111"],'9':["111","101","111","001","111"]}
gap = 8
cellW = fw*scale; cellH = fh*scale + 14
totW = ncols*(cellW+gap)+gap; totH = cellH+gap
buf = [[(20,20,28,255)]*totW for _ in range(totH)]
def setp(x,y,c):
    if 0<=x<totW and 0<=y<totH: buf[y][x]=c
for col in range(ncols):
    cx0 = gap + col*(cellW+gap)
    # checkerboard bg for transparency
    for yy in range(fh*scale):
        for xx in range(cellW):
            setp(cx0+xx, 14+yy, (40,40,48,255) if ((xx//8+yy//8)%2==0) else (60,60,68,255))
    for py in range(fh):
        for px in range(fw):
            sx, sy = ox+col*fw+px, oy+py
            if 0<=sx<W and 0<=sy<H:
                k = idx(sx,sy)
                if k==0: continue
                r,g,bb = emb[k]
                for dy in range(scale):
                    for dx in range(scale):
                        setp(cx0+px*scale+dx, 14+py*scale+dy, (r,g,bb,255))
    # column label
    for ci,ch in enumerate(f"col{col}"):
        gx = cx0+2+ci*4
        for ry,bits in enumerate(FONT.get(ch,["000"]*5) if ch.isdigit() else [["000"]]*5):
            pass
    lbl = str(col)
    for ci,ch in enumerate(lbl):
        for ry,bits in enumerate(FONT[ch]):
            for rx,bit in enumerate(bits):
                if bit=='1': setp(cx0+2+ci*4+rx, 2+ry, (255,255,0,255))
def chunk(t,da):
    c=t+da; return struct.pack('>I',len(da))+c+struct.pack('>I',zlib.crc32(c)&0xffffffff)
raw=bytearray()
for y in range(totH):
    raw.append(0)
    for x in range(totW):
        raw+=bytes(buf[y][x])
open(outp,'wb').write(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',totW,totH,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(bytes(raw),9))+chunk(b'IEND',b''))
print(f"{outp}: {ncols} cols of {fw}x{fh} from ({ox},{oy})")
