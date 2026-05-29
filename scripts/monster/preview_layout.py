#!/usr/bin/env python3
"""Render the 4 carousel frames (SW, NW, NE, SE) exactly as SpriteSheetExtractor would.
NE=mirror(NW), SE=mirror(SW). Out-of-range reads render transparent (matches the extractor).
Two modes:
  column: python preview_layout.py <bmp> <out> col <fw> <fh> <swCol> <nwCol> <row> <offX> <offY> [scale]
  rects : python preview_layout.py <bmp> <out> rects <swX> <swY> <swW> <swH> <nwX> <nwY> <nwW> <nwH> [scale]
"""
import sys, struct, zlib
bmp, outp, mode = sys.argv[1], sys.argv[2], sys.argv[3]
if mode == "col":
    fw,fh,swCol,nwCol,row,ox,oy = map(int, sys.argv[4:11])
    scale = int(sys.argv[11]) if len(sys.argv) > 11 else 3
    swBox=(ox+swCol*fw, oy+row*fh, fw, fh); nwBox=(ox+nwCol*fw, oy+row*fh, fw, fh)
else:
    sx,sy,sw,sh,nx,ny,nw,nh = map(int, sys.argv[4:12])
    scale = int(sys.argv[12]) if len(sys.argv) > 12 else 3
    swBox=(sx,sy,sw,sh); nwBox=(nx,ny,nw,nh)
b = open(bmp,'rb').read()
off = struct.unpack('<I', b[10:14])[0]
W = struct.unpack('<i', b[18:22])[0]; H = struct.unpack('<i', b[22:26])[0]
dib = struct.unpack('<I', b[14:18])[0]; paloff = 14+dib
emb = [(b[paloff+i*4+2],b[paloff+i*4+1],b[paloff+i*4]) for i in range(16)]
rowb = ((W*4+31)//32)*4
def idx(x,y):
    if not (0<=x<W and 0<=y<H): return 0
    syy=H-1-y; byte=b[off+syy*rowb+x//2]
    return (byte>>4)&0xF if x%2==0 else byte&0xF
def crop(box, mirror):
    x0,y0,w,h = box
    cell=[[None]*w for _ in range(h)]
    for py in range(h):
        for px in range(w):
            k = idx(x0+px, y0+py)
            dx = (w-1-px) if mirror else px
            cell[py][dx] = None if k==0 else emb[k]
    return (w,h,cell)
frames=[("SW",crop(swBox,False)),("NW",crop(nwBox,False)),("NE",crop(nwBox,True)),("SE",crop(swBox,True))]
FONT={'0':["111","101","101","101","111"],'1':["010","110","010","010","111"],'2':["111","001","111","100","111"],'3':["111","001","111","001","111"],'4':["101","101","111","001","001"],'5':["111","100","111","001","111"],'6':["111","100","111","101","111"],'7':["111","001","001","001","001"],'8':["111","101","111","101","111"],'9':["111","101","111","001","111"],'S':["111","100","111","001","111"],'W':["101","101","101","111","111"],'N':["101","111","111","111","101"],'E':["111","100","111","100","111"]}
maxw=max(f[1][0] for f in frames); maxh=max(f[1][1] for f in frames)
gap=10; cw=maxw*scale; ch=maxh*scale+14; totW=4*(cw+gap)+gap; totH=ch+gap
buf=[[(18,18,24,255)]*totW for _ in range(totH)]
def setp(x,y,c):
    if 0<=x<totW and 0<=y<totH: buf[y][x]=c
for fi,(name,(w,h,cell)) in enumerate(frames):
    cx0=gap+fi*(cw+gap)
    for yy in range(maxh*scale):
        for xx in range(cw):
            setp(cx0+xx,14+yy,(44,44,52,255) if ((xx//8+yy//8)%2==0) else (64,64,72,255))
    for py in range(h):
        for px in range(w):
            c=cell[py][px]
            if c:
                for dy in range(scale):
                    for dx in range(scale):
                        setp(cx0+px*scale+dx,14+py*scale+dy,(c[0],c[1],c[2],255))
    midx=cx0+(w*scale)//2
    for yy in range(0,h*scale,4): setp(midx,14+yy,(255,80,80,255))
    for ci,chh in enumerate(name):
        for ry,bits in enumerate(FONT[chh]):
            for rx,bit in enumerate(bits):
                if bit=='1': setp(cx0+2+ci*4+rx,2+ry,(255,255,0,255))
def chunk(t,da):
    c=t+da; return struct.pack('>I',len(da))+c+struct.pack('>I',zlib.crc32(c)&0xffffffff)
raw=bytearray()
for y in range(totH):
    raw.append(0)
    for x in range(totW): raw+=bytes(buf[y][x])
open(outp,'wb').write(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',totW,totH,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(bytes(raw),9))+chunk(b'IEND',b''))
print(f"{outp}: SW{swBox} NW{nwBox}")
