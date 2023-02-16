using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Reflection;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.Graphics.Capture;
using Terraria.ModLoader;
using Terraria.ModLoader.UI.Elements;
using Terraria.UI;

namespace ImmersiveProjector.UI
{
    public class HandleGridImageButton : UIImageFramed
    {
        public Asset<Texture2D> asset;
        public int styleX;
        public int styleY;
        public int snapping;

        public Vector2 inGamePosition;

        public bool dragging;
        protected Vector2 draggingAnchor;

        public bool visible = true;
        public Func<bool> ActiveFunc = null;

        public FieldInfo cameraLerp;
        public Vector2 predictedScreenPosition;

        public HandleGridGroup group;


        public HandleGridImageButton(Asset<Texture2D> asset, int styleX, int styleY, Vector2 inGamePosition, int snapping = 1) : base(asset, asset.Frame(6, 3, styleX, styleY))
        {
            this.asset = asset;
            this.styleX = styleX;
            this.styleY = styleY;
            this.inGamePosition = inGamePosition;
            this.snapping = snapping;
            HAlign = VAlign = 0.5f;
            Left.Precent = -0.5f;
            Top.Precent = -0.5f;
        }

        public void SetActiveFunc(Func<bool> f)
        {
            ActiveFunc = f;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Main.inFancyUI)
            {
                IgnoresMouseInteraction = true;
                dragging = false;
                return;
            }

            SetFrame(asset.Frame(6, 3, styleX + (dragging || IsMouseHovering ? 3 : 0), styleY));


            if (ActiveFunc != null)
                IgnoresMouseInteraction = !ActiveFunc() || !visible;

            if (ContainsPoint(Main.MouseScreen) && !IgnoresMouseInteraction)
            {
                Main.LocalPlayer.mouseInterface = true;
            }

            var screenPosition = Main.screenPosition;
            Vector2 zoomOffset = new Vector2(Main.Transform.Translation.X, Main.Transform.Translation.Y);
            if (dragging)
            {
                Vector2 newPosition = ((new Vector2(Main.mouseX, Main.mouseY) - zoomOffset / Main.UIScale) / Main.GameZoomTarget - draggingAnchor) * Main.UIScale + screenPosition;
                inGamePosition.X = Utils.Clamp(snapping * MathF.Round(newPosition.X / snapping), 0, Main.maxTilesX * 16f);
                inGamePosition.Y = Utils.Clamp(snapping * MathF.Round(newPosition.Y / snapping), 0, Main.maxTilesY * 16f);
                if (styleX == 0) inGamePosition.X = MathF.Min(inGamePosition.X, group.bottomRight.X - snapping);
                if (styleX == 2) inGamePosition.X = MathF.Max(inGamePosition.X, group.topLeft.X + snapping);
                if (styleY == 0) inGamePosition.Y = MathF.Min(inGamePosition.Y, group.bottomRight.Y - snapping);
                if (styleY == 2) inGamePosition.Y = MathF.Max(inGamePosition.Y, group.topLeft.Y + snapping);
                group.FromHandlePositions(this);
            }

            Left.Pixels = ((inGamePosition.X - screenPosition.X ) * Main.GameZoomTarget + zoomOffset.X) / Main.UIScale; 
            Top.Pixels = ((inGamePosition.Y - screenPosition.Y) * Main.GameZoomTarget + zoomOffset.Y) / Main.UIScale;
            predictedScreenPosition = screenPosition;

            Recalculate();

            ClampParent();
        }

        public void ClampParent()
        {
            if (Parent == null) return;
            var parentSpace = Parent.GetDimensions().ToRectangle();
            if (styleX == 1 && styleY != 1) Left.Pixels = Utils.Clamp(Left.Pixels, 0, parentSpace.Right);
            if (styleX != 1 && styleY == 1) Top.Pixels = Utils.Clamp(Top.Pixels, 0, parentSpace.Bottom);
            Recalculate();
        }

        public void SetVisibility(bool visible)
        {
            this.visible = visible;
            if (!visible) dragging = false;
        }


        public override void MouseOver(UIMouseEvent evt)
        {
            base.MouseOver(evt);
        }

        public override void MouseOut(UIMouseEvent evt)
        {
            base.MouseOut(evt);
        }

        public override void MouseDown(UIMouseEvent evt)
        {
            base.MouseDown(evt);
            if (UISystem.Instance.MouseBlocked(true))
                return;
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

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!visible) return;
            if (ActiveFunc != null && !ActiveFunc()) return;
            ModContent.GetInstance<ProjectorSystem>().delayedSpriteDraw += DelayedDrawCall;
        }

        public void DelayedDrawCall(SpriteBatch spriteBatch)
        {
            // Delay UI rendering to post-update
            // screenPosition is changed during update procedure
            Vector2 offset = dragging ? Vector2.Zero : (Main.screenPosition - predictedScreenPosition) * Main.GameZoomTarget / Main.UIScale;
            if (offset.Length() > 0)
            {
                Top.Pixels -= offset.Y;
                Left.Pixels -= offset.X;
                Recalculate();
            }
            base.Draw(spriteBatch);
        }
    }
}
