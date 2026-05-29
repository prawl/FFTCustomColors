#!/usr/bin/env python3
"""Given a monster HD BMP and two pose box indices (from detect_bmp_poses numbering), build a
centered, neighbor-free explicit-rect FrameLayout (common cell = max of the two pose bboxes + pad,
centered on each), print the FrameLayout.Rects(...) C# call, and render a 4-frame preview.
Usage: python pick_poses.py <bmp> <swIdx> <nwIdx> <out_preview.png> [pad=4]
"""
import sys, struct, zlib, subprocess, os
from collections import deque
bmp, swIdx, nwIdx, outp = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), sys.argv[4]
pad = int(sys.argv[5]) if len(sys.argv) > 5 else 4
b=open(bmp,'rb').read()
off=struct.unpack('<I',b[10:14])[0]; W=struct.unpack('<i',b[18:22])[0]; H=struct.unpack('<i',b[22:26])[0]
rowb=((W*4+31)//32)*4
def idx(x,y):
    if not(0<=x<W and 0<=y<H):return 0
    sy=H-1-y; byte=b[off+sy*rowb+x//2]
    return (byte>>4)&0xF if x%2==0 else byte&0xF
# Reproduce detect_bmp_poses box detection + ordering exactly.
G=[[idx(x,y) for x in range(W)] for y in range(H)]
vis=[[False]*W for _ in range(H)]; boxes=[]
for y in range(H):
    for x in range(W):
        if G[y][x]==0 or vis[y][x]: continue
        q=deque([(y,x)]); vis[y][x]=True; n=0; x0=x1=x; y0=y1=y
        while q:
            cy,cx=q.popleft(); n+=1; x0=min(x0,cx);x1=max(x1,cx);y0=min(y0,cy);y1=max(y1,cy)
            for dy,dx in((-1,0),(1,0),(0,-1),(0,1)):
                ny,nx=cy+dy,cx+dx
                if 0<=ny<H and 0<=nx<W and not vis[ny][nx] and G[ny][nx]!=0:
                    vis[ny][nx]=True; q.append((ny,nx))
        if n>=300 and (x1-x0)>=24 and (y1-y0)>=24: boxes.append([x0,y0,x1,y1])
boxes.sort(key=lambda bx:(bx[1]//40,bx[0]))
sw=boxes[swIdx]; nw=boxes[nwIdx]
sww,swh=sw[2]-sw[0]+1,sw[3]-sw[1]+1; nww,nwh=nw[2]-nw[0]+1,nw[3]-nw[1]+1
cw=max(sww,nww)+pad; ch=max(swh,nwh)+pad
def rect(bx):
    cx=(bx[0]+bx[2])//2; cy=(bx[1]+bx[3])//2
    return (cx-cw//2, cy-ch//2, cw, ch)
sr=rect(sw); nr=rect(nw)
print(f"FrameLayout.Rects({sr[0]}, {sr[1]}, {sr[2]}, {sr[3]}, {nr[0]}, {nr[1]}, {nr[2]}, {nr[3]})")
print(f"# SW box {swIdx}={sw} NW box {nwIdx}={nw} cell {cw}x{ch}")
subprocess.run([sys.executable, os.path.join(os.path.dirname(__file__),"preview_layout.py"),
    bmp, outp, "rects", str(sr[0]),str(sr[1]),str(sr[2]),str(sr[3]), str(nr[0]),str(nr[1]),str(nr[2]),str(nr[3]), "4"])
