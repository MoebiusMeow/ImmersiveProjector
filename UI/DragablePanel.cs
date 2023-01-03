using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace ImmersiveProjector.UI
{
	public class DragablePanel : UIPanel
	{
		public bool dragging;
		public Vector2 draggingAnchor;

		public override void MouseDown(UIMouseEvent evt)
		{
			base.MouseDown(evt);
			UIElement element = GetElementAt(evt.MousePosition);
			if (element is UIScrollbar) return;
			if (element is UIColoredSlider) return;
			if (element is SliderPanel) return;
			if (!dragging)
			{
				dragging = true;
				draggingAnchor = new Vector2(evt.MousePosition.X - Left.Pixels, evt.MousePosition.Y - Top.Pixels);
			}
		}

		public override void MouseUp(UIMouseEvent evt)
		{
			base.MouseUp(evt);
			dragging = false;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			if (ContainsPoint(Main.MouseScreen))
			{
				Main.LocalPlayer.mouseInterface = true;
			}

			if (dragging)
			{
				Left.Set(Main.mouseX - draggingAnchor.X, 0f); 
				Top.Set(Main.mouseY - draggingAnchor.Y, 0f);
				Recalculate();
			}

			// MarginRight = MarginLeft = MarginBottom = MarginTop = 0;
			var parentSpace = Parent.GetDimensions().ToRectangle();
            Left.Pixels = Utils.Clamp(Left.Pixels, 0, parentSpace.Right - Width.Pixels - 0);
            Top.Pixels = Utils.Clamp(Top.Pixels, 0, parentSpace.Bottom - Height.Pixels - 0);
            Recalculate();

		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			base.Draw(spriteBatch);
		}
	}
}
