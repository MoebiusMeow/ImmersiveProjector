using ImmersiveProjector.DataStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Steamworks;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;

namespace ImmersiveProjector.UI
{
	public class MapHintPanel : UIPanel
	{
		public ProjectorInstance projector;

		public MapHintPanel(Asset<Texture2D> asset) : base(null, asset, 12, 8)
		{
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			base.Draw(spriteBatch);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{

            RasterizerState rasterizerState = spriteBatch.GraphicsDevice.RasterizerState;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, rasterizerState , null, Main.UIScaleMatrix);

			Rectangle current = GetDimensions().ToRectangle();
			Vector2 topLeft = Main.screenPosition / 16 - current.Size();
			Vector2 bottomRight = topLeft + current.Size() * 2;
			if (projector != null && projector.data != null)
			{
				topLeft = projector.data.sourceTopLeft;
				bottomRight = projector.data.sourceBottomRight;
				Vector2 targetTopLeft = projector.data.targetTopLeft;
				Vector2 targetBottomRight = projector.data.targetBottomRight;
				topLeft.X = MathF.Min(topLeft.X, targetTopLeft.X);
				bottomRight.X = MathF.Max(bottomRight.X, targetBottomRight.X);
				topLeft.Y = MathF.Min(topLeft.Y, targetTopLeft.Y);
				bottomRight.Y = MathF.Max(bottomRight.Y, targetBottomRight.Y);
				topLeft = topLeft / 16 - Vector2.One * 10;
				bottomRight = bottomRight / 16 + Vector2.One * 10;
			}

			Vector2 size = bottomRight - topLeft;
			topLeft -= size * 0.1f;
			bottomRight += size * 0.1f;
			size *= 1.2f;
			// Scale map to contain all POI
			float scale = MathF.Min(current.Width / size.X, current.Height / size.Y);
			// Fill rest of the panel
			if (size.X * scale < current.Width)
			{
				var d = current.Width / scale - size.X;
				size.X += d;
				topLeft.X -= d * 0.5f;
				bottomRight.X += d * 0.5f;
			}
			if (size.Y * scale < current.Height)
			{
				var d = current.Height / scale - size.Y;
				size.Y += d;
				topLeft.Y -= d * 0.5f;
				bottomRight.Y += d * 0.5f;
			}

			spriteBatch.Draw(TextureAssets.MagicPixel.Value, current, Color.Black);
			for (int i = 0; i < Main.mapTargetX; i++)
				for (int j = 0; j < Main.mapTargetY; j++)
				{
					RenderTarget2D mapTarget = Main.instance.mapTarget[i, j];
					if (mapTarget != null && !mapTarget.IsContentLost)
					{
						Vector2 targetPos = new Vector2(i * mapTarget.Width, j * mapTarget.Height);
						float l = MathF.Max(topLeft.X, targetPos.X);
						float r = MathF.Min(bottomRight.X, targetPos.X + mapTarget.Width);
						float t = MathF.Max(topLeft.Y, targetPos.Y);
						float b = MathF.Min(bottomRight.Y, targetPos.Y + mapTarget.Height);
						Vector2 offset = new Vector2(MathF.Max(l - targetPos.X, 0), MathF.Max(t - targetPos.Y, 0));
						Rectangle frame = new Rectangle((int)offset.X, (int)offset.Y, (int)(r - l), (int)(b - t));
						if (frame.Width <= 0 || frame.Height <= 0)
							continue;
						Vector2 fracOffset = mapTarget.Size() * scale - frame.BottomRight() * scale;
						if (r >= bottomRight.X) fracOffset.X = 0;
						if (b >= bottomRight.Y) fracOffset.Y = 0;
                        spriteBatch.Draw(mapTarget, (new Vector2(l, t) - topLeft) * scale + fracOffset + 0 * (offset - frame.TopLeft()) + current.TopLeft(), frame, Color.White, 0, Vector2.Zero,
							scale, SpriteEffects.None, 0);
                        // spriteBatch.Draw(mapTarget, current.TopLeft(), mapTarget.Frame(), Color.White, 0, Vector2.Zero, 0.5f, SpriteEffects.None, 0);
                        // spriteBatch.Draw(TextureAssets.Map.Value, current.TopLeft(), mapTarget.Frame(), Color.White, 0, Vector2.Zero, 0.1f, SpriteEffects.None, 0);
					}
				}
			if (projector != null && projector.data != null)
			{
				var sys = ModContent.GetInstance<ProjectorSystem>();
				sys.QuickDrawBoxLocal((projector.data.sourceTopLeft - topLeft * 16) / 16 * scale + current.TopLeft(), projector.data.sourceSize / 16 * scale, Color.Cyan);
				sys.QuickDrawBoxLocal((projector.data.targetTopLeft - topLeft * 16) / 16 * scale + current.TopLeft(), projector.data.targetSize / 16 * scale, Color.Orange);
				var playerTopLeft = Main.LocalPlayer.TopLeft;
				playerTopLeft = new Vector2(MathF.Max(playerTopLeft.X, topLeft.X * 16 + 10), MathF.Max(playerTopLeft.Y, topLeft.Y * 16 + 10));
				var playerBottomRight = Main.LocalPlayer.BottomRight;
				playerBottomRight = new Vector2(MathF.Min(playerBottomRight.X, bottomRight.X * 16 - 10), MathF.Min(playerBottomRight.Y, bottomRight.Y * 16 - 10));
				sys.QuickDrawBoxLocal((playerTopLeft - topLeft * 16) / 16 * scale + current.TopLeft(), (playerBottomRight - playerTopLeft) / 16 * scale, Color.LightGreen);
				sys.QuickDashLineLocal
                (
                    (projector.data.sourcePoint - topLeft * 16) / 16 * scale + current.TopLeft(),
                    (projector.data.targetPoint - topLeft * 16) / 16 * scale + current.TopLeft(),
                    Color.White
                );
            }
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, rasterizerState, null, Main.UIScaleMatrix);
			base.DrawSelf(spriteBatch);
            // spriteBatch.Draw(TextureAssets.Map.Value, Vector2.Zero, TextureAssets.Map.Frame(), Color.White, 0, Vector2.Zero, 0.01f, SpriteEffects.None, 0);
        }
    }
}
