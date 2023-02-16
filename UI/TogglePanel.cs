using ImmersiveProjector.DataStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;

namespace ImmersiveProjector.UI
{
    public class TogglePanel : UIPanel
    {
        public string configName = "";
        public int valueRange;
        public int currentValue;
        public UIText nameText;
        public UIText valueText;

        public UIImageFramed toggleItem;
        public Asset<Texture2D> itemTexture;
        public Rectangle frame;

        public Action<int> onValueChanged;
        public string[] tooltips = null;

        public PropertyInfo bindedValue;
        public ProjectorData bindedObject;

        public TogglePanel(int valueRange, Asset<Texture2D> item, Rectangle startFrame, Asset<Texture2D> background, Asset<Texture2D> border = null) : base(background, border)
        {
            this.valueRange = valueRange;
            this.itemTexture = item;
            this.frame = startFrame;

            PaddingTop = PaddingBottom = 0;

            nameText = new UIText("");
            nameText.VAlign = 0.5f;
            Append(nameText);

            valueText = new UIText("");
            valueText.VAlign = 0.5f;
            valueText.HAlign = 1;
            valueText.Left.Set(-40, 0);
            Append(valueText);

            toggleItem = new UIImageFramed(itemTexture, frame);
            toggleItem.VAlign = 0.5f;
            toggleItem.HAlign = 1;
            Append(toggleItem);
        }

        public override void MouseDown(UIMouseEvent evt)
        {
            base.MouseDown(evt);
        }

        public override void MouseUp(UIMouseEvent evt)
        {
            base.MouseUp(evt);
        }

        public override void MouseOver(UIMouseEvent evt)
        {
            base.MouseOver(evt);
            // SoundEngine.PlaySound(SoundID.MenuTick);
        }

        public override void Click(UIMouseEvent evt)
        {
            base.Click(evt);
            if (valueRange > 0)
            {
                SoundEngine.PlaySound(SoundID.MenuTick);
                currentValue = (currentValue + 1) % valueRange;
                if (onValueChanged != null)
                    onValueChanged(currentValue);
                if (bindedValue != null && bindedObject != null)
                    bindedValue.SetValue(bindedObject, currentValue);
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (bindedValue != null && bindedObject != null)
                currentValue = (int)bindedValue.GetValue(bindedObject);
            BackgroundColor = ProjectorUI.DefaultBackground * (IsMouseHovering ? 1.7f : 1);
            toggleItem.SetFrame(new Rectangle(frame.X + currentValue * frame.Width, frame.Y, frame.Width, frame.Height));
            nameText.SetText(configName);
            if (tooltips != null)
            {
                valueText.SetText(currentValue < tooltips.Length ? tooltips[currentValue] : "IndexOutOfRange");
                if (IsMouseHovering)
                    Main.instance.MouseText(nameText.Text + ": " + valueText.Text);
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);
        }
    }
}
