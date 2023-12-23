using ImmersiveProjector.DataStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.GameContent;
using System.Linq;
using Terraria.Graphics.Capture;
using Terraria.ModLoader;

namespace ImmersiveProjector
{
    class ProjectorUtils
    {
        public static UISystem uiSystem { get => ModContent.GetInstance<UISystem>(); }
        public static Effect projectorRangeDisplayEffect;
        public static Effect projectorFilterEffect;
        public static SpriteBatchHack spriteBatchHack = new SpriteBatchHack();
        public static void EnsureRT(float width, float height, ref RenderTarget2D projectorRT)
        {
            if (projectorRT == null || projectorRT.Width < width || projectorRT.Height < height)
            {
                if (projectorRT != null && !projectorRT.IsDisposed)
                    projectorRT.Dispose();
                projectorRT = new RenderTarget2D(Main.graphics.GraphicsDevice,
                    Math.Max(projectorRT != null ? projectorRT.Width : 100, (int)Math.Ceiling(width)),
                    Math.Max(projectorRT != null ? projectorRT.Height : 100, (int)Math.Ceiling(height)),
                    false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
            }
        }

        public static void QuickDrawBox(Vector2 position, Vector2 size, Color color)
        {
            QuickDrawBoxLocal(position - Main.screenPosition, size, color);
        }

        public static void QuickDrawBoxLocal(Vector2 position, Vector2 size, Color color)
        {
            Rectangle simpleRect = new Rectangle(0, 0, 1, 1);
            size.X = MathF.Max(0, size.X);
            size.Y = MathF.Max(0, size.Y);
            Vector2 UnitX = Vector2.UnitX * size / 16f;
            Vector2 UnitY = Vector2.UnitY * size / 16f;
            Vector2 SX = UnitX + Vector2.UnitY;
            Vector2 SY = UnitY + Vector2.UnitX;

            SpriteBatch spriteBatch = Main.spriteBatch;
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, position + Vector2.UnitX * -2f, simpleRect, color, 0f, Vector2.Zero, new Vector2(2f, 16f) * SY, SpriteEffects.None, 0f);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, position + UnitX * 16f, simpleRect, color, 0f, Vector2.Zero, new Vector2(2f, 16f) * SY, SpriteEffects.None, 0f);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, position + Vector2.UnitY * -2f, simpleRect, color, 0f, Vector2.Zero, new Vector2(16f, 2f) * SX, SpriteEffects.None, 0f);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, position + UnitY * 16f, simpleRect, color, 0f, Vector2.Zero, new Vector2(16f, 2f) * SX, SpriteEffects.None, 0f);
        }

        public static void QuickDrawLineLocal(Vector2 from, Vector2 to, Color color)
        {
            SpriteBatch spriteBatch = Main.spriteBatch;
            Rectangle simpleRect = new Rectangle(
                0 + (int)(512 * (Main.timeForVisualEffects * 0.01 - Math.Floor(Main.timeForVisualEffects * 0.01))) % 512,
                0,
                (int)((to - from).Length() * 2),
                2
            );
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, to, simpleRect, color * 0.5f, MathF.Atan2(-to.Y + from.Y, -to.X + from.X),
                Vector2.UnitY * 0.5f, new Vector2(1 / 2f, 1f), SpriteEffects.None, 0f);
            spriteBatch.Draw(TextureAssets.Extra[178].Value, to, simpleRect, color * 0.5f, MathF.Atan2(-to.Y + from.Y, -to.X + from.X),
                Vector2.UnitY * 0.5f, new Vector2(1 / 2f, 3f), SpriteEffects.None, 0f);
        }

        public static void QuickDrawLine(Vector2 from, Vector2 to, Color color)
        {
            QuickDrawLineLocal(from - Main.screenPosition, to - Main.screenPosition, color);
            if (from != to && false)
            {
                Vector2 dir = Vector2.Normalize(to - from) * 10;
                QuickDrawLineLocal(to - dir.RotatedBy(MathF.PI / 10) - Main.screenPosition, to - Main.screenPosition, color);
                QuickDrawLineLocal(to - dir.RotatedBy(-MathF.PI / 10) - Main.screenPosition, to - Main.screenPosition, color);
            }
        }
        public static void QuickDashLineLocal(Vector2 from, Vector2 to, Color color)
        {
            if (from == to)
                return;
            float step = 8f / (from - to).Length();
            float t = (float)Main.timeForVisualEffects * 0.02f;
            for (float v = (t - MathF.Floor(t) - 1) * 2 * step; v < 1; v += 2 * step)
                QuickDrawLineLocal(from * (1 - Utils.Clamp(v, 0, 1)) + to * Utils.Clamp(v, 0, 1), from * (1 - Utils.Clamp(v + step, 0, 1)) + to * Utils.Clamp(v + step, 0, 1), color);
        }

        public static void QuickDrawBezier(Vector2 from, Vector2 to, Vector2 control, Color color, int steps = 10)
        {
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            List<SimpleVertexInfo> vertex = new List<SimpleVertexInfo>();

            projectorRangeDisplayEffect.Parameters["uMVP"].SetValue(CreateMVP(Vector2.Zero));
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)steps;
                Vector2 p = (1 - t) * (1 - t) * from + 2 * t * (1 - t) * control + t * t * to;
                float u = (i + 1) / (float)steps;
                Vector2 q = (1 - u) * (1 - u) * from + 2 * u * (1 - u) * control + u * u * to;

                Vector2 dir = (q - p).SafeNormalize(Vector2.UnitX);
                Vector2 norm = new Vector2(-dir.Y, dir.X);

                vertex.Add(new SimpleVertexInfo(new Vector3(p.X - norm.X, -p.Y + norm.Y, 0) * Main.GameViewMatrix.Zoom.X, new Vector2(t, 0)));
                vertex.Add(new SimpleVertexInfo(new Vector3(p.X + norm.X, -p.Y - norm.Y, 0) * Main.GameViewMatrix.Zoom.X, new Vector2(t, 1)));
                vertex.Add(new SimpleVertexInfo(new Vector3(q.X - norm.X, -q.Y + norm.Y, 0) * Main.GameViewMatrix.Zoom.X, new Vector2(t, 0)));
                vertex.Add(new SimpleVertexInfo(new Vector3(q.X + norm.X, -q.Y - norm.Y, 0) * Main.GameViewMatrix.Zoom.X, new Vector2(t, 1)));
            }
            projectorRangeDisplayEffect.Parameters["uTex"].SetValue(TextureAssets.MagicPixel.Value);
            projectorRangeDisplayEffect.Parameters["uColor"].SetValue(color.ToVector4());
            projectorRangeDisplayEffect.CurrentTechnique.Passes["Simple"].Apply();
            if (vertex.Count > 3)
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertex.ToArray(), 0, vertex.Count - 2);
            Main.spriteBatch.End();
        }

        public static bool CalculateClippedArea(ProjectorInstance structure, out bool hitFlag, out Vector2 targetTopLeft, out Vector2 targetBottomRight, out Vector2 targetFollowOffset)
        {
            var anchorOffset = structure.data.targetPoint - structure.tilePosition.ToWorldCoordinates();
            var targetSize = structure.data.targetSize;
            bool flipFlag = false;
            float followRotation = 0;
            targetFollowOffset = Vector2.Zero;

            structure.cacheTargetOffset = Vector2.Zero;
            structure.cacheParallaxOffset = Vector2.Zero;
            structure.cacheFollowFlipFlag = false;
            structure.cacheFollowRotation = 0;
            switch (structure.data.targetFollow)
            {
                case (int)ProjectorData.FollowingFlag.Player:
                    for (var i = 0; i < Main.player.Length; i++)
                    {
                        var player = Main.player[(i + (int)(structure.data.targetFollowId * Main.player.Length)) % Main.npc.Length];
                        if (player.active)
                        {
                            targetFollowOffset = -structure.data.targetPoint + player.Center;
                            targetFollowOffset += anchorOffset;
                            flipFlag = player.direction > 0;
                            followRotation = player.fullRotation;
                            break;
                        }
                    }
                    break;
                case (int)ProjectorData.FollowingFlag.Boss:
                    for (var i = 0; i < Main.npc.Length; i++)
                    {
                        var npc = Main.npc[(i + (int)(structure.data.targetFollowId * Main.npc.Length)) % Main.npc.Length];
                        if (npc.active && npc.boss)
                        {
                            targetFollowOffset = -structure.data.targetPoint + npc.Center;
                            targetFollowOffset += anchorOffset;
                            flipFlag = npc.direction > 0;
                            followRotation = npc.rotation;
                            break;
                        }
                    }
                    break;
                case (int)ProjectorData.FollowingFlag.TownNPC:
                    for (var i = 0; i < Main.npc.Length; i++)
                    {
                        var npc = Main.npc[(i + (int)(structure.data.targetFollowId * Main.npc.Length)) % Main.npc.Length];
                        if (npc.active && npc.townNPC)
                        {
                            targetFollowOffset = -structure.data.targetPoint + npc.Center;
                            targetFollowOffset += anchorOffset;
                            flipFlag = npc.direction > 0;
                            followRotation = npc.rotation;
                            break;
                        }
                    }
                    break;
            }
            if (structure.data.targetFollowFlip == 0)
                flipFlag = false;
            followRotation *= 180f / MathF.PI;
            structure.data.targetPoint += targetFollowOffset;
            Vector2 parallaxOffset = ((CaptureManager.Instance.IsCapturing ? Main.LocalPlayer.Center : Main.Camera.Center)
                                   - (structure.tilePosition.ToWorldCoordinates())) * structure.data.parallax;
            structure.data.targetPoint += parallaxOffset;
            float targetRotation = structure.data.targetRotation + followRotation;

            Vector2 targetBoundingR = targetSize.RotatedBy(targetRotation / 180f * MathF.PI) * 0.5f;
            targetBoundingR.X = MathF.Abs(targetBoundingR.X);
            targetBoundingR.Y = MathF.Abs(targetBoundingR.Y);
            Vector2 targetBoundingR2 = (targetSize * new Vector2(-1, 1)).RotatedBy(targetRotation / 180f * MathF.PI) * 0.5f;
            targetBoundingR2.X = MathF.Abs(targetBoundingR2.X);
            targetBoundingR2.Y = MathF.Abs(targetBoundingR2.Y);
            targetBoundingR.X = MathF.Max(targetBoundingR.X, targetBoundingR2.X);
            targetBoundingR.Y = MathF.Max(targetBoundingR.Y, targetBoundingR2.Y);
            Vector2 targetBoundTopLeft = structure.data.targetPoint - targetBoundingR;
            Vector2 targetBoundBottomRight = structure.data.targetPoint + targetBoundingR;

            // Target out of screen
            if ((uiSystem.projectorUIState.focusedInstance != structure || uiSystem.userInterface.CurrentState != uiSystem.projectorUIState) &&
                (targetBoundTopLeft.X > Main.screenPosition.X + Main.screenWidth || targetBoundBottomRight.X < Main.screenPosition.X ||
                 targetBoundTopLeft.Y > Main.screenPosition.Y + Main.screenHeight || targetBoundBottomRight.Y < Main.screenPosition.Y))
            {
                structure.data.targetPoint -= targetFollowOffset + parallaxOffset;
                hitFlag = false;
                targetTopLeft = targetBottomRight = Vector2.Zero;
                return false;
            }

            hitFlag = (targetBoundTopLeft.X < Main.LocalPlayer.position.X + Main.LocalPlayer.width && targetBoundBottomRight.X > Main.LocalPlayer.position.X
                    && targetBoundTopLeft.Y < Main.LocalPlayer.position.Y + Main.LocalPlayer.height && targetBoundBottomRight.Y > Main.LocalPlayer.position.Y);

            Vector2 clipTL = Main.Camera.ScaledPosition - Vector2.One * 4;
            Vector2 clipBR = Main.Camera.ScaledPosition + Main.Camera.ScaledSize + Vector2.One * 4;

            targetBoundTopLeft = new Vector2
            (
                MathF.Max(targetBoundTopLeft.X, clipTL.X),
                MathF.Max(targetBoundTopLeft.Y, clipTL.Y)
            );

            targetBoundBottomRight = new Vector2
            (
                MathF.Min(targetBoundBottomRight.X, clipBR.X),
                MathF.Min(targetBoundBottomRight.Y, clipBR.Y)
            );

            if (targetBoundTopLeft.X > targetBoundBottomRight.X || targetBoundTopLeft.Y > targetBoundBottomRight.Y)
            {
                structure.data.targetPoint -= targetFollowOffset + parallaxOffset;
                hitFlag = false;
                targetTopLeft = targetBottomRight = Vector2.Zero;
                return false;
            }

            Vector2 targetBoundingT1 = (targetBoundTopLeft - structure.data.targetPoint).RotatedBy(-targetRotation / 180f * MathF.PI);
            Vector2 targetBoundingT2 = (new Vector2(targetBoundTopLeft.X, targetBoundBottomRight.Y) - structure.data.targetPoint).RotatedBy(-targetRotation / 180f * MathF.PI);
            Vector2 targetBoundingT3 = (targetBoundBottomRight - structure.data.targetPoint).RotatedBy(-targetRotation / 180f * MathF.PI);
            Vector2 targetBoundingT4 = (new Vector2(targetBoundBottomRight.X, targetBoundTopLeft.Y) - structure.data.targetPoint).RotatedBy(-targetRotation / 180f * MathF.PI);

            if ((structure.data.targetFlip == (int)ProjectorData.FlipFlag.Horizontal) != flipFlag)
            {
                targetBoundingT1.X *= -1;
                targetBoundingT2.X *= -1;
                targetBoundingT3.X *= -1;
                targetBoundingT4.X *= -1;
            }
            if (structure.data.targetFlip == (int)ProjectorData.FlipFlag.Vertical)
            {
                targetBoundingT1.Y *= -1;
                targetBoundingT2.Y *= -1;
                targetBoundingT3.Y *= -1;
                targetBoundingT4.Y *= -1;
            }

            var expandTargetBoundTopLeft = new Vector2
            (
                MathF.Min(MathF.Min(targetBoundingT1.X, targetBoundingT2.X), MathF.Min(targetBoundingT3.X, targetBoundingT4.X)),
                MathF.Min(MathF.Min(targetBoundingT1.Y, targetBoundingT2.Y), MathF.Min(targetBoundingT3.Y, targetBoundingT4.Y))
            ) + structure.data.targetPoint;

            var expandTargetBoundBottomRight = new Vector2
            (
                MathF.Max(MathF.Max(targetBoundingT1.X, targetBoundingT2.X), MathF.Max(targetBoundingT3.X, targetBoundingT4.X)),
                MathF.Max(MathF.Max(targetBoundingT1.Y, targetBoundingT2.Y), MathF.Max(targetBoundingT3.Y, targetBoundingT4.Y))
            ) + structure.data.targetPoint;

            targetTopLeft = new Vector2
            (
                MathF.Max(expandTargetBoundTopLeft.X, structure.data.targetTopLeft.X),
                MathF.Max(expandTargetBoundTopLeft.Y, structure.data.targetTopLeft.Y)
            );

            targetBottomRight = new Vector2
            (
                MathF.Min(expandTargetBoundBottomRight.X, structure.data.targetBottomRight.X),
                MathF.Min(expandTargetBoundBottomRight.Y, structure.data.targetBottomRight.Y)
            );

            bool expandFlag = (structure.data.targetScale >= 1.0f);
            float align = expandFlag ?
                1 * structure.data.targetScale : // 1 pixel in source rect
                2; // 2 pixels in target rect
            targetTopLeft = (targetTopLeft - structure.data.targetTopLeft) / align;
            targetTopLeft = new Vector2(MathF.Floor(targetTopLeft.X), MathF.Floor(targetTopLeft.Y)) * align + structure.data.targetTopLeft;

            targetSize = targetBottomRight - targetTopLeft;
            structure.cacheParallaxOffset = parallaxOffset;
            structure.data.targetPoint -= targetFollowOffset + parallaxOffset;

            if (targetSize.X <= 0 || targetSize.Y <= 0)
                return false;
            if (structure.data.targetFollowFlip > 0)
                structure.cacheFollowFlipFlag = flipFlag;
            if (structure.data.targetFollowRotation > 0)
                structure.cacheFollowRotation = followRotation;
            return true;
        }

        public static void CalculateSourceAreaFromCache(ProjectorInstance structure, out Vector2 sourceTopLeft, out Vector2 sourceBottomRight, out Vector2 sourceFollowOffset)
        {
            var anchorOffset = structure.data.sourcePoint - structure.tilePosition.ToWorldCoordinates();
            var targetTopLeft = structure.cacheTopLeft;
            var targetBottomRight = structure.cacheBottomRight;
            sourceTopLeft = (targetTopLeft - structure.data.targetPoint) / structure.data.targetScale + structure.data.sourcePoint;
            sourceBottomRight = (targetBottomRight - structure.data.targetPoint) / structure.data.targetScale + structure.data.sourcePoint;
            sourceFollowOffset = Vector2.Zero;

            switch (structure.data.sourceFollow)
            {
                case (int)ProjectorData.FollowingFlag.Player:
                    for (var i = 0; i < Main.player.Length; i++)
                    {
                        var player = Main.player[(i + (int)(structure.data.sourceFollowId * Main.player.Length)) % Main.npc.Length];
                        if (player.active)
                        {
                            sourceFollowOffset = -structure.data.sourcePoint + player.Center;
                            sourceFollowOffset += anchorOffset;
                            break;
                        }
                    }
                    break;
                case (int)ProjectorData.FollowingFlag.Boss:
                    for (var i = 0; i < Main.npc.Length; i++)
                    {
                        var npc = Main.npc[(i + (int)(structure.data.sourceFollowId * Main.npc.Length)) % Main.npc.Length];
                        if (npc.active && npc.boss)
                        {
                            sourceFollowOffset = -structure.data.sourcePoint + npc.Center;
                            sourceFollowOffset += anchorOffset;
                            break;
                        }
                    }
                    break;
                case (int)ProjectorData.FollowingFlag.TownNPC:
                    for (var i = 0; i < Main.npc.Length; i++)
                    {
                        var npc = Main.npc[(i + (int)(structure.data.sourceFollowId * Main.npc.Length)) % Main.npc.Length];
                        if (npc.active && npc.townNPC)
                        {
                            sourceFollowOffset = -structure.data.sourcePoint + npc.Center;
                            sourceFollowOffset += anchorOffset;
                            break;
                        }
                    }
                    break;
            }
            structure.cacheSourceParallaxOffset = Vector2.Zero;
            if (structure.data.sourceParallax != 0 && Main.myPlayer != 255)
            {
                structure.cacheSourceParallaxOffset = structure.data.sourceParallax *
                    ((CaptureManager.Instance.IsCapturing ? Main.LocalPlayer.Center : Main.Camera.Center) - structure.tilePosition.ToWorldCoordinates());
                // structure.cacheSourceParallaxOffset = structure.data.sourceParallax *
                // ((CaptureManager.Instance.IsCapturing ? Main.LocalPlayer.Center : Main.Camera.Center) - structure.data.targetPoint);
                sourceFollowOffset += structure.cacheSourceParallaxOffset;
            }
            structure.cacheSourceOffset = sourceFollowOffset;
            sourceTopLeft += sourceFollowOffset;
            sourceBottomRight += sourceFollowOffset;
            structure.cacheSourceTopLeft = sourceTopLeft;
            structure.cacheSourceBottomRight = sourceBottomRight;
        }

        // Hack for XNA sprite batch
        // Hack XNA sprite batch by adding scales to eradicate gaps between tiles
        public class SpriteBatchHack
        {
            public Type typeOfSpriteInfo;
            public Type arrayOfSpriteInfo;
            public FieldInfo spriteInfosField;
            public FieldInfo numSpritesField;
            public FieldInfo destinationWField;
            public FieldInfo destinationHField;
            public FieldInfo depthField;

            public SpriteBatchHack()
            {
                typeOfSpriteInfo = typeof(SpriteBatch).Assembly.GetTypes().Where((x) => x.IsNestedPrivate && x.Name.Equals("SpriteInfo")).ToArray()[0];
                arrayOfSpriteInfo = typeOfSpriteInfo.MakeArrayType();
                spriteInfosField = typeof(SpriteBatch).GetField("spriteInfos", BindingFlags.Instance | BindingFlags.NonPublic);
                numSpritesField = typeof(SpriteBatch).GetField("numSprites", BindingFlags.Instance | BindingFlags.NonPublic);
                destinationWField = typeOfSpriteInfo.GetField("destinationW", BindingFlags.Public | BindingFlags.Instance);
                destinationHField = typeOfSpriteInfo.GetField("destinationH", BindingFlags.Public | BindingFlags.Instance);
                depthField = typeOfSpriteInfo.GetField("depth", BindingFlags.Public | BindingFlags.Instance);
            }

            public void HackSpriteBatchScale(SpriteBatch spriteBatch, float scaleFactor)
            {

                Array spriteInfos = (Array)spriteInfosField.GetValue(spriteBatch);
                int numSprites = (int)numSpritesField.GetValue(spriteBatch);

                for (int i = 0; i < numSprites; i++)
                {
                    object boxed = spriteInfos.GetValue(i);
                    float destinationH = (float)destinationHField.GetValue(boxed);
                    destinationHField.SetValue(boxed, destinationH * scaleFactor);
                    float destinationW = (float)destinationWField.GetValue(boxed);
                    destinationWField.SetValue(boxed, destinationW * scaleFactor);
                    depthField.SetValue(boxed, 1 - i / (float)(numSprites + 1));
                    spriteInfos.SetValue(boxed, i);
                }
            }
        }

        public static Matrix CreateMVP(Vector2 worldPosOffset)
        {
            Matrix mvp =
                Matrix.CreateTranslation
                (
                    Main.GameViewMatrix.Zoom.X * ((worldPosOffset.X - Main.screenPosition.X) - Main.screenWidth * 0.5f),
                    Main.GameViewMatrix.Zoom.Y * (-(worldPosOffset.Y - Main.screenPosition.Y) + Main.screenHeight * 0.5f),
                    2000
                ) *
                Matrix.CreateLookAt(Vector3.UnitZ, Vector3.Zero, -Vector3.UnitY) *
                Matrix.CreatePerspective(Main.screenWidth * 0.25f, Main.screenHeight * 0.25f, 500, 10000);
            return mvp;
        }

        private struct SimpleVertexInfo : IVertexType
        {
            private static VertexDeclaration _VertexDeclaration = new VertexDeclaration(new VertexElement[2]
            {
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(3 * sizeof(float), VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0)
            });
            public Vector3 position;
            public Vector2 texCoord;

            public SimpleVertexInfo(Vector3 pos, Vector2 uv)
            {
                position = pos;
                texCoord = uv;
            }

            public VertexDeclaration VertexDeclaration { get => _VertexDeclaration; }
        }
    }
}
