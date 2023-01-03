using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Diagnostics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.ModLoader.UI.Elements;
using Terraria.UI;

namespace ImmersiveProjector.UI
{
	public class HandleGridGroup
	{
		public Asset<Texture2D> asset;
		public int snapping;

		public HandleGridImageButton[,] buttons;
		public int draggingStyleX;
		public int draggingStyleY;
		public bool draggingExpanding;

		public Vector2 anchorPosition;

		public Vector2 topLeft;
		public Vector2 bottomRight;

		public Vector2 Size => bottomRight - topLeft;

		public enum TransformType
		{
			FreeTransform,
			ScaleAndMove,
			Disabled
		}
		public TransformType transformType = TransformType.Disabled;

        public HandleGridGroup(Asset<Texture2D> asset,Vector2 anchorPosition, Vector2 anchorPrecentage, Vector2 size, Func<bool> activeFunc, int snapping = 1)
        {
			SetFromAnchorAndSize(anchorPosition, anchorPrecentage, size);
			buttons = new HandleGridImageButton[3, 3];
			for (var i = 0; i < 3; i++)
				for (var j = 0; j < 3; j++)
				{
					buttons[i, j] = new HandleGridImageButton(asset, i, j, GetPosition9Grid(i, j), snapping);
					buttons[i, j].SetActiveFunc(activeFunc);
					buttons[i, j].group = this;
				}
        }

		public void AddChildrenTo(UIElement parent)
		{
			for (var i = 0; i < 3; i++)
				for (var j = 0; j < 3; j++)
					if (i != 1 || j != 1)
						parent.Append(buttons[i, j]);
            parent.Append(buttons[1, 1]);
			parent.OnUpdate += Parent_OnUpdate;
		}

		private void Parent_OnUpdate(UIElement affectedElement)
		{
			if (buttons[0, 0].Parent == null) return;
			if (buttons[0, 0].ActiveFunc != null && !buttons[0, 0].ActiveFunc()) return;
			for (var i = 0; i < 3; i += 2)
			{
				buttons[i, 1].ClampParent();
				if (buttons[i, 1].dragging) continue;
				buttons[i, 1].SetVisibility(buttons[i, 1].Top.Pixels > buttons[i, 0].Top.Pixels
										 && buttons[i, 1].Top.Pixels < buttons[i, 2].Top.Pixels);
			}
            for (var j = 0; j < 3; j += 2)
			{
				buttons[1, j].ClampParent();
				if (buttons[1, j].dragging) continue;
				buttons[1, j].SetVisibility(buttons[1, j].Left.Pixels > buttons[0, j].Left.Pixels
										 && buttons[1, j].Left.Pixels < buttons[2, j].Left.Pixels);
			}
		}

		public void SetFromAnchorAndSize(Vector2 anchorPosition, Vector2 anchorPrecentage, Vector2 size)
		{
			anchorPosition -= (anchorPrecentage - Vector2.One * 0.5f) * size;
			anchorPrecentage = Vector2.One * 0.5f;
			this.anchorPosition = anchorPosition;
			this.topLeft = anchorPosition - size * anchorPrecentage;
			this.bottomRight = anchorPosition + size * (Vector2.One - anchorPrecentage);
		}

		public void SetSnapping(int snapping)
		{
			this.snapping = snapping;
			for (var i = 0; i < 3; i++)
				for (var j = 0; j < 3; j++)
					buttons[i, j].snapping = snapping;
		}

		public void UpdateHandlePositions()
		{
			for (var i = 0; i < 3; i++)
				for (var j = 0; j < 3; j++)
					buttons[i, j].inGamePosition = GetPosition9Grid(i, j);
		}

		public void FromHandlePositions(HandleGridImageButton dragging)
		{
			if (transformType == TransformType.Disabled)
				return;
			Vector2 oldSize = bottomRight - topLeft;
			draggingStyleX = dragging.styleX;
			draggingStyleY = dragging.styleY;
            if (dragging.styleX == 0) topLeft.X = dragging.inGamePosition.X;
            if (dragging.styleX == 2) bottomRight.X = dragging.inGamePosition.X;
            if (dragging.styleY == 0) topLeft.Y = dragging.inGamePosition.Y;
            if (dragging.styleY == 2) bottomRight.Y = dragging.inGamePosition.Y;
			if (!(dragging.styleX == 1 && dragging.styleY == 1))
			{
				if (transformType == TransformType.ScaleAndMove)
				{
                    Vector2 newSize = (bottomRight - topLeft) * 2 - oldSize;
                    newSize.X = MathF.Max(newSize.X, MathF.Max(8, 2 * snapping));
                    newSize.Y = MathF.Max(newSize.Y, MathF.Max(8, 2 * snapping));
					draggingExpanding = newSize.X * newSize.Y > oldSize.X * oldSize.Y;
                    float factor = MathF.Max(dragging.styleX == 1 ? 0 : newSize.X / oldSize.X, dragging.styleY == 1 ? 0 : newSize.Y / oldSize.Y);
					Debug.Assert(factor > 0);
					SetFromAnchorAndSize(buttons[1, 1].inGamePosition, Vector2.One * 0.5f, oldSize * factor);
				}
				else if (transformType == TransformType.FreeTransform)
				{
					if (Main.LocalPlayer.controlSmart)
					{
                        Vector2 newSize = (bottomRight - topLeft) * 2 - oldSize;
                        newSize.X = MathF.Max(newSize.X, MathF.Max(8, 2 * snapping));
                        newSize.Y = MathF.Max(newSize.Y, MathF.Max(8, 2 * snapping));
                        draggingExpanding = newSize.X * newSize.Y > oldSize.X * oldSize.Y;
                        SetFromAnchorAndSize(buttons[1, 1].inGamePosition, Vector2.One * 0.5f, newSize);
					}
					else
					{
                        Vector2 newSize = (bottomRight - topLeft);
                        newSize.X = MathF.Max(newSize.X, MathF.Max(8, 2 * snapping));
                        newSize.Y = MathF.Max(newSize.Y, MathF.Max(8, 2 * snapping));
                        draggingExpanding = newSize.X * newSize.Y > oldSize.X * oldSize.Y;
                        SetFromAnchorAndSize(buttons[2 - dragging.styleX, 2 - dragging.styleY].inGamePosition,
											 new Vector2(2 - dragging.styleX, 2 - dragging.styleY) * 0.5f, newSize);
						if (UISystem.Instance.projectorUIState.sourceGrid == this)
						{
						}
					}
				}

			}
			if (dragging.styleX == 1 && dragging.styleY == 1)
				SetFromAnchorAndSize(dragging.inGamePosition, Vector2.One * 0.5f, bottomRight - topLeft);
		}

		public Vector2 GetPosition9Grid(int i, int j)
		{
			return new Vector2(topLeft.X * (2 - i) + bottomRight.X * i, topLeft.Y * (2 - j) + bottomRight.Y * j) * 0.5f;
		}
    }
}
