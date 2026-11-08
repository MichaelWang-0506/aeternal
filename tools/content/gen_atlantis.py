"""Generates Level 2 (Atlantis) content for Aeternal: recoloured sprites and card art (copies,
the originals are never written), and the region's ScriptableObject assets as Unity YAML.
GUIDs are derived from each asset's path, so re-running rewrites the same assets in place.
Needs Pillow (pip install pillow). Run: python3 tools/content/gen_atlantis.py <project root>
"""
import colorsys, os, re, sys, uuid
from PIL import Image

ROOT = sys.argv[1]
NS = uuid.UUID('6f1c3d1e-8a55-4c5b-9a4e-1e2a3b4c5d6e')

def guid(path):
    return uuid.uuid5(NS, path).hex

def p(*parts):
    return os.path.join(ROOT, *parts)

def write(rel, text):
    full = p(rel)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, 'w', newline='') as f:
        f.write(text)

FOLDER_META = '''fileFormatVersion: 2
guid: {g}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
'''
ASSET_META = '''fileFormatVersion: 2
guid: {g}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
'''

def folder(rel):
    """Folder plus its .meta (and its parents' up to Assets)."""
    parts = rel.split('/')
    for i in range(2, len(parts) + 1):
        sub = '/'.join(parts[:i])
        os.makedirs(p(sub), exist_ok=True)
        meta = p(sub + '.meta')
        if not os.path.exists(meta):
            write(sub + '.meta', FOLDER_META.format(g=guid(sub)))

# ----------------------------------------------------------------------------
# Images: recoloured copies
# ----------------------------------------------------------------------------
def recolor(src_rel, dst_rel, rules):
    """Copies src to dst, recolouring pixels by the first matching rule. A rule's test gets
    (hue degrees, s, v, x, y); its map returns (hue, s, v[, blend])."""
    im = Image.open(p(src_rel)).convert('RGBA')
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            hh, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            d = hh * 360
            for test, fn in rules:
                if test(d, s, v, x, y):
                    res = fn(d, s, v, x, y)
                    t = res[3] if len(res) > 3 else 1.0
                    rr, gg, bb = colorsys.hsv_to_rgb((res[0] % 360) / 360, min(1, max(0, res[1])), min(1, max(0, res[2])))
                    px[x, y] = (int(r + (rr * 255 - r) * t), int(g + (gg * 255 - g) * t), int(b + (bb * 255 - b) * t), a)
                    break
    os.makedirs(os.path.dirname(p(dst_rel)), exist_ok=True)
    im.save(p(dst_rel))
    # Same import settings and sprite ids as the source, under a new guid: references to the
    # copy use the source's sprite fileID with the copy's guid.
    meta = open(p(src_rel + '.meta'), newline='').read()
    old = re.search(r'guid: (\w+)', meta).group(1)
    src_name = os.path.splitext(os.path.basename(src_rel))[0]
    dst_name = os.path.splitext(os.path.basename(dst_rel))[0]
    meta = meta.replace(f'guid: {old}', f'guid: {guid(dst_rel)}', 1).replace(f'{src_name}_0', f'{dst_name}_0')
    write(dst_rel + '.meta', meta)
    return guid(dst_rel)

def sprite_id(src_rel):
    meta = open(p(src_rel + '.meta')).read()
    return re.search(r'internalID: (-?\d+)', meta).group(1)

C = 'Assets/Images/Characters/'
A = 'Assets/Images/Atlantis/'
folder('Assets/Images/Atlantis/Characters')
folder('Assets/Images/Atlantis/Cards')

g_eyes = lambda d, s, v, x, y: 22 <= d <= 42 and s > 0.5 and v > 0.8
g_skin = lambda d, s, v, x, y: 40 <= d <= 80 and s > 0.3 and v > 0.08
g_cloth = lambda d, s, v, x, y: (d <= 40 or d >= 330) and s > 0.3 and v > 0.08
b_bandana = lambda d, s, v, x, y: (d <= 11 or d >= 345) and s > 0.7 and v > 0.12
b_skin = lambda d, s, v, x, y: d <= 40 and s > 0.4 and v >= 0.3
b_cloth = lambda d, s, v, x, y: y > 420 and (d <= 40 or d >= 340) and s > 0.5 and 0.04 < v < 0.3
below_beard = lambda y: min(1, (y - 420) / 110)

SPRITES = {}
# Reef Lurker: sea-green skin, sickly yellow-green eyes, coral-red rags.
SPRITES['reef_lurker'] = (recolor(C + 'goblin.png', A + 'Characters/reef_lurker.png', [
    (g_eyes,  lambda d, s, v, x, y: (78, 0.85, 1.0)),
    (g_skin,  lambda d, s, v, x, y: (155, s * 0.8, v * 1.05)),
    (g_cloth, lambda d, s, v, x, y: (8, min(1, s * 1.1), min(1, v * 1.35))),
]), sprite_id(C + 'goblin.png'))
# Tidecaller: deep teal skin, pale pink eyes, violet robes.
SPRITES['tidecaller'] = (recolor(C + 'goblin.png', A + 'Characters/tidecaller.png', [
    (g_eyes,  lambda d, s, v, x, y: (330, 0.3, 1.0)),
    (g_skin,  lambda d, s, v, x, y: (188, min(1, s * 0.95), v * 1.1)),
    (g_cloth, lambda d, s, v, x, y: (285, s * 0.7, v * 1.1)),
]), sprite_id(C + 'goblin.png'))
# Drowned Sailor: grey-green corpse skin, sea-faded teal clothes and bandana.
SPRITES['drowned_sailor'] = (recolor(C + 'bandit.png', A + 'Characters/drowned_sailor.png', [
    (b_bandana, lambda d, s, v, x, y: (175, 0.45, v * 1.3)),
    (b_skin,    lambda d, s, v, x, y: (95, s * 0.3, v * 0.92)),
    (b_cloth,   lambda d, s, v, x, y: (185, 0.4, min(1, v * 1.6 + 0.02), below_beard(y))),
]), sprite_id(C + 'bandit.png'))
# Drowned Captain: pale blue skin, navy coat, gold bandana.
SPRITES['drowned_captain'] = (recolor(C + 'bandit.png', A + 'Characters/drowned_captain.png', [
    (b_bandana, lambda d, s, v, x, y: (44, 0.8, min(1, v * 2.0))),
    (b_skin,    lambda d, s, v, x, y: (205, s * 0.25, v * 0.95)),
    (b_cloth,   lambda d, s, v, x, y: (225, 0.65, min(1, v * 1.9 + 0.03), below_beard(y))),
]), sprite_id(C + 'bandit.png'))
# Poseidon: aqua skin, sea-green robe and foam-white beard, deep blue weapon; gold kept.
z_gold = lambda d, s, v, x, y: 28 <= d <= 60 and s > 0.55 and v > 0.45
z_skin = lambda d, s, v, x, y: (d < 28 or d >= 340) and s > 0.4 and v > 0.12
z_robe = lambda d, s, v, x, y: s <= 0.4 and v > 0.45
z_bolt = lambda d, s, v, x, y: 165 <= d <= 215 and s > 0.15
SPRITES['poseidon'] = (recolor(C + 'zeus.png', A + 'Characters/poseidon.png', [
    (z_gold, lambda d, s, v, x, y: (d, s, v)),
    (z_bolt, lambda d, s, v, x, y: (212, min(1, s * 1.2 + 0.2), v * 0.9)),
    (z_skin, lambda d, s, v, x, y: (182, s * 0.55, v * 1.05)),
    (z_robe, lambda d, s, v, x, y: (172, 0.42 if y > 560 else 0.18, v * 0.95)),
]), sprite_id(C + 'zeus.png'))

# Card art: warm hues turned to the sea (the card frames go blue-grey too, marking the deck).
def seaward(d, s, v, x, y):
    off = ((d + 40) % 360) - 40           # -40..320, warm hues near 0..60
    return (195 + off * 0.45, s * 0.9, v)
sea_test = lambda d, s, v, x, y: s > 0.12 and (d <= 75 or d >= 320)
CARD_ART = {}
for card, src in [('Trident Strike', 'Thunder/thunderspear.png'), ('Wave Push', 'Thunder/punch.png'),
                  ('Water Cage', 'Steampunk/Recycle.png'), ('Hidden Wave', 'Steampunk/steammist.png'),
                  ("Ocean's Blessing", 'Thunder/thundersrage.png'), ("Ocean's Protection", 'Thunder/thunderarmor.png'),
                  ("Ocean's Flow", 'Steampunk/BackflipKick.png'), ('Ocean Block', 'Thunder/teleport.png'),
                  ('Suffocation', 'Steampunk/Disembowel.png'), ('Breakout', 'Thunder/ThunderStrike.png')]:
    file = card.lower().replace("'", '').replace(' ', '_') + '.png'
    CARD_ART[card] = (recolor('Assets/Images/' + src, A + 'Cards/' + file, [(sea_test, seaward)]),
                      sprite_id('Assets/Images/' + src))

# ----------------------------------------------------------------------------
# ScriptableObjects
# ----------------------------------------------------------------------------
SCRIPT = {
    'MapData': ('00f922e1bb0064b03ae880384cb54fef', 'Aeternal.Encounters.MapData'),
    'EncounterData': ('e18f6374be14b4ccd9db12d67257ed96', 'Aeternal.Encounters.EncounterData'),
    'PlayerStats': ('4ad4c5ce00fbf7247a91ff04b126a104', 'Aeternal.Stats.PlayerStats'),
    'DeckData': ('9859ce49cdfde9c4abbc1bc7701a1830', 'Aeternal.Decks.DeckData'),
    'CardData': ('48684741d3f20674fb0731f745997a0f', 'Aeternal.Cards.CardData'),
}

def asset(rel, kind, name, body):
    sg, cls = SCRIPT[kind]
    text = f'''%YAML 1.1
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
  m_Script: {{fileID: 11500000, guid: {sg}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier: Assembly-CSharp::{cls}
{body}'''
    write(rel, text)
    write(rel + '.meta', ASSET_META.format(g=guid(rel)))
    return guid(rel)

def ref(g):
    return f'{{fileID: 11400000, guid: {g}, type: 2}}'

def sprite_ref(pair):
    g, fid = pair
    return f'{{fileID: {fid}, guid: {g}, type: 3}}'

def q(text):
    return '"' + text.replace('\\', '\\\\').replace('"', '\\"') + '"'

def lines(dialogue, indent='  '):
    if not dialogue:
        return ' []'
    out = ''
    for speaker, text in dialogue:
        out += f'\n{indent}- speaker: {q(speaker)}\n{indent}  text: {q(text)}'
    return out

# Effect enum values (keep in step with EffectType.cs etc.)
DAMAGE, HEAL, SHIELD, AP, DF, DRAW, RECYCLE, MOVE, STATUS, ERASE, TERRAIN, EXTEND, CONSUME = range(13)
SELF, ENEMY = 0, 1
PHYSICAL, TRUE_DMG, FIRE, LIGHTNING, WATER = 0, 1, 2, 3, 4
NO_BEHAVIOR, BLEED, DODGE, BLACKOUT, CAGED = range(5)
BEHIND, PREVIOUS = 0, 1
ALWAYS, TERRAIN_BEHIND = 0, 1
CAGE = ('Caged', CAGED, 5, 2)          # statusId, behavior, damage per round, growth per round

def effect(kind, magnitude=0, rounds=0, target=ENEMY, dmg=PHYSICAL, status=None, place=BEHIND, cond=ALWAYS, indent='  '):
    sid, beh, val, grow = status if status else ('', NO_BEHAVIOR, 0, 0)
    i = indent
    return (f'\n{i}- effectType: {kind}\n{i}  magnitude: {magnitude}\n{i}  durationRounds: {rounds}\n{i}  target: {target}'
            f'\n{i}  damageType: {dmg}\n{i}  statusEffectData:\n{i}    statusId: {sid}\n{i}    behavior: {beh}'
            f'\n{i}    behaviorValue: {val}\n{i}    growthPerRound: {grow}\n{i}  terrainPlacement: {place}\n{i}  condition: {cond}')

S = 'Assets/Scriptable Objects/Atlantis/'
folder('Assets/Scriptable Objects/Atlantis/Enemies')
folder('Assets/Scriptable Objects/Atlantis/Poseidon Cards')
folder('Assets/Scriptable Objects/Atlantis/Reward Cards')

# ---- Poseidon's cards (Initial Card Ideas: Poseidon). AP costs as in the document.
STUN = ('Blackout', BLACKOUT, 0, 0)
POSEIDON = {}
RARITY = {'Common': 0, 'Uncommon': 1, 'Rare': 2}
POSEIDON_RARITY = {"Ocean's Protection": 'Uncommon', "Ocean's Blessing": 'Uncommon', 'Water Cage': 'Uncommon',
                   'Breakout': 'Rare', 'Suffocation': 'Rare'}
def card(name, card_id, cost, desc, effects):
    rarity = RARITY[POSEIDON_RARITY.get(name, 'Common')]
    body = (f'  id: {card_id}\n  title: \n  description: {q(desc)}\n  artwork: {sprite_ref(CARD_ART[name])}\n'
            f'  costAP: {cost}\n  rarity: {rarity}\n  effects:' + ''.join(effects) + '\n')
    POSEIDON[name] = asset(S + f'Poseidon Cards/{name}.asset', 'CardData', name, body)

card('Wave Push', 'poseidon_wave_push', 1,
     'Deal 5 water damage. If terrain stands behind the enemy, stun it for 1 round.',
     [effect(DAMAGE, 5, dmg=WATER), effect(STATUS, 1, 1, status=STUN, cond=TERRAIN_BEHIND)])
card('Water Cage', 'poseidon_water_cage', 2,
     'Cage the enemy in water for 1 round: 5 damage at round end, 2 more for each round it holds.',
     [effect(STATUS, 1, 1, status=CAGE)])
card('Hidden Wave', 'poseidon_hidden_wave', 1,
     'Deal 5 water damage. Raise terrain behind the enemy for 2 rounds. Two terrains become a water cage.',
     [effect(DAMAGE, 5, dmg=WATER), effect(TERRAIN, 2, 1, status=CAGE, place=BEHIND)])
card("Ocean's Blessing", 'poseidon_blessing', 1, 'Gain 2 Action Points.',
     [effect(AP, 2, target=SELF)])
card("Ocean's Protection", 'poseidon_protection', 1, 'Gain 7 shield.',
     [effect(SHIELD, 7, target=SELF)])
card("Ocean's Flow", 'poseidon_flow', 1, 'Draw 2 cards.',
     [effect(DRAW, 2, target=SELF)])
card('Trident Strike', 'poseidon_trident', 1,
     'Deal 5 water damage, then leap to the other side if it is open.',
     [effect(DAMAGE, 5, dmg=WATER), effect(MOVE, 2, target=SELF)])
card('Ocean Block', 'poseidon_block', 1,
     'Move clockwise once, then raise terrain where you stood for 2 rounds.',
     [effect(MOVE, 1, target=SELF), effect(TERRAIN, 2, 1, status=CAGE, place=PREVIOUS)])
card('Suffocation', 'poseidon_suffocation', 3, "Double the rounds left on the enemy's water cage.",
     [effect(EXTEND, 2, status=CAGE)])
card('Breakout', 'poseidon_breakout', 2,
     'Break the water cage: deal 6 water damage for each round it had left.',
     [effect(CONSUME, 6, dmg=WATER, status=CAGE)])

def deck_entries(pairs):
    return ''.join(f'\n  - card: {ref(g)}\n    count: {n}' for g, n in pairs)

core = asset(S + 'Poseidon Core Deck.asset', 'DeckData', 'Poseidon Core Deck',
    '  deckId: Poseidon\n'
    f'  description: {q("Core of Poseidon" + chr(39) + "s blessing; the Tide Pearls gathered in Atlantis are added when it awakens.")}\n'
    '  artwork: {fileID: 0}\n'
    '  defaultCards:' + deck_entries([(POSEIDON['Trident Strike'], 1), (POSEIDON['Wave Push'], 1),
                                       (POSEIDON['Hidden Wave'], 2), (POSEIDON['Water Cage'], 1),
                                       (POSEIDON["Ocean's Flow"], 1)]) +
    '\n  onShiftIn:' + effect(SHIELD, 4, target=SELF) +
    '\n  onShiftOut: []\n  shiftUpgradeCosts:\n  - 2\n  - 3\n  - 4\n  shiftUpgradeBonus: 2\n')

# ---- Reward cards: copies, so tuning them here leaves Level 1 alone. The starting deck's pool
# copies Zeusonia's reward pool; the awakened Zeus deck's pool copies Zeus's cards.
def copy_card(src_rel, src):
    text = open(p(src_rel), newline='').read()
    text = text.replace('\r\n', '\n')
    name = f'Atlantis {src}'
    text = re.sub(r'\n  m_Name: .*', f'\n  m_Name: {name}', text, count=1)
    text = re.sub(r'(\n  id: [^\n]*)', r'\1' + f'\n  title: {src}', text, count=1)
    rel = S + f'Reward Cards/{name}.asset'
    write(rel, text)
    write(rel + '.meta', ASSET_META.format(g=guid(rel)))
    return guid(rel)

REWARDS = [copy_card(f'Assets/Scriptable Objects/Cards/First Deck/First Deck Cards/{src}.asset', src)
           for src in ['Smash', 'Disembowel', 'Steam Mist', 'BackflipKick', 'Sacrifice', 'Slash', 'Sword Swing']]
ZEUS_REWARDS = [copy_card(f'Assets/Scriptable Objects/Cards/Zeus Deck/Zeus Cards/{src}.asset', src)
                for src in ['Thunder Strike', 'Gnaw', 'Thunder Spear', 'Thunder Armor', 'Combo Punch', 'Teleport',
                            'Extraction', "Thunder's Rage"]]

# ---- Enemies
def stats(name, hp):
    return asset(S + f'Enemies/{name} Stats.asset', 'PlayerStats', f'{name} Stats',
                 f'  maxHealth: {hp}\n  allowOverheal: 0\n  maxAP: 3\n  maxDF: 2\n  baseDefense: 0\n')

def encounter(name, eid, hp, dmg, dtype, aoe, sprite, intro, boss=False, elite=False, scale=1.0,
              terrain_every=0, charged_every=0, extra=''):
    g = stats(name, hp)
    body = (f'  encounterId: {eid}\n  displayName: {name}\n  isBoss: {int(boss)}\n  isElite: {int(elite)}\n'
            f'  stormy: 0\n  stats: {ref(g)}\n  baseDamage: {dmg}\n  damageType: {dtype}\n  canAOE: {int(aoe)}\n'
            f'  turnsBetweenAttacks: 1\n  sprite: {sprite_ref(SPRITES[sprite])}\n'
            f'  spriteTint: {{r: 1, g: 1, b: 1, a: 1}}\n  spriteScale: {scale}\n'
            f'  terrainEvery: {terrain_every}\n  terrainRounds: 2\n  terrainName: CORAL WALL\n'
            f'  terrainWarning: {q("Coral bursts from the sea floor beside you. You can" + chr(39) + "t step there.")}\n'
            f'  chargedAttackEvery: {charged_every}\n' + extra +
            '  introDialogue:' + lines(intro) + '\n')
    return asset(S + f'Enemies/{name} Encounter.asset', 'EncounterData', f'{name} Encounter', body)

sailor = encounter('Drowned Sailor', 'drowned_sailor', 20, 4, PHYSICAL, False, 'drowned_sailor', [
    ('Drowned Sailor', 'Ship went down with all hands. The sea kept us. It will keep you.'),
    ('The Ruler', 'I have been dead before. It did not take.')])
lurker = encounter('Reef Lurker', 'reef_lurker', 16, 3, WATER, True, 'reef_lurker', [
    ('', 'Something green and grinning uncurls from the coral.')])
captain = encounter('Drowned Captain', 'drowned_captain', 34, 4, PHYSICAL, False, 'drowned_captain', [
    ('Drowned Captain', 'Poseidon gave me the reef to guard. Every wall of it answers to me.'),
    ('Drowned Captain', 'Run if you like. The coral will close the way.')],
    elite=True, scale=1.15, terrain_every=2)
tidecaller = encounter('Tidecaller', 'tidecaller', 30, 5, WATER, True, 'tidecaller', [
    ('Tidecaller', 'The tide answers my call. Three squares at once, mortal. Can you dance?')],
    elite=True, scale=1.15)
poseidon = encounter('Poseidon', 'poseidon', 72, 4, WATER, True, 'poseidon', [
    ('', 'At the heart of the sunken city, a throne of coral. The lord of the sea rises from it.'),
    ('Poseidon', 'My brother let you climb his mountain. The sea is not so forgiving.'),
    ('The Ruler', 'I took his thunder. I will take your tide.'),
    ('Poseidon', 'Then drown trying.')],
    boss=True, scale=1.0, terrain_every=2, charged_every=3,
    extra=('  chargedAttackMultiplier: 2\n  chargedAttackName: TIDAL WAVE\n  chargeTitle: THE TIDE RISES\n'
           f'  chargeWarning: {q("The sea pulls back. Next round a wave crashes over every square but one!")}\n'
           f'  chargeRelease: {q("Only one square is safe from the wave. Move!")}\n'))

# ---- The region
atlantis = asset(S + 'Atlantis.asset', 'MapData', 'Atlantis',
    '  regionName: Atlantis\n  shardName: Tide Pearl\n'
    '  backgroundTint: {r: 0.16, g: 0.34, b: 0.4, a: 1}\n  ambience: 1\n'
    '  startingDeck: {fileID: 11400000, guid: 21085a2bfd26475c801f12e228e9cb98, type: 2}\n'
    '  shiftDecks: []\n'
    f'  encounters:\n  - {ref(sailor)}\n  - {ref(lurker)}\n  - {ref(captain)}\n  - {ref(poseidon)}\n'
    '  cardRewardPool:' + ''.join(f'\n  - {ref(g)}' for g in REWARDS) + '\n'
    '  onRegionClearedScene: MainMenu\n  showMap: 1\n  nextMap: {fileID: 0}\n  keepsProgress: 1\n'
    '  openingNarrative:' + lines([
        ('', 'The storm broke, and the ruler fell with the rain, down through the clouds and into the sea.'),
        ('', 'Beneath the waves lay a city he knew: his own harbour capital, sunk long ago, now Atlantis, the realm of Poseidon.'),
        ('', "Zeus's thunder answers him now. When the fight turns, Shift to the Zeus deck (Z, or the button by your Divine Focus)."),
        ('', 'Poseidon waits at the heart of the city. Gather the Tide Pearls on the way; his blessing will wake them.'),
    ]) + '\n'
    f'  blessingDeck: {ref(core)}\n'
    '  stormShardPool:' + ''.join(f'\n  - {ref(POSEIDON[n])}' for n in
                                  ['Ocean Block', "Ocean's Protection", "Ocean's Blessing", 'Suffocation', 'Breakout', 'Trident Strike']) + '\n'
    '  blessingNarrative:' + lines([
        ('Poseidon', 'You hold your breath well, mortal. Very well. Even the sea must give way to some things.'),
        ('Poseidon', 'Take my blessing, and the tide with it. Build your walls; drown your enemies in them.'),
        ('', 'The Tide Pearls the ruler had gathered glow, and the sea answers him.'),
    ]) + '\n'
    '  blessingTitle: Blessing of Poseidon\n'
    '  regionEvent:\n    enabled: 1\n    afterEncounter: 1\n    title: Drowned Shrine\n'
    f'    text: {q("A shrine to Poseidon stands half-buried in sand, its offering bowl still full of pearls. The water around it is warm.")}\n'
    '    healAmount: 8\n    shardChoices: 3\n'
    '  branchingMap: 1\n'
    f'  normalEncounters:\n  - {ref(lurker)}\n  - {ref(sailor)}\n'
    f'  eliteEncounters:\n  - {ref(captain)}\n  - {ref(tidecaller)}\n'
    f'  bossEncounter: {ref(poseidon)}\n'
    '  restHeal: 10\n'
    '  goldReward: {x: 16, y: 24}\n  eliteGoldReward: {x: 34, y: 44}\n'
    '  deckChoiceRewards: 1\n'
    '  deckRewardPools:\n  - deckId: Zeus\n    cards:' + ''.join(f'\n    - {ref(g)}' for g in ZEUS_REWARDS) + '\n'
    '  sealedChoices: 2\n  replacePrice: 60\n')

print('Atlantis map guid', atlantis)
