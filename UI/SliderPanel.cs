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
using Terraria.Localization;
using Terraria.UI;

namespace ImmersiveProjector.UI
{
	public class SliderPanel : UIPanel
	{
		public string configName = "";
		public string tooltip = "";
		public float valueMin;
		public float valueMax;
		public float currentValue;
		public float valueSnapping = 5;
		public UIText nameText;
		public UIText valueText;

		public UIImageFramed toggleItem;
		UIColoredSlider slider;
		public Rectangle frame;

		public Action<float> onValueChanged;

		public PropertyInfo bindedValue;
		public ProjectorData bindedObject;

		public SliderPanel(float valueMin, float valueMax, Color color, Asset<Texture2D> background, Asset<Texture2D> border = null) : base(background, border)
		{
			this.valueMin = valueMin;
			this.valueMax = valueMax;
			currentValue = valueMin;

			PaddingTop = PaddingBottom = 0;

			slider = new UIColoredSlider(LocalizedText.Empty,
				() => (currentValue - this.valueMin) / (this.valueMax - this.valueMin),
				(float v) =>
				{
					currentValue = (1 - v) * this.valueMin + v * this.valueMax;
					currentValue = Utils.Clamp(MathF.Round(currentValue / valueSnapping) * valueSnapping, this.valueMin, this.valueMax);
					if (onValueChanged != null)
						onValueChanged(currentValue);
                    if (bindedValue != null && bindedObject != null)
                        bindedValue.SetValue(bindedObject, currentValue);
				},
				() => { }, (float v) => color * v, Color.White);
			slider.Width.Set(0, 1);
			slider.Height.Set(0, 1);
			slider.HAlign = 1;
			slider.Left.Set(10, 0);
			Append(slider);

			nameText = new UIText("");
			nameText.VAlign = 0.5f;
			nameText.IgnoresMouseInteraction = true;
			Append(nameText);

			valueText = new UIText("", 0.7f);
			valueText.VAlign = 0.5f;
			valueText.HAlign = 1;
			valueText.Left.Set(-0, 0);
			valueText.Top.Set(10, 0);
			valueText.IgnoresMouseInteraction = true;
			Append(valueText);
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
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (bindedValue != null && bindedObject != null)
				currentValue = (float)bindedValue.GetValue(bindedObject);
			BackgroundColor = ProjectorUI.DefaultBackground * (IsMouseHovering ? 1.7f : 1);
			nameText.SetText(configName);
			if (tooltip.Length > 0)
			{
				valueText.SetText(currentValue.ToString("0.00"));
				if (IsMouseHovering)
                    Main.instance.MouseText(tooltip + ": " + valueText.Text);
			}
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			base.Draw(spriteBatch);
		}
	}
}
