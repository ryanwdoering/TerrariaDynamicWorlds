# Building Anchor - Zone Management Controls

## Building Anchor Item Controls

```
┌─────────────────────────────────────────────────────────────────┐
│                    BUILDING ANCHOR CONTROLS                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  LEFT-CLICK + DRAG                                              │
│  ═══════════════════════                                        │
│  • Creates a new zone                                           │
│  • Click at starting corner, drag to opposite corner            │
│  • Release to save the zone                                     │
│  • Minimum size required                                        │
│  • Cannot overlap existing zones                                │
│                                                                 │
│                                                                 │
│  SHIFT + LEFT-CLICK                                             │
│  ══════════════════════                                         │
│  • Removes a single zone                                        │
│  • Click anywhere inside the zone you want to remove            │
│  • Instant removal with sound effect                            │
│  • Only removes zones on THIS anchor                            │
│                                                                 │
│                                                                 │
│  RIGHT-CLICK                                                    │
│  ═══════════                                                    │
│  • Removes ALL zones from this anchor                           │
│  • Clears everything at once                                    │
│  • Use with caution!                                            │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘

## Visual Indicators

When holding the Building Anchor:
- Gold overlay appears while dragging (shows zone being created)
- Zone boundaries shown in inventory/tooltip text
- Chat messages confirm zone creation/removal
```

## Workflow Example

```
Step 1: CREATING A ZONE
  Hold Building Anchor → Left-click & drag → Release
  ✓ Zone created with gold outline during drag
  ✓ Chat confirmation: "Building zone #1 added"

Step 2: MANAGING ZONES
  Option A - Remove one zone:
    Hold Building Anchor → Shift+Click inside zone
    ✓ Zone instantly removed
    ✓ Chat confirmation: "Building zone #1 removed"

  Option B - Remove all zones:
    Hold Building Anchor → Right-click anywhere
    ✓ All zones cleared
    ✓ Chat confirmation: "Cleared X zones"

Step 3: VIEW ZONES
  Hover over Building Anchor in inventory
  ✓ Tooltip shows all zones with coordinates
  ✓ Shows: Zone #ID: WxH — ground ref Y=###
```

## Tips & Tricks

✓ **Precision Removal:** Shift+Click on the zone center for best results  
✓ **Multiple Zones:** You can have many zones on one anchor  
✓ **No Overlap:** Zones cannot overlap - try dragging elsewhere  
✓ **Undo:** If you mess up, just recreate the zone by dragging again  
✓ **Audio Cues:** Listen for "Item4" (creation) and "Item14" (removal) sounds  

## Common Issues

**"Zone overlaps with existing zone"**
→ Move your zone to a clear area that doesn't touch other zones

**Shift+Click not working**
→ Make sure you're holding the Building Anchor item  
→ Click must be inside the zone boundaries  

**Can't create zones**
→ Minimum size required (at least 2×2 tiles)  
→ Check that you're in single-player mode
