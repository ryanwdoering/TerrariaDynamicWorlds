#nullable enable
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace DynamicWorlds
{
	public class PrefabToolSystem : ModSystem
	{
		public const int MaxPrefabs = 10;
		public static readonly BuildingZone?[] Prefabs = new BuildingZone?[MaxPrefabs];
		public static int CurrentSlot = 0;

		public override void SaveWorldData(TagCompound tag)
		{
			var list = new System.Collections.Generic.List<TagCompound>();
			for (int i = 0; i < MaxPrefabs; i++)
			{
				if (Prefabs[i] != null)
					list.Add(Prefabs[i]!.ToTag());
			}
			tag["PrefabSlots"] = list;
			tag["PrefabCurrentSlot"] = CurrentSlot;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			for (int i = 0; i < MaxPrefabs; i++)
				Prefabs[i] = null;

			if (tag.ContainsKey("PrefabSlots"))
			{
				var list = tag.GetList<TagCompound>("PrefabSlots");
				int idx = 0;
				foreach (var t in list)
				{
					if (idx >= MaxPrefabs) break;
					Prefabs[idx] = BuildingZone.FromTag(t);
					idx++;
				}
			}

			CurrentSlot = tag.ContainsKey("PrefabCurrentSlot") ? tag.GetInt("PrefabCurrentSlot") : 0;
			CurrentSlot = (CurrentSlot + MaxPrefabs) % MaxPrefabs;
		}

		public static void SavePrefab(Point16 topLeft, Point16 bottomRight)
		{
			int id = CurrentSlot;
			Prefabs[id] = BuildingZone.Capture(topLeft, bottomRight, id);
			Main.NewText($"Saved prefab to slot {id + 1}/{MaxPrefabs} ({Prefabs[id]?.Width}×{Prefabs[id]?.Height}).", 100, 220, 255);
		}

		public static void PastePrefabAtCursor()
		{
			var zone = Prefabs[CurrentSlot];
			if (zone == null)
			{
				Main.NewText($"Prefab slot {CurrentSlot + 1} is empty.", 255, 120, 120);
				return;
			}

			int cx = (int)(Main.MouseWorld.X / 16f);
			int cy = (int)(Main.MouseWorld.Y / 16f);
			var placement = zone.PredictRestorePlacement(cx, cy);
			zone.RestoreToPlacement(placement, "[Prefab]");
			SoundEngine.PlaySound(SoundID.MenuOpen, Main.MouseWorld);
			Main.NewText($"Pasted prefab slot {CurrentSlot + 1} at ({placement.TopLeft.X},{placement.TopLeft.Y}).", 120, 255, 120);
		}

		public static void CycleSlot(int delta)
		{
			CurrentSlot = (CurrentSlot + delta + MaxPrefabs) % MaxPrefabs;
			Main.NewText($"Prefab slot: {CurrentSlot + 1}/{MaxPrefabs}", 200, 200, 255);
		}
	}

	public class PrefabToolPlayer : ModPlayer
	{
		private bool _dragging;
		private Point16 _dragStart;
		private Point16 _dragEnd;
		private bool _wasMouseDown;

		public override void PostUpdate()
		{
			if (Main.netMode != NetmodeID.SinglePlayer || Main.mapFullscreen)
			{
				CancelDrag();
				return;
			}

			bool holding = Player.HeldItem?.type == ModContent.ItemType<PrefabToolItem>();
			if (!holding)
			{
				CancelDrag();
				return;
			}

			bool mouseLeft = Main.mouseLeft && !Main.LocalPlayer.mouseInterface;
			bool mouseRight = Main.mouseRight && !Main.LocalPlayer.mouseInterface;
			bool shiftHeld = Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift) || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);

			int tx = (int)(Main.MouseWorld.X / 16f);
			int ty = (int)(Main.MouseWorld.Y / 16f);

			// Shift + right-click: cycle prefab slot
			if (shiftHeld && mouseRight && !_wasMouseDown)
			{
				PrefabToolSystem.CycleSlot(1);
				_wasMouseDown = true;
				return;
			}

			// Right-click paste current prefab
			if (mouseRight && !_wasMouseDown)
			{
				PrefabToolSystem.PastePrefabAtCursor();
				_wasMouseDown = true;
				return;
			}

			// Left-drag capture
			if (mouseLeft)
			{
				if (!_wasMouseDown)
				{
					_dragging = true;
					_dragStart = new Point16(tx, ty);
					_dragEnd = _dragStart;
				}
				else if (_dragging)
				{
					_dragEnd = new Point16(tx, ty);
				}
			}
			else if (_wasMouseDown && _dragging)
			{
				CommitPrefab();
				_dragging = false;
			}

			_wasMouseDown = mouseLeft || mouseRight;
		}

		private void CommitPrefab()
		{
			int x0 = System.Math.Min(_dragStart.X, _dragEnd.X);
			int x1 = System.Math.Max(_dragStart.X, _dragEnd.X);
			int y0 = System.Math.Min(_dragStart.Y, _dragEnd.Y);
			int y1 = System.Math.Max(_dragStart.Y, _dragEnd.Y);

			if (x1 - x0 < 1 || y1 - y0 < 1)
			{
				Main.NewText("Drag a larger area to save a prefab.", 255, 200, 80);
				return;
			}

			PrefabToolSystem.SavePrefab(new Point16(x0, y0), new Point16(x1, y1));
			SoundEngine.PlaySound(SoundID.Item4, Player.position);
		}

		public void CancelDrag()
		{
			_dragging = false;
			_wasMouseDown = false;
		}
	}

	public class PrefabToolItem : ModItem
	{
		// Reuse the Structure Anchor item's texture so we don't need a separate asset in Tools/
		public override string Texture => "DynamicWorlds/Preservation/StructureAnchorItem";

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.useTime = 12;
			Item.useAnimation = 18;
			Item.rare = ItemRarityID.Pink;
			Item.value = Item.buyPrice(gold: 1);
			Item.maxStack = 1;
			Item.noMelee = true;
			Item.noUseGraphic = false;
			Item.UseSound = SoundID.Item8;
		}

		public override bool CanUseItem(Player player) => true;
		public override bool ConsumeItem(Player player) => false;

		public override void ModifyTooltips(System.Collections.Generic.List<Terraria.ModLoader.TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "PF1", "Left-click drag: save prefab to current slot (max 10)") { OverrideColor = Color.LimeGreen });
			tooltips.Add(new TooltipLine(Mod, "PF2", "Right-click: paste current prefab at cursor") { OverrideColor = Color.LightSkyBlue });
			tooltips.Add(new TooltipLine(Mod, "PF3", "Shift+Right-click: cycle prefab slot") { OverrideColor = Color.LightBlue });
			tooltips.Add(new TooltipLine(Mod, "PF4", "Slots are per-world and saved with the world") { OverrideColor = Color.Gray });
		}
	}
}