using Terraria.ID;
using ImmersiveProjector.DataStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.UI.Elements;
using Terraria.UI;

namespace ImmersiveProjector.UI
{
    public class PresetFrame : UIElement, IColorable
    {
        public int presetTypeKey = 0;
        public string presetName = "";

        public ProjectorData presetData = null;

        Asset<Texture2D> backTexture;
        Asset<Texture2D> borderTexture;
        Asset<Texture2D> HoverBorderTexture;

        public float alpha = 1f;
        public bool selected = false;

        public UIText nameDisplay;
        public UIElement imageDisplay;

        private Color _color = Color.White;
        public Color Color { get => _color; set => _color = value; }

        public PresetFrame(Asset<Texture2D> frame, Asset<Texture2D> hover,
                           Asset<Texture2D> bottom, Asset<Texture2D> content, int presetTypeKey = 0, string presetName = "NoName")
        {
            backTexture = bottom;
            borderTexture = frame;
            HoverBorderTexture = hover;

            Width.Set(borderTexture.Width(), 0);
            Height.Set(borderTexture.Height(), 0);
            MarginBottom = 10;
            MarginLeft = MarginRight = 3;

            this.presetTypeKey = presetTypeKey;
            this.presetName = presetName;

            UIElement imageContainer = new UIElement();
            imageContainer.HAlign = imageContainer.VAlign = 0.5f;
            imageContainer.Width.Set(-4, 1);
            imageContainer.Height.Set(-4, 1);
            // imageContainer.OverflowHidden = true;
            imageDisplay = new UIImage(content);
            imageDisplay.HAlign = imageDisplay.VAlign = 0.5f;
            imageContainer.Append(imageDisplay);
            imageContainer.IgnoresMouseInteraction = true;
            Append(imageContainer);

            nameDisplay = new UIText(ShortString(presetName, 12), 1.0f);
            nameDisplay.HAlign = 0.5f;
            nameDisplay.VAlign = 0f;
            nameDisplay.Top.Set(5, 1);
            nameDisplay.Recalculate();

            nameDisplay.SetText(nameDisplay.Text, MathF.Min(1f, Width.Pixels / nameDisplay.MinWidth.Pixels), false);
            Append(nameDisplay);
        }

        public string ShortString(string s, int maxLength = 10)
        {
            string result = s;
            int index = -1;
            int lastRemove = 0;
            while (result.Length > maxLength && lastRemove <= result.Length)
            {
                index = (index + result.Length) % result.Length;
                lastRemove += 1;
                if ("aeiouAEIOU".IndexOf(result[index]) != -1 && index != 0 && char.IsLetter(result[index - 1]))
                {
                    result = result.Remove(index, 1);
                    lastRemove = 0;
                }
                index -= 1;
            }
            if (result.Length > maxLength && maxLength > 5)
            {
                int l = (int)Math.Floor((maxLength - 3) * 0.5);
                result = string.Concat(result.AsSpan(0, l), "...", result.AsSpan(result.Length - l, l));
            }
            return result;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dimensions = GetDimensions();
            spriteBatch.Draw(backTexture.Value, dimensions.Position(), Color * (IsMouseHovering || selected ? 1 : 0.7f));
            spriteBatch.Draw(borderTexture.Value, dimensions.Position(), Color * (IsMouseHovering || selected ? 1 : 0.4f));
            if (HoverBorderTexture != null && selected)
            {
                spriteBatch.Draw(HoverBorderTexture.Value, dimensions.Position(), Color);
            }
        }

        public override void MouseOver(UIMouseEvent evt)
        {
            base.MouseOver(evt);
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        public override void MouseOut(UIMouseEvent evt)
        {
            base.MouseOut(evt);
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            if (Parent.Parent is PresetGrid grid)
            {
                grid.handleClick(this);
            }
        }

        public void SetTypeKey(int presetTypeKey)
        {
            this.presetTypeKey = presetTypeKey;
        }

        public void SetPresetName(string presetName)
        {
            this.presetName = presetName;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            ((UIImage)imageDisplay).Color = Color.White * (IsMouseHovering || selected ? 1 : 0.6f);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);
        }

        public override int CompareTo(object obj)
        {
            PresetFrame other = obj as PresetFrame;
            if (other == null)
            {
                if (obj is UIHorizontalSeparator sep)
                    return presetTypeKey < 0 ? -1 : 1;
                return -1;
            }
            if (presetTypeKey != other.presetTypeKey)
                return presetTypeKey < other.presetTypeKey ? -1 : 1;
            return string.Compare(presetName, other.presetName);
        }
    }
}
