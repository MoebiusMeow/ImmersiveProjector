using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ImmersiveProjector.Tiles;
using Terraria.GameContent.Creative;

namespace ImmersiveProjector.Items
{
    public class MiniProjector : ModItem
    {
        public override void SetStaticDefaults()
        {
            CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId[Type] = 1;
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
            recipe.AddRecipeGroup("IronBar");
            recipe.AddIngredient(ItemID.Glass);
            recipe.AddIngredient(ItemID.FallenStar, 3);
            recipe.AddTile(TileID.WorkBenches);
            recipe.Register();
        }
    }
}
