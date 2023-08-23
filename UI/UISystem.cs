using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using ImmersiveProjector.UI;
using Microsoft.Xna.Framework.Graphics;

namespace ImmersiveProjector
{

    public class UISystem : ModSystem
    {
        public static UISystem Instance { get => ModContent.GetInstance<UISystem>();  }

        public ProjectorUI projectorUIState;
        public UserInterface userInterface;
        public GameTime formerUpdateTime;

        public override void Load()
        {
            projectorUIState = new ProjectorUI();
            projectorUIState.Activate();
            userInterface = new UserInterface();
            userInterface.SetState(null);

            On_ItemSlot.MouseHover_ItemArray_int_int += ItemSlotMouseHoverDecorator;
            On_ItemSlot.LeftClick_ItemArray_int_int += ItemSlotLeftClickDecorator;
            On_ItemSlot.RightClick_ItemArray_int_int += ItemSlotRightClickDecorator;
            On_ChestUI.UpdateHover += ChestHoverDecorator;
            On_Main.DrawInventory += DrawInventoryDecorator;
        }

        public override void Unload()
        {
            On_ItemSlot.MouseHover_ItemArray_int_int -= ItemSlotMouseHoverDecorator;
            On_ItemSlot.LeftClick_ItemArray_int_int -= ItemSlotLeftClickDecorator;
            On_ItemSlot.RightClick_ItemArray_int_int -= ItemSlotRightClickDecorator;
            On_ChestUI.UpdateHover -= ChestHoverDecorator;
            On_Main.DrawInventory -= DrawInventoryDecorator;
            base.Unload();
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (userInterface?.CurrentState != null)
                userInterface.Update(gameTime);
            base.UpdateUI(gameTime);
            formerUpdateTime = gameTime;
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int MouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (MouseTextIndex != -1)
            {
                layers.Insert(MouseTextIndex, new LegacyGameInterfaceLayer("ImmersiveProjector : ProjectorSetting",
                    delegate
                    {
                        if (formerUpdateTime != null && userInterface?.CurrentState != null)
                            userInterface.Draw(Main.spriteBatch, formerUpdateTime);
                        return true;
                    },
                InterfaceScaleType.UI));
            }
            base.ModifyInterfaceLayers(layers);
        }

        public bool MouseBlocked(bool ignoreHandle = false)
        {
            UIElement element = projectorUIState.GetElementAt(new Vector2(Main.mouseX, Main.mouseY));
            if (userInterface.CurrentState != null && element != null && element.GetType() != typeof(ProjectorUI) && (!ignoreHandle || element.GetType() != typeof(HandleGridImageButton)))
            {
                // Main.NewText(element);
                return true;
            }
            return false;
        }

        public void ItemSlotMouseHoverDecorator(On_ItemSlot.orig_MouseHover_ItemArray_int_int orig,
                                                Item[] inv, int context, int slot)
        {
            Item i = inv[slot];
            // Main.NewText(String.Format("{0} {1} {2}", inv.Length, context, slot));
            if (!i.IsAir)
            {
                // Main.NewText(i.Name);
                // return;
            }
            orig(inv, context, slot);
        }

        public bool ItemSlotOverrideLeftClickDecorator(On_ItemSlot.orig_OverrideLeftClick orig,
                                                       Item[] inv, int context, int slot)
        {
            return orig(inv, context, slot);
        }

        public void ItemSlotLeftClickDecorator(On_ItemSlot.orig_LeftClick_ItemArray_int_int orig,
                                               Item[] inv, int context, int slot)
        {
            Item i = inv[slot];
            // Main.NewText(String.Format("{0} {1} {2}", inv.Length, context, slot));
            if (!i.IsAir)
            {
                // Main.NewText(i.Name);
                // return;
            }
            orig(inv, context, slot);
        }

        public void ItemSlotRightClickDecorator(On_ItemSlot.orig_RightClick_ItemArray_int_int orig,
                                                Item[] inv, int context, int slot)
        {
            orig(inv, context, slot);
        }
        public void ChestHoverDecorator(On_ChestUI.orig_UpdateHover orig, int ID, bool hovering)
        {
            if (MouseBlocked())
                hovering = false;
            orig(ID, hovering);
        }

        public void DrawInventoryDecorator(On_Main.orig_DrawInventory orig, Main self)
        {
            var orig_itemAnimation = Main.LocalPlayer.itemAnimation;
            if (MouseBlocked())
            {
                // Main.NewText(element.GetType());
                Main.LocalPlayer.itemAnimation = 100;
                // A hack to prevent mouse interaction with inventory UI
            }
            // Main.LocalPlayer.mouseInterface = false;
            orig(self);
            Main.LocalPlayer.itemAnimation = orig_itemAnimation;
        }
    }
}
