# TEX sprite ID → Character/Job Mapping

Source: extracted from FFT Sprite Toolkit's `working/extracted_sprites/` filenames (435 IDs).
Each character/job typically owns 2 consecutive IDs: even = overworld sprite sheet (256×256, 131072 bytes), odd = sheet with dialog portrait pose (256×232, 118784 bytes).

## What we currently cover

- **830-835**: Ramza (Ch1 / Ch23 / Ch4 pairs) — full pipeline shipped

## The gap (per White Knights Vol.4 scope, plus extras the Toolkit exports)

### Story characters (830-915)

| IDs | Character | Notes |
|---|---|---|
| 836-837 | Delita_Ch1 | |
| 838-839 | Delita_Ch23 | |
| 840-841 | Delita_Ch4 | |
| 842-843 | Argath | |
| 844-845 | Zalbaag | |
| 846-847 | Dycedarg | |
| 848-849 | Larg | |
| 850-851 | Goltanna | |
| 852-853 | Ovelia | |
| 854-855 | Orlandeau | We have section mapping |
| 856-857 | Funebris | |
| 858-859 | Reis | We have section mapping |
| 860-861 | Zalmour | |
| 862-863 | Simon | |
| 864-865 | Orran | |
| 866-867 | Gaffgarion | |
| 868-869 | Delacroix | |
| 870-871 | Rapha | We have section mapping |
| 872-873 | Marach | We have section mapping |
| 874-875 | Elmdore | |
| 876-877 | Tietra | |
| 878-879 | Barrington | |
| 880-881 | Agrias | **Pilot candidate** - we have section mapping |
| 882-883 | Beowulf | We have section mapping |
| 884-885 | Wiegraf_Ch1 | |
| 886-887 | Valmafra | |
| 888-889 | Mustadio | We have section mapping |
| 890-891 | Ludovich | |
| 892-893 | Folmarv | |
| 894-895 | Loffrey | |
| 896-897 | Isilud | |
| 898-899 | Cletienne | |
| 900-901 | Wiegraf_Ch23 | |
| 902-903 | Barich | |
| 904 | Alma_Dead | single ID |
| 905-906 | Meliadoul | We have section mapping |
| 907-908 | Alma | |
| 909 | Ajora | single ID |
| 910-911 | Cloud | We have section mapping |
| 912-913 | Zalbaag_Zombie | |
| 914-915 | Agrias | Alternate / Ch4 variant |

### Generic class portraits + overworld (916-927)

| IDs | Class |
|---|---|
| 916-917 | Chemist_Female |
| 918-919 | White_Mage_Female |
| 920-921 | Black_Mage_Male |
| 922-923 | Mystic_Male |
| 926-927 | Dancer_Female |

### Special NPCs (928-979)

| IDs | Character |
|---|---|
| 928-929 | Lettie |
| 930-931 | Belias |
| 932-933 | Zelera |
| 934-935 | Archer_Male |
| 936-937 | Hashmal |
| 938-939 | Altima |
| 940-941 | Black_Mage_Male (variant) |
| 942-943 | Cuchulainn |
| 944-945 | Time_Mage_Female |
| 946-947 | Adrammelech |
| 948-949 | Mystic_Male (variant) |
| 950-951 | Reis_Dragon_Form |
| 952-953 | Altima_Second_Form |
| 954-955 | 10yo_Male |
| 956-957 | 10yo_Female |
| 958-959 | 20yo_Male |
| 960-961 | 20yo_Female |
| 962-963 | 40yo_Male |
| 964-965 | 40yo_Female |
| 966-967 | 60yo_Male |
| 968-969 | 60yo_Female |
| 970-971 | Old_Funeral_Man |
| 972-973 | Old_Funeral_Woman |
| 974-975 | Funeral_Man |
| 976-977 | Funeral_Woman |
| 978-979 | Funeral_Priest |

### Generic job sprites (994-1067) — the biggest visibility gap

Pattern: 4 IDs per job, two male + two female slots. Bard is Male-only, Dancer is Female-only.

| IDs | Job |
|---|---|
| 994-995 | Squire_Female |
| 996-999 | Chemist (M, M, F, F) |
| 1000-1003 | Knight (M, M, F, F) |
| 1004-1007 | Archer |
| 1008-1011 | Monk |
| 1012-1015 | Priest |
| 1016-1019 | Black_Mage |
| 1020-1023 | Time_Mage |
| 1024-1027 | Summoner |
| 1028-1031 | Thief |
| 1032-1035 | Mediator |
| 1036-1039 | Mystic |
| 1040-1043 | Geomancer |
| 1044-1047 | Lancer |
| 1048-1051 | Samurai |
| 1052-1055 | Ninja |
| 1056-1059 | Calculator |
| 1060-1061 | Bard (Male only) |
| 1062-1063 | Dancer (Female only) |
| 1064-1067 | Mime |

## Format facts

- All TEX files are uncompressed RGB555 (no YOX/zlib compression on these IDs)
- Even ID: 131072 bytes = 256×256, contains overworld poses only
- Odd ID: 118784 bytes = 256×232, contains overworld + larger dialog portrait pose at bottom
- Sprite sheets are 4bpp indexed in their original game representation, but the TEX files in g2d.dat store the BAKED RGB555 colors (one specific palette applied per file)
- Recolor via direct RGB555 transform IS valid — confirmed by our existing Ramza dark_knight ↔ vanilla byte diff (8.3% of file changed, scattered pixels matching color transform)
- Source for vanilla baseline: `Reloaded/Mods/original_squire_v2/.../tex_*.bin` (332 vanilla files, range 830-1586)

## Pilot recommendation

**Agrias (880-881 + 914-915)**: 4 TEX files, section mapping already exists at `Data/SectionMappings/Story/Agrias.json`, iconic and high-visibility in cutscenes. Mirror the Ramza pipeline pattern (`AgriasThemes/<theme>/tex_880-915.bin`).
