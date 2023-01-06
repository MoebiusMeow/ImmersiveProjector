using ImmersiveProjector.Items;
using ImmersiveProjector.UI;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace ImmersiveProjector.Tiles
{
	public class MiniProjectorTile : ModTile
	{
		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileLavaDeath[Type] = true;
			Main.tileNoAttach[Type] = true;
			TileID.Sets.FramesOnKillWall[Type] = true;
			TileID.Sets.HasOutlines[Type] = true;
			TileID.Sets.DisableSmartCursor[Type] = true;

			TileObjectData.newTile.CopyFrom(TileObjectData.StyleSwitch);
			var placementHook = new PlacementHook(ModContent.GetInstance<ProjectorTileEntity>().Hook_AfterPlacement, -1, 0, true);
			TileObjectData.newTile.HookPostPlaceMyPlayer = placementHook;
			TileObjectData.newTile.StyleHorizontal = true;
			TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
			// TileObjectData.newTile.StyleMultiplier = 5;

			/*
			TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
			TileObjectData.addAlternate(0);
			*/

            TileObjectData.newAlternate.CopyFrom(TileObjectData.StyleSwitch);
			TileObjectData.newAlternate.AnchorLeft = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.Tree | AnchorType.AlternateTile, TileObjectData.newTile.Height, 0);
			TileObjectData.newAlternate.AnchorAlternateTiles = new[] { 124 };
			TileObjectData.newAlternate.HookPostPlaceMyPlayer = placementHook;
			TileObjectData.addAlternate(1);

            TileObjectData.newAlternate.CopyFrom(TileObjectData.StyleSwitch);
			TileObjectData.newAlternate.AnchorRight = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.Tree | AnchorType.AlternateTile, TileObjectData.newTile.Height, 0);
			TileObjectData.newAlternate.AnchorAlternateTiles = new[] { 124 };
			TileObjectData.newAlternate.HookPostPlaceMyPlayer = placementHook;
			TileObjectData.addAlternate(2);

            TileObjectData.newAlternate.CopyFrom(TileObjectData.StyleSwitch);
			TileObjectData.newAlternate.AnchorWall = true;
			TileObjectData.newAlternate.HookPostPlaceMyPlayer = placementHook;
			TileObjectData.addAlternate(3);

			TileObjectData.addTile(Type);
			AddMapEntry(Microsoft.Xna.Framework.Color.Aqua, ImmersiveProjector.ModTranslateL("MiniProjectorTile", "MapObject."));
		}

		public override void ModifySmartInteractCoords(ref int width, ref int height, ref int frameWidth, ref int frameHeight, ref int extraY)
		{
			width = 1;
			height = 1;
			frameWidth = 1;
			frameHeight = 1;
			extraY = 0;
		}

		public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings)
		{
			return true;
		}

		public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
		{
			Item.NewItem(new EntitySource_TileBreak(i, j), i * 16, j * 16, 32, 32, ModContent.ItemType<MiniProjector>());
			ModContent.GetInstance<ProjectorTileEntity>().Kill(i, j);
			base.KillTile(i, j, ref fail, ref effectOnly, ref noItem);
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<MiniProjector>();
		}
        public override void HitWire(int i, int j)
		{
			Tile tile = Main.tile[i, j];
			int style = tile.TileFrameY / 18;
			TileEntity.ByPosition.TryGetValue(new Point16(i, j), out TileEntity te);
			if (te is ProjectorTileEntity p)
				style = p.data.turnedOn;

			tile.TileFrameY = (short)((style ^ 1) * 18);

			if (te is ProjectorTileEntity projector)
			{
				projector.data.turnedOn = (tile.TileFrameY >= 18).ToInt();
				projector.MarkNetUpdate();
				// UISystem.Instance.userInterface.SetState(UISystem.Instance.projectorUIState);
			}
		}

		public override bool RightClick(int i, int j)
		{
			TileEntity.ByPosition.TryGetValue(new Point16(i, j), out TileEntity te);
			if (te is ProjectorTileEntity projector)
			{
				if (UISystem.Instance.userInterface.CurrentState != UISystem.Instance.projectorUIState)
				{
					SoundEngine.PlaySound(SoundID.MenuOpen);
					UISystem.Instance.userInterface.SetState(UISystem.Instance.projectorUIState);
					UISystem.Instance.projectorUIState.FocusInstance(projector.projectorInstance);
				}
				else
				{
					SoundEngine.PlaySound(SoundID.MenuClose);
					UISystem.Instance.projectorUIState.AnimatedClose();
				}
			}
			else
			{
				SoundEngine.PlaySound(SoundID.Drown with { Volume = 0.3f, MaxInstances = 1, Pitch = 1.0f });
			}
			return true;
		}
	}
}
