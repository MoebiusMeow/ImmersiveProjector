using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ImmersiveProjector.Tiles;

namespace ImmersiveProjector.Items
{
    public class MiniProjector : ModItem
    {
        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Hookshot Spot");
            // Tooltip.SetDefault("Placeable hookshot spot.");
        }

        public override void SetDefaults()
        {
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.autoReuse = true;
            Item.maxStack = 999;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<MiniProjectorTile>();
            Item.width = 32;
            Item.height = 32;
        }
        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
			recipe.Register();
		}
    }
}
