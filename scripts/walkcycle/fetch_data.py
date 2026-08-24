"""Fetch the game's SEQ/SHP animation files from the FFTPatcher repo into the local data dir
used by gen_monster_walkcycle.py. Run once before regenerating; the files are tiny (<10 KB).
"""
import os, urllib.request

BASE = "https://raw.githubusercontent.com/Glain/FFTPatcher/master/"
FILES = {
    "TYPE1.SEQ": "ShishiSpriteEditor/Resources/TYPE1.SEQ",
    "TYPE1.SHP": "ShishiSpriteEditor/Resources/BATTLE/TYPE1/TYPE1.SHP",
    "TYPE2.SEQ": "ShishiSpriteEditor/Resources/TYPE2.SEQ",
    "TYPE2.SHP": "ShishiSpriteEditor/Resources/BATTLE/TYPE2/TYPE2.SHP",
    "MON.SEQ": "ShishiSpriteEditor/Resources/MON.SEQ",
    "MON.SHP": "ShishiSpriteEditor/Resources/MON.SHP",
    "CYOKO.SEQ": "ShishiSpriteEditor/Resources/CYOKO.SEQ",
    "CYOKO.SHP": "ShishiSpriteEditor/Resources/BATTLE/other/CYOKO.SHP",
}

def data_dir():
    d = os.environ.get("FFT_WALKCYCLE_DATA") or os.path.join(
        os.environ.get("TEMP", "/tmp"), "fft_walkcycle_data")
    os.makedirs(d, exist_ok=True)
    return d

if __name__ == "__main__":
    out = data_dir()
    for name, path in FILES.items():
        dest = os.path.join(out, name)
        urllib.request.urlretrieve(BASE + path, dest)
        print("ok", name, os.path.getsize(dest))
    print("data dir:", out)
