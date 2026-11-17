"""Rebuild the embedded 40x32 pixel cells with Python and Pillow."""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
POSES = "walk idle fall held land sit sleep jump faint".split()
# Shadow, midtone, fleece, highlight and accessory
STYLES = [
    ("#ad9190", "#d8c6ae", "#fff0d2", "#fffbea", "classic"),
    ("#895d86", "#bb89aa", "#f1b7cd", "#ffe2e6", "flower"),
    ("#776494", "#ad98c6", "#d8c7ee", "#f3e7ff", "nightcap"),
    ("#477f78", "#7aaf99", "#b4ddbb", "#e4f4cb", "scarf"),
    ("#423c52", "#665c72", "#8c8095", "#b5aabe", "horns"),
]
INK, SKIN, CHEEK = "#342e40", "#675263", "#f19e9b"

FLEECE = [
    "       iii     iii       ",
    "     iihhhiiiiiihhii     ",
    "   iihhwwwwhhhwwwwhhii   ",
    "  ihhwwwwwwwwwwwwwwwwii  ",
    " ihwwwwwwwwwwwwwwwwwwwmi ",
    " ihwwwwwwwwwwwwwwwwwwmmi ",
    "ihwwwwwwwwwwwwwwwwwwwwwmi",
    "iwwwwwwwwwwwwwwwwwwwwwwmi",
    "imwwwwwwwwwwwwwwwwwwwwmmi",
    " imwwwwwwwwwwwwwwwwwwwmi ",
    " ismwwwwwwwwwwwwwwwwmmsi ",
    "  ismwwwwmmwwwmmwwwmmsi  ",
    "   issmmmsmmmmssmmmsii   ",
    "     iisssiiiisssii      ",
    "       iii    iii        ",
]


def cell(pose, frame, style):
    shadow, mid, wool, light, accessory = style
    im = Image.new("RGBA", (40, 32))
    d = ImageDraw.Draw(im)
    def box(x, y, w, h, color):
        d.rectangle((x, y, x+w-1, y+h-1), fill=color)
    def line(points, color, width=1):
        d.line(points, fill=color, width=width)
    def oval(bounds, color, edge=None):
        d.ellipse(bounds, fill=color, outline=edge)

    resting = pose in ("sit", "sleep", "land")
    airborne = pose in ("held", "fall", "jump")
    y = 9 + (3 if resting else -2 if airborne else 0)
    if (pose == "walk" and frame % 2) or (pose == "sleep" and frame == 2):
        y -= 1
    for n, x in enumerate((9, 13, 21, 25)):
        swing = (0, 2, 0, -2)[(frame + (0, 2, 2, 0)[n]) % 4]
        dx = swing if pose in ("walk", "fall", "held") else 0
        end = 27
        if resting:
            dx = -3 if n < 2 else 2
        if airborne:
            end = y + (22 if pose == "fall" else 18) + (swing//2)
        if pose == "jump":
            dx = -4 if n < 2 else 2
            end = 22
        line([(x, y+11), (x, end-3), (x+dx, end-1)], SKIN, 2)
        box(x+dx, end-2, 2, 1, "#ae918e")
        box(x+dx-1, end-1, 3, 2, INK)
    oval((2, y+4, 8, y+9), wool, INK)
    colors = dict(i=INK, h=light, w=wool, m=mid, s=shadow)
    for row, pixels in enumerate(FLEECE):
        for col, value in enumerate(pixels):
            if value != " ":
                d.point((5+col, y+row), fill=colors[value])
    for x, dy in ((10,5), (16,3), (20,8), (12,10)):
        line([(x,y+dy), (x,y+dy+1), (x+2,y+dy+1), (x+3,y+dy)], mid)
        box(x+1,y+dy,2,1,light)
    # Head tips forward at rest. Both eyes are visible in three-quarter view.
    hx = 23
    hy = y + {"sit": -1, "sleep": 4, "land": 3}.get(pose, 1)
    oval((hx, hy, hx+12, hy+11), SKIN, INK)
    oval((hx-3, hy+2, hx+2, hy+5), CHEEK, INK)
    oval((hx, hy-2, hx+9, hy+2), wool, INK)
    box(hx+2,hy-1,4,1,light)
    if pose == "fall":
        for x, size in ((hx+3,5), (hx+9,4)):
            oval((x-1,hy+2,x+size-2,hy+6), "#fffbef")
            box(x+1,hy+4,1,2,INK)
        oval((hx+7,hy+8,hx+10,hy+11), INK)
        box(hx+8,hy+10,2,1,CHEEK)
    elif pose == "faint":
        for x in (hx+3,hx+9):
            line([(x,hy+3),(x+2,hy+5)],light)
            line([(x+2,hy+3),(x,hy+5)],light)
    else:
        for x in (hx+3,hx+9):
            if pose in ("sleep","land","jump") or pose in ("idle", "sit") and frame == 3:
                line([(x,hy+5),(x+1,hy+(4 if pose=="jump" else 6)),(x+2,hy+5)], "#ffefd5")
            else:
                box(x,hy+3,3,3,"#fffbef")
                box(x+(0 if pose=="held" else 2),hy+4,1,2,INK)
        box(hx+8,hy+7,3,1,CHEEK)
        line([(hx+6,hy+8),(hx+7,hy+9),(hx+9,hy+9)],INK)
    if accessory == "flower":
        for x, dy in ((0,-2),(-2,0),(2,0),(0,2)):
            box(hx+x,hy+dy,2,2,"#d34d78")
        box(hx,hy,2,2,"#ffda69")
    elif accessory == "nightcap":
        d.polygon([(22,hy),(25,hy-5),(28,hy-6),(32,hy-3),(28,hy-3),(30,hy)],fill="#595481",outline=INK)
        box(22,hy,9,2,"#aba9e1")
        box(32,hy-4,2,2,"#ffda83")
    elif accessory == "scarf":
        line([(23,hy+8),(25,hy+11),(30,hy+12)],"#a4433c",3)
        line([(23,hy+9),(19,hy+11),(16,hy+10)],"#ea8252",2)
        box(19,hy+10,1,2,"#ffd48a")
    elif accessory == "horns":
        oval((hx-4,hy-2,hx+3,hy+5),"#e0ae74",INK)
        line([(hx-2,hy+2),(hx-1,hy),(hx+1,hy),(hx+1,hy+2),(hx,hy+2)],"#8d6056")
    if pose == "land":
        # Briefly flatten the whole silhouette against the perch.
        mass = im.crop((0, 0, 40, 28)).resize((40, 23), Image.Resampling.NEAREST)
        im = Image.new("RGBA", im.size)
        im.paste(mass, (0, 5))
    if pose == "sleep":
        x, z = 32+frame%2, 7-frame
        line([(x,z),(x+3,z),(x,z+3),(x+3,z+3)],"#a797cf")
    if pose == "faint":
        # Tipped onto the fleece, hooves in the air, with orbiting stars.
        flipped = im.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
        im = Image.new("RGBA", im.size)
        im.paste(flipped,(0,28-flipped.getbbox()[3]))
        d = ImageDraw.Draw(im)
        for x, z in ((27+frame,7),(36-frame,5)):
            box(x-1,z,3,1,"#e99c4b")
            box(x,z-1,1,3,"#ffd977")
    return im


atlas = Image.new("RGBA", (40*4*len(POSES), 32*len(STYLES)))
for row, style in enumerate(STYLES):
    for column, pose in enumerate(POSES):
        for frame in range(4):
            atlas.paste(cell(pose, frame, style), ((4*column+frame)*40, row*32))
atlas.save(ROOT / "assets/sheep.png", optimize=True)
