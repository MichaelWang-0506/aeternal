"""Generates the hero's animation frames from the single protagonist sprite (the original is
never written): each frame re-poses parts of the art (legs, torso, arms, cape) with a smooth
warp, so idle, hop, attack, hit and guard read as real frame-by-frame motion.

Writes, under the project root:
  Assets/Images/Characters/protagonist_frames.png (+ .meta)  one sprite sheet, 512x768 cells
  Assets/Scriptable Objects/Hero Frames.asset (+ .meta)      the SpriteFrameSet listing them
GUIDs and sprite ids are derived from fixed names, so re-running rewrites the same assets.
Needs Pillow and NumPy. Run: python3 tools/content/gen_hero_frames.py <project root> [contact.png]
"""
import math, os, sys, uuid
import numpy as np
from PIL import Image

ROOT = sys.argv[1]
SOURCE = "Assets/Images/Characters/protagonist.png"
SHEET = "Assets/Images/Characters/protagonist_frames.png"
FRAME_SET = "Assets/Scriptable Objects/Hero Frames.asset"
FRAME_SET_SCRIPT_GUID = "5f0c2a9e7b3d4e1f8a6c9d2b4e7f1a3c"  # Assets/Scripts/FX/SpriteFrameSet.cs.meta

NS = uuid.UUID("3b1f6c2d-9e47-4a8b-b5d1-7c2e9f4a6b80")
SCALE = 0.5            # frames are drawn at full size, then halved (source is 1024x1536)
PPU = 500 * SCALE      # keeps the hero the same size in the world as protagonist.png
COLUMNS = 7
PAD = 64               # empty border added around the art, so a swinging cape isn't cut off

# Landmarks on protagonist.png (pixels, origin top-left).
FEET_Y = 1320          # bottom of the boots
BODY_X = 575           # centre line of the body
HEAD_Y = 125           # top of the hair
WAIST_Y = 700          # belt
HIP_Y = 1000           # where the legs come out of the coat


def guid(name):
    return uuid.uuid5(NS, name).hex


def sprite_id(name):
    """A stable, non-zero signed 64-bit id for a sprite in the sheet."""
    v = uuid.uuid5(NS, "sprite:" + name).int & ((1 << 63) - 1)
    return -(v or 1)


def ramp(v, a, b):
    """0 at a, 1 at b, smooth in between (a may be greater than b)."""
    t = np.clip((v - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def box(x, y, x0, x1, y0, y1, fx, fy=None):
    """1 inside the box, fading to 0 over fx pixels left and right of it, fy above and below."""
    fy = fx if fy is None else fy
    return ramp(x, x0 - fx, x0) * ramp(x, x1 + fx, x1) * ramp(y, y0 - fy, y0) * ramp(y, y1 + fy, y1)


# Body regions as weight fields over the output image.
def w_upper(x, y):      # head and torso: everything above the hips, fading out down to them
    return ramp(y, HIP_Y, WAIST_Y)


def w_left_arm(x, y):   # the arm and fist on the left of the image (the punching arm)
    return box(x, y, 100, 285, 640, 950, 45, 30)


def w_right_arm(x, y):  # the arm and fist on the right, in front of the cape
    return box(x, y, 650, 810, 640, 950, 60, 25)


def w_cape(x, y):       # the cape's free end: more movement the further down it hangs
    return box(x, y, 790, 1020, 380, 1160, 60) * ramp(y, 420, 1150)


def w_legs(x, y):
    return ramp(y, HIP_Y - 40, HIP_Y + 40)


def pose(**p):
    """Pose parameters (all default to rest). Offsets are in source pixels.
    tx, ty  move the whole body            sx, sy  stretch about the feet
    lean    shift of the head relative to the feet (positive: leans right)
    breathe raise of head and torso         tuck    leg shortening (0 none, 0.2 = 20%)
    larm, rarm, cape  (dx, dy) offsets of those regions"""
    base = dict(tx=0, ty=0, sx=1, sy=1, lean=0, breathe=0, tuck=0,
                larm=(0, 0), rarm=(0, 0), cape=(0, 0))
    base.update(p)
    return base


def source_coords(x, y, p):
    """Inverse warp: for output pixel (x, y), the protagonist.png pixel to show there."""
    u = x - p["tx"]
    v = y - p["ty"]
    # Lean: shear about the feet, the head moving by `lean`.
    u = u - p["lean"] * (FEET_Y - v) / (FEET_Y - HEAD_Y)
    # Squash and stretch about the feet.
    u = BODY_X + (u - BODY_X) / p["sx"]
    v = FEET_Y + (v - FEET_Y) / p["sy"]
    # Parts, measured at the posed position.
    up = w_upper(u, v)
    v = v - p["breathe"] * up
    for weight, (dx, dy) in ((w_left_arm, p["larm"]), (w_right_arm, p["rarm"]), (w_cape, p["cape"])):
        w = weight(u, v)
        u = u - dx * w
        v = v - dy * w
    # Tucked legs: the leg area is drawn shorter, so the boots rise toward the coat.
    if p["tuck"]:
        legs = v > HIP_Y
        v = np.where(legs, HIP_Y + (v - HIP_Y) / (1 - p["tuck"]), v)
    return u, v


def render(src, p):
    """Samples src through the warp (nearest pixel, so the pixel art stays crisp), onto a
    canvas PAD pixels larger on every side."""
    h, w = src.shape[:2]
    y, x = np.mgrid[0:h + 2 * PAD, 0:w + 2 * PAD].astype(np.float32)
    u, v = source_coords(x - PAD, y - PAD, p)
    ui = np.rint(u).astype(int)
    vi = np.rint(v).astype(int)
    inside = (ui >= 0) & (ui < w) & (vi >= 0) & (vi < h)
    out = np.zeros((h + 2 * PAD, w + 2 * PAD, 4), dtype=src.dtype)
    out[inside] = src[vi[inside], ui[inside]]
    return Image.fromarray(out)


# The animations. Each entry: (state, list of poses), in play order.
ANIMATIONS = [
    # Breathing in and out, the cape stirring.
    ("idle", [pose(),
              pose(breathe=-6, cape=(6, 0)),
              pose(breathe=-11, cape=(12, -3), larm=(0, -3), rarm=(0, -3)),
              pose(breathe=-6, cape=(5, 0))]),
    # A hop to the next square: crouch, push off, tuck, fall, land.
    ("move", [pose(sy=0.9, sx=1.05, breathe=4, cape=(0, 6)),
              pose(ty=-20, sy=1.06, sx=0.97, cape=(-8, 18), larm=(-10, 12), rarm=(10, 12)),
              pose(ty=-95, tuck=0.16, cape=(-14, 30), larm=(-18, -10), rarm=(16, -10)),
              pose(ty=-105, tuck=0.2, cape=(-10, 18), larm=(-20, -18), rarm=(18, -18)),
              pose(ty=-45, sy=1.05, tuck=0.06, cape=(4, -18), larm=(-8, -6), rarm=(8, -6)),
              pose(sy=0.88, sx=1.06, breathe=6, cape=(8, 6))]),
    # A left-handed punch: wind up, strike, follow through, recover.
    ("attack", [pose(lean=22, sy=0.97, larm=(25, -12), cape=(-6, 0)),
                pose(lean=34, sy=0.95, larm=(40, -18), rarm=(10, 0), cape=(-10, 4)),
                pose(lean=-40, sx=1.03, larm=(-60, -14), rarm=(-14, 0), cape=(26, -10)),
                pose(lean=-30, sx=1.02, larm=(-45, -8), cape=(20, -6)),
                pose(lean=-12, larm=(-30, -4), cape=(8, 0))]),
    # Knocked back by a blow, then steadying.
    ("hit", [pose(lean=40, sy=0.93, sx=1.04, larm=(18, -14), rarm=(14, -14), cape=(-14, 8)),
             pose(lean=24, sy=0.96, larm=(10, -8), rarm=(8, -8), cape=(-8, 4)),
             pose(lean=9, sy=0.99, cape=(-3, 0))]),
    # Bracing behind the arms when shielding or healing.
    ("guard", [pose(sy=0.96, breathe=4, larm=(36, -20), rarm=(-24, -20), cape=(-20, 0)),
               pose(sy=0.93, breathe=8, larm=(52, -34), rarm=(-34, -30), cape=(-36, 0)),
               pose(sy=0.94, breathe=6, larm=(48, -30), rarm=(-30, -26), cape=(-30, 0))]),
]


def main():
    src = np.array(Image.open(os.path.join(ROOT, SOURCE)).convert("RGBA"))
    h, w = src.shape[:2]
    cw, ch = int((w + 2 * PAD) * SCALE), int((h + 2 * PAD) * SCALE)

    frames = []  # (name, state, image)
    for state, poses in ANIMATIONS:
        for i, p in enumerate(poses):
            img = render(src, p).resize((cw, ch), Image.NEAREST)
            frames.append((f"hero_{state}_{i}", state, img))

    rows = math.ceil(len(frames) / COLUMNS)
    sheet = Image.new("RGBA", (cw * COLUMNS, ch * rows), (0, 0, 0, 0))
    rects = []
    for k, (name, state, img) in enumerate(frames):
        cx, cy = (k % COLUMNS) * cw, (k // COLUMNS) * ch
        sheet.paste(img, (cx, cy))
        rects.append((name, state, cx, sheet.height - cy - ch))  # Unity rects start bottom-left
    sheet.save(os.path.join(ROOT, SHEET))

    # Pivot: the centre of protagonist.png's trimmed sprite rect (x 123-1024, y 122-1333 from the
    # top), so a frame lines up exactly with the sprite it replaces.
    pivot = (((123 + 1024) / 2 + PAD) / (w + 2 * PAD), 1 - ((122 + 1333) / 2 + PAD) / (h + 2 * PAD))
    write_sheet_meta(rects, cw, ch, pivot, sheet.size)
    write_frame_set(rects)

    if len(sys.argv) > 2:  # a labelled contact sheet for reviewing the poses
        thumbs = Image.new("RGBA", sheet.size, (40, 40, 48, 255))
        thumbs.alpha_composite(sheet)
        thumbs.convert("RGB").resize((sheet.width // 2, sheet.height // 2)).save(sys.argv[2])
    print(f"{len(frames)} frames, sheet {sheet.size[0]}x{sheet.size[1]}")


def write_sheet_meta(rects, cw, ch, pivot, size):
    sprites, table, ids = [], [], []
    for name, _, x, y in rects:
        sid = sprite_id(name)
        table.append(f"  - first:\n      213: {sid}\n    second: {name}")
        ids.append(f"      {name}: {sid}")
        sprites.append(f"""    - serializedVersion: 2
      name: {name}
      rect:
        serializedVersion: 2
        x: {x}
        y: {y}
        width: {cw}
        height: {ch}
      alignment: 9
      pivot: {{x: {pivot[0]:.5f}, y: {pivot[1]:.5f}}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      customData:
      outline: []
      physicsShape: []
      tessellationDetail: -1
      bones: []
      spriteID: {guid('spriteID:' + name)}
      internalID: {sid}
      vertices: []
      indices:
      edges: []
      weights: []""")
    max_size = 4096 if max(size) > 2048 else 2048
    platform = lambda target: f"""  - serializedVersion: 4
    buildTarget: {target}
    maxTextureSize: {max_size}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0"""
    meta = f"""fileFormatVersion: 2
guid: {guid(SHEET)}
TextureImporter:
  internalIDToNameTable:
{chr(10).join(table)}
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: {max_size}
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 2
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: {PPU:g}
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
{platform('DefaultTexturePlatform')}
{platform('Standalone')}
  spriteSheet:
    serializedVersion: 2
    sprites:
{chr(10).join(sprites)}
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable:
{chr(10).join(ids)}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""
    write(SHEET + ".meta", meta)


def write_frame_set(rects):
    sheet_guid = guid(SHEET)
    lists = []
    for state, _ in ANIMATIONS:
        refs = [f"  - {{fileID: {sprite_id(n)}, guid: {sheet_guid}, type: 3}}" for n, s, _, _ in rects if s == state]
        lists.append(f"  {state}:\n" + "\n".join(refs))
    asset = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {FRAME_SET_SCRIPT_GUID}, type: 3}}
  m_Name: Hero Frames
  m_EditorClassIdentifier: Assembly-CSharp::Aeternal.FX.SpriteFrameSet
  idleFramesPerSecond: 5
  actionFramesPerSecond: 18
  artFacesLeft: 1
{chr(10).join(lists)}
"""
    write(FRAME_SET, asset)
    write(FRAME_SET + ".meta", f"""fileFormatVersion: 2
guid: {guid(FRAME_SET)}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
""")


def write(rel, text):
    with open(os.path.join(ROOT, rel), "w", newline="\n") as f:
        f.write(text)


if __name__ == "__main__":
    main()
