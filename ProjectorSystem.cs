using Terraria.DataStructures;
using ImmersiveProjector.DataStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.Liquid;
using Terraria.Graphics;
using Terraria.Graphics.Light;
using Terraria.ModLoader;
using static Terraria.WaterfallManager;
using ImmersiveProjector.Tiles;
using ReLogic.Content;
using ImmersiveProjector.UI;
using System.Collections;
using System.Linq;
using Steamworks;
using Terraria.GameInput;
using Terraria.Graphics.Capture;
using Terraria.GameContent.Events;
using System.Linq.Expressions;
using Humanizer;
using Terraria.IO;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using Terraria.ID;
using static ImmersiveProjector.ProjectorUtils;

namespace ImmersiveProjector
{
    class ProjectorSystem : ModSystem
    {
        public bool inited = false;

        public RenderTarget2D projectorRT = null;
        public RenderTarget2D projectorRTSwap = null;
        public RenderTarget2D overlayRT = null;


        // Reference to original Tile drawing
        public TilePaintSystemV2 tilePaintSystem = null;
        public WallDrawing wallDrawing = null;

        // Create new tile renderer 
        public TileDrawing projectorTileDrawing;

        // Dust and Gore
        // store dusts and gores in any projector area
        public Dust[] projectorDust;
        public Gore[] projectorGore;
        // Temporary buffer to for UpdateDust
        public Dust[] tempDustBuffer = null;

        // Lighting
        // Create new lighting engines
        // public LightingEngine projectorLightingEngine = new LightingEngine();
        public LegacyLighting projectorLegacyLighting = new LegacyLighting(Main.Camera);
        private bool legacyLightingRebuilt;
        public IList perFrameLightList = null;
        // Lighting source combination
        public ILightingEngine origLightingEngineCache = null;
        public ProjectorData.LightingSourceFlag lightingCombination = ProjectorData.LightingSourceFlag.Source;
        public Tuple<Rectangle, Vector3[]> referenceLightingCache = new (new Rectangle(0, 0, 1, 1), new Vector3[1]);

        // Liquid Renderer
        public LiquidRenderer projectorLiquidRenderer;
        private bool projectorLiquidProcessing = false;
        private float projectorLiquidToScale = 1f;

        // Hook flag
        private bool projectorProcessing = false;

        // When processing UpdateDust
        // ban NewDust from being called
        private bool banNewDust = false;
        // also change DustUpdate to return as soon as Main.maxDustToDraw is reached
        // IL warning: overrideDustUpdate uses IL editing
        private bool overrideDustUpdate = false;
        private List<Dust> workingDustIdentityList = null;
        private List<Gore> workingGoreIdentityList = null;
        // temp list to store newly created per-frame dusts
        public List<Dust> tempDustIdentityList = null;
        public List<Gore> tempGoreIdentityList = null;

        // Only for UI color
        private bool findingTargetFlag = false;

        // IO with RT
        private Color[] colorBuffer = new Color[1];

        public UISystem uiSystem { get => ModContent.GetInstance<UISystem>(); }

        public List<ProjectorInstance> projectorList;
        public static List<ProjectorInstance> ListInWorld { get => ModContent.GetInstance<ProjectorSystem>().projectorList; }

        public Effect projectorRangeDisplayEffect;
        public Effect projectorFilterEffect;

        public Action<SpriteBatch> delayedSpriteDraw;

        public ProjectorSystem()
        {
        }

        public override void OnWorldUnload()
        {
            if (projectorList != null)
                projectorList.Clear();
            if (uiSystem.userInterface.CurrentState is ProjectorUI)
                uiSystem.userInterface.SetState(null);
        }

        public override void OnWorldLoad()
        {
            inited = false;
            // Lighting.Mode = LightMode.Color;
            // projectorLightingEngine.Rebuild();
            projectorLegacyLighting.Rebuild();
            projectorDust = new Dust[6002];
            tempDustBuffer = new Dust[6002];
            projectorGore = new Gore[602];
            tempDustIdentityList = new List<Dust>();
            tempGoreIdentityList = new List<Gore>();
            for (int i = 0; i <= 6000; i++)
            {
                projectorDust[i] = new Dust();
                projectorDust[i].dustIndex = i;
            }
            // Really need this
            // or the game will crash due to null ref
            // from dusts that creates new dusts during update
            // such as RoD dusts
            tempDustBuffer[Main.maxDust] = new Dust();
            tempDustBuffer[Main.maxDust].dustIndex = Main.maxDust;
            for (int i = 0; i <= 600; i++)
                projectorGore[i] = new Gore();

            projectorList.Clear();
            base.OnWorldLoad();

            /*
            var info = typeof(WorldFile).GetField("IOLock", BindingFlags.Static | BindingFlags.NonPublic);
            object IOLock = info.GetValue(null);
            Monitor.Enter(IOLock);
            */
        }

        public override void PreWorldGen()
        {
            base.PreWorldGen();
        }

        public override void Load()
        {
            tilePaintSystem = Main.instance.TilePaintSystem;
            // tileDrawing = Main.instance.TilesRenderer;
            wallDrawing = Main.instance.WallsRenderer;
            projectorTileDrawing = new TileDrawing(tilePaintSystem);
            projectorList = new List<ProjectorInstance>();
            On.Terraria.Graphics.Effects.FilterManager.EndCapture += ScreenEffectDecorator;
            On.Terraria.Lighting.GetColor_int_int += LightColorDecorator;
            On.Terraria.Graphics.TileBatch.Draw_Texture2D_Vector2_Nullable1_VertexColors_Vector2_float_SpriteEffects += TileBatchDrawDecorator;
            On.Terraria.Graphics.TileBatch.InternalDraw += TileBatchInternalDrawDecorator;
            On.Terraria.Lighting.Initialize += LightingInitializeDecorator;
            On.Terraria.Dust.NewDust += NewDustDecorator;
            On.Terraria.Gore.NewGore_IEntitySource_Vector2_Vector2_int_float += NewGoreDecorator;
            IL.Terraria.Dust.UpdateDust += UpdateDustILEdit;

            On.Terraria.GameContent.Drawing.TileDrawing.PostDrawTiles += DrawHook_TileEntities;
            On.Terraria.Main.DoDraw_UpdateCameraPosition += DrawHook_UpdateCameraPosition;
            On.Terraria.Main.DrawCachedProjs += DrawHook_CachedProjs;
            On.Terraria.Main.DrawCachedNPCs += DrawHook_CachedNPCs;
            On.Terraria.Main.DrawCapture += DrawHook_Capture;

            legacyLightingRebuilt = false;
            base.Load();
        }

        public override void OnModLoad()
        {
            projectorRangeDisplayEffect = Mod.Assets.Request<Effect>("Effects/ProjectorRangeDisplay", AssetRequestMode.ImmediateLoad).Value;
            projectorFilterEffect = Mod.Assets.Request<Effect>("Effects/ProjectorFilter", AssetRequestMode.ImmediateLoad).Value;
            ProjectorUtils.projectorRangeDisplayEffect = projectorRangeDisplayEffect;
            ProjectorUtils.projectorFilterEffect = projectorFilterEffect;
            projectorLiquidRenderer = new LiquidRenderer();
            var info = typeof(LiquidRenderer).GetMethod("PrepareAssets", BindingFlags.Instance | BindingFlags.NonPublic);
            info.Invoke(projectorLiquidRenderer, new object[] { });
            base.OnModLoad();
        }


        public override void Unload()
        {
            On.Terraria.Graphics.Effects.FilterManager.EndCapture -= ScreenEffectDecorator;
            On.Terraria.Lighting.GetColor_int_int -= LightColorDecorator;
            On.Terraria.Graphics.TileBatch.Draw_Texture2D_Vector2_Nullable1_VertexColors_Vector2_float_SpriteEffects -= TileBatchDrawDecorator;
            On.Terraria.Graphics.TileBatch.InternalDraw -= TileBatchInternalDrawDecorator;
            On.Terraria.Lighting.Initialize -= LightingInitializeDecorator;
            On.Terraria.Dust.NewDust -= NewDustDecorator;
            On.Terraria.Gore.NewGore_IEntitySource_Vector2_Vector2_int_float -= NewGoreDecorator;
            IL.Terraria.Dust.UpdateDust -= UpdateDustILEdit;

            On.Terraria.GameContent.Drawing.TileDrawing.PostDrawTiles -= DrawHook_TileEntities;
            On.Terraria.Main.DoDraw_UpdateCameraPosition -= DrawHook_UpdateCameraPosition;
            On.Terraria.Main.DrawCachedProjs -= DrawHook_CachedProjs;
            On.Terraria.Main.DrawCachedNPCs -= DrawHook_CachedNPCs;
            On.Terraria.Main.DrawCapture -= DrawHook_Capture;

            base.Unload();
        }

        public void EnsureProjectorRT(float width, float height)
        {
            EnsureRT(width, height, ref projectorRT);
            EnsureRT(width, height, ref projectorRTSwap);
        }

        public void EnsureOverlayRT(float width, float height)
        {
            // This has to be with the same size
            if (overlayRT == null || overlayRT.Width != width || overlayRT.Height != height)
            {
                if (overlayRT != null && !overlayRT.IsDisposed)
                    overlayRT.Dispose();
                overlayRT = null;
                overlayRT = new RenderTarget2D(Main.graphics.GraphicsDevice,
                    Math.Max(overlayRT != null ? overlayRT.Width : 100, (int)Math.Ceiling(width)),
                    Math.Max(overlayRT != null ? overlayRT.Height : 100, (int)Math.Ceiling(height)),
                    false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            }
        }


        private void DrawHook_Capture(On.Terraria.Main.orig_DrawCapture orig, Main self, Rectangle area, CaptureSettings settings)
        {
            var origScreenPosition = Main.screenPosition;
            var origScreenWidth = Main.screenWidth;
            var origScreenHeight = Main.screenHeight;

            Main.screenPosition = area.TopLeft() * 16;
            Main.screenWidth = area.Width * 16;
            Main.screenHeight = area.Height * 16;

            PrepareClippingCache();
            orig(self, area, settings);

            Main.screenPosition = origScreenPosition;
            Main.screenWidth = origScreenWidth;
            Main.screenHeight = origScreenHeight;
        }

        private void DrawHook_UpdateCameraPosition(On.Terraria.Main.orig_DoDraw_UpdateCameraPosition orig)
        {
            orig();
        }

        private void DrawHook_TileEntities(On.Terraria.GameContent.Drawing.TileDrawing.orig_PostDrawTiles orig, TileDrawing self, bool solidLayer, bool forRenderTargets, bool intoRenderTargets)
        {
            orig(self, solidLayer, forRenderTargets, intoRenderTargets);
        }

        private void DrawHook_CachedProjs(On.Terraria.Main.orig_DrawCachedProjs orig, Main self, List<int> projCache, bool startSpriteBatch)
        {
            if (projCache == Main.instance.DrawCacheProjsOverWiresUI)
            {
                if (startSpriteBatch)
                    Main.spriteBatch.Begin();
                DrawProjectorLayer((int)ProjectorData.LayerFlag.Foreground);
                if (startSpriteBatch)
                    Main.spriteBatch.End();
            }
            if (projCache == Main.instance.DrawCacheProjsBehindNPCs)
            {
                if (startSpriteBatch)
                    Main.spriteBatch.Begin();
                DrawProjectorLayer((int)ProjectorData.LayerFlag.Normal);
                if (startSpriteBatch)
                    Main.spriteBatch.End();
            }
            orig(self, projCache, startSpriteBatch);
        }

        private void DrawHook_CachedNPCs(On.Terraria.Main.orig_DrawCachedNPCs orig, Main self, List<int> npcCache, bool behindTiles)
        {
            if (npcCache == Main.instance.DrawCacheNPCsMoonMoon)
                DrawProjectorLayer((int)ProjectorData.LayerFlag.BehindWalls);
            if (npcCache == Main.instance.DrawCacheNPCsBehindNonSolidTiles)
            {
                DrawProjectorLayer((int)ProjectorData.LayerFlag.BehindFurnitures);
            }
            orig(self, npcCache, behindTiles);
        }

        public void PrepareClippingCache()
        {
            lock (projectorList)
            {
                foreach (var s in projectorList)
                {
                    TileEntity.ByPosition.TryGetValue(new Point16(s.tilePosition.X, s.tilePosition.Y), out var te);
                    if (te != null && te is ProjectorTileEntity entity)
                    {
                        if (entity.projectorInstance != s)
                            continue;
                        if (entity.TurnedOn && CalculateClippedArea(s, out var hitFlag, out var tl, out var br, out var to))
                        {
                            s.cacheHitFlag = hitFlag;
                            s.cacheTopLeft = tl;
                            s.cacheBottomRight = br;
                            s.cacheTargetOffset = to;
                            s.cacheNeedDraw = true;
                        }
                    }
                }
            }
        }

        public void HookedDraw(ProjectorInstance structure)
        {
            var targetTopLeft = structure.cacheTopLeft;
            var targetBottomRight = structure.cacheBottomRight;
            var targetFollowOffset = structure.cacheTargetOffset;
            var hitFlag = structure.cacheHitFlag;
            structure.data.targetPoint += targetFollowOffset;
            structure.data.targetPoint += structure.cacheParallaxOffset;
            structure.data.targetRotation += structure.cacheFollowRotation;
            // Rounding down top-left corner to ensure that the clipped area is aligned
            // otherwise there would be annoying sub-pixel glitches

            // This is quite complex since this mod implement rendering in 2 different mode
            // if the target scale is greater than 1, a render target of the size of source area is used and then scaled up to draw
            // if the target scale is less than 1, a render target of the size of target area is used. 
            bool expandFlag = (structure.data.targetScale >= 1.0f);

            var targetSize = targetBottomRight - targetTopLeft;
            Debug.Assert(targetSize.X > 0 && targetSize.Y > 0);

            CalculateSourceAreaFromCache(structure, out Vector2 sourceTopLeft, out Vector2 sourceBottomRight, out Vector2 sourceFollowOffset);
            var sourceSize = sourceBottomRight - sourceTopLeft;
            // The code above is to calculate clipped bounding rect on the *source* area
            // which is a minimum rect that contains all pixels that contribute to the visible target pixels after rotation

            // might be a little complex but definitely useful

            projectorProcessing = true;

            var origDrawToScreen = Main.drawToScreen;
            var origViewMatrix = Main.GameViewMatrix;
            var origScreenPosition = Main.screenPosition;
            var origScreenWidth = Main.screenWidth;
            var origScreenHeight = Main.screenHeight;
            var origOffscreenRange = Main.offScreenRange;
            var origSampleState = Main.DefaultSamplerState.Filter;

            var origDust = Main.dust;
            var origGore = Main.gore;
            // Hook newly created dust and gore
            Main.dust = projectorDust;
            Main.gore = projectorGore;
            workingDustIdentityList = structure.dustIdentities;
            workingGoreIdentityList = null;

            // Try fixing White / Retro Lighting Mode
            // Be careful! The setter method of Light.Mode
            // affects LegacyEngine.Mode
            var origLightingMode = Lighting.Mode;
            var origRenderCount = Main.renderCount;
            // Map Delay and Map Time are used in lighting engine
            // to export lighted tile data to world map / minimap
            var origMapDelay = Main.mapDelay;
            var origMapTime = Main.mapTime;
            // This is used in Legacy Lighting in shifting 
            // lighting data from then last call to reduce calculation
            var origScreenLastPosition = Main.screenLastPosition;

            FieldInfo _activeEngineInfo = typeof(Lighting).GetField("_activeEngine", BindingFlags.Static | BindingFlags.NonPublic);
            ILightingEngine origActiveEngine = (ILightingEngine)_activeEngineInfo.GetValue(null);
            origLightingEngineCache = origActiveEngine;

            var origLiquidRenderer = LiquidRenderer.Instance;
            LiquidRenderer.Instance = projectorLiquidRenderer;
            projectorLiquidRenderer._liquidTextures = origLiquidRenderer._liquidTextures;

            Rectangle lightingArea = new Rectangle(Math.Max(5, (int)(sourceTopLeft.X / 16)), Math.Max(5, (int)(sourceTopLeft.Y / 16)),
                                                   (int)(sourceSize.X / 16) + 1, (int)(sourceSize.Y / 16) + 1);
            Rectangle liquidArea = new Rectangle(Math.Max(5, lightingArea.X - 1) - 2, Math.Max(5, lightingArea.Y - 1),
                                                 Math.Min(Main.maxTilesX - 5 - lightingArea.X, lightingArea.Width + 3) + 2,
                                                 Math.Min(Main.maxTilesY - 5 - lightingArea.Y, lightingArea.Height + 6) + 4);
            // These magical numbers cannot be changed for some reason

            GraphicsDevice graphicDevice = Main.graphics.GraphicsDevice;

            var projectorLightingEngine = structure.lightingEngine;
            ILightingEngine currentEngine = (origLightingMode == LightMode.Color || origLightingMode == LightMode.White) ? projectorLightingEngine : projectorLegacyLighting;
            projectorLegacyLighting.Mode = Lighting.LegacyEngine.Mode;
            _activeEngineInfo.SetValue(null, currentEngine);
            HookedDraw_UpdateLighting(structure, currentEngine, lightingArea);

            if (expandFlag)
                EnsureProjectorRT(sourceSize.X, sourceSize.Y);
            else
                EnsureProjectorRT(targetSize.X, targetSize.Y);

            Main.drawToScreen = true;
            Main.offScreenRange = 0;
            // Main.screenPosition = Main.LocalPlayer.Center + new Vector2(Main.mouseX - Main.screenWidth * 0.5f, Main.mouseY - Main.screenHeight * 0.5f) - new Vector2(16, 100);
            Main.screenWidth = (int)Math.Ceiling(sourceSize.X) + 1;
            Main.screenHeight = (int)Math.Ceiling(sourceSize.Y) + 1;
            // Main.screenPosition = sourceTopLeft + sourceSize * 0.5f * (-1f + 1f / structure.data.targetScale);
            Main.screenPosition = sourceTopLeft;
            // Main.screenPosition.X = (int)Main.screenPosition.X;
            // Main.screenPosition.Y = (int)Main.screenPosition.Y;

            Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(Main.graphics.GraphicsDevice);

            // Main.maxTilesY = (int)(Main.screenPosition.Y / 16.0) + 200;
            RenderTargetBinding[] origTargets = graphicDevice.GetRenderTargets();
            graphicDevice.SetRenderTarget(projectorRT);

            // Do not always use Clear here
            // Since the whole RT might be significantly bigger than what we need
            // We should only clear the pixels we need

            Rectangle renderRect = new Rectangle(0, 0,
                (int)Math.Ceiling(expandFlag ? sourceSize.X : targetSize.X),
                (int)Math.Ceiling(expandFlag ? sourceSize.Y : targetSize.Y));
            // If we need to clear a relatively large area
            if (renderRect.Width * (float)renderRect.Height >= 0.5f * projectorRT.Width * (float)projectorRT.Height)
            {
                graphicDevice.Clear(Color.Transparent);
            }
            else
            {
                Main.spriteBatch.Begin(SpriteSortMode.Texture, BlendState.Opaque,
                    Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, renderRect, new Color(0, 0, 0, 0.0f));
                Main.spriteBatch.End();
            }

            Main.DefaultSamplerState.Filter = TextureFilter.Point;

            var DrawCachedNPCsMethod = typeof(Main).GetMethod("DrawCachedNPCs", BindingFlags.Instance | BindingFlags.NonPublic);
            var DrawCachedProjsMethod = typeof(Main).GetMethod("DrawCachedProjs", BindingFlags.Instance | BindingFlags.NonPublic);

            if (structure.data.captureCreature == (int)ProjectorData.CaptureCreatureFlag.All)
            {
                // MoonMoon NPCs
                if (!expandFlag)
                {
                    Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, (int)(sourceSize.X), (int)(sourceSize.Y)));
                    Main.GameViewMatrix.Zoom = Vector2.One * structure.data.targetScale;
                    Main.screenPosition = sourceTopLeft + sourceSize * 0.5f * (-1f + 1f / structure.data.targetScale);
                }
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
                DrawCachedNPCsMethod.Invoke(Main.instance, new object[] {Main.instance.DrawCacheNPCsMoonMoon, true});
                Main.spriteBatch.End();
                Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(Main.graphics.GraphicsDevice);
                Main.screenPosition = sourceTopLeft;
            }

            if (structure.data.captureSolid == (int)ProjectorData.CaptureSolidFlag.All)
            {
                // Liquid 1
                projectorLiquidProcessing = true;
                projectorLiquidToScale = expandFlag ? 1f : structure.data.targetScale + 0.01f;
                if (!expandFlag)
                {
                    Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, (int)(sourceSize.X), (int)(sourceSize.Y)));
                    Main.GameViewMatrix.Zoom = Vector2.One * structure.data.targetScale;
                    Main.screenPosition = sourceTopLeft + sourceSize * 0.5f * (-1f + 1f / structure.data.targetScale);
                }
                LiquidRenderer.Instance.PrepareDraw(liquidArea);
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None,
                    Main.Rasterizer, null, Matrix.CreateScale(projectorLiquidToScale));
                LiquidRenderer.Instance.Draw(Main.spriteBatch, -sourceTopLeft, Main.waterStyle, Main.liquidAlpha[Main.waterStyle], true);
                Main.spriteBatch.End();
                projectorLiquidProcessing = false;
                Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(Main.graphics.GraphicsDevice);
                Main.screenPosition = sourceTopLeft;
                // LiquidRenderer.Instance.Draw(Main.spriteBatch, -sourceTopLeft, Main.waterStyle, Main.liquidAlpha[Main.waterStyle], false);
            }

            if (structure.data.captureWall == (int)ProjectorData.CaptureWallFlag.TileWall)
            {
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None,
                    Main.Rasterizer, null, expandFlag ? Matrix.Identity : Matrix.CreateScale(structure.data.targetScale));
                Main.tileBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None,
                    Main.Rasterizer, null, expandFlag ? Matrix.Identity : Matrix.CreateScale(structure.data.targetScale));
                wallDrawing.DrawWalls();
                Main.tileBatch.End();
                Main.spriteBatch.End();
            }

            if (structure.data.captureCreature == (int)ProjectorData.CaptureCreatureFlag.All)
            {
                if (!expandFlag)
                {
                    Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, (int)(sourceSize.X), (int)(sourceSize.Y)));
                    Main.GameViewMatrix.Zoom = Vector2.One * structure.data.targetScale;
                    Main.screenPosition = sourceTopLeft + sourceSize * 0.5f * (-1f + 1f / structure.data.targetScale);
                }
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
                var info = typeof(Main).GetMethod("DrawWoF", BindingFlags.Instance | BindingFlags.NonPublic);
                info.Invoke(Main.instance, new object[0]);
                MoonlordDeathDrama.DrawPieces(Main.spriteBatch);
                MoonlordDeathDrama.DrawExplosions(Main.spriteBatch);
                DrawCachedNPCsMethod.Invoke(Main.instance, new object[] {Main.instance.DrawCacheNPCsBehindNonSolidTiles, true});
                Main.spriteBatch.End();
                Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(Main.graphics.GraphicsDevice);
                Main.screenPosition = sourceTopLeft;
            }

            // Tiles part 1
            if (structure.data.captureSolid != (int)ProjectorData.CaptureSolidFlag.None)
            {
                projectorTileDrawing.PreDrawTiles(false, true, true);
                Main.spriteBatch.Begin(expandFlag ? SpriteSortMode.Deferred : SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None,
                    Main.Rasterizer, null, expandFlag ? Matrix.Identity : Matrix.CreateScale(structure.data.targetScale));
                if (structure.data.captureSolid == (int)ProjectorData.CaptureSolidFlag.All)
                    projectorTileDrawing.Draw(false, false /* Unused */, true, -1);
                if (!expandFlag)
                    spriteBatchHack.HackSpriteBatchScale(Main.spriteBatch, 1 + 0.1f / 16f / structure.data.targetScale);
                Main.spriteBatch.End();
            }

            if (structure.data.captureSolid == (int)ProjectorData.CaptureSolidFlag.All)
            {
                if (!expandFlag)
                {
                    Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, (int)(sourceSize.X), (int)(sourceSize.Y)));
                    Main.GameViewMatrix.Zoom = Vector2.One * structure.data.targetScale;
                    Main.screenPosition = sourceTopLeft + sourceSize * 0.5f * (-1f + 1f / structure.data.targetScale);
                }
                projectorTileDrawing.PostDrawTiles(false, true, false);
                Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(Main.graphics.GraphicsDevice);
                Main.screenPosition = sourceTopLeft;
            }

            if (structure.data.captureSolid == (int)ProjectorData.CaptureSolidFlag.All)
            {
                // Waterfall
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None,
                    Main.Rasterizer, null, Matrix.CreateScale(projectorLiquidToScale));

                FieldInfo waterfallCurrentMaxInfo = typeof(WaterfallManager).GetField("currentMax", BindingFlags.Instance | BindingFlags.NonPublic);
                var waterfallCurrentMax = waterfallCurrentMaxInfo.GetValue(Main.instance.waterfallManager);
                FieldInfo waterfallCountInfo = typeof(WaterfallManager).GetField("findWaterfallCount", BindingFlags.Instance | BindingFlags.NonPublic);
                var waterfallCount = waterfallCountInfo.GetValue(Main.instance.waterfallManager);
                FieldInfo waterfallsInfo = typeof(WaterfallManager).GetField("waterfalls", BindingFlags.Instance | BindingFlags.NonPublic);
                var waterfalls = waterfallsInfo.GetValue(Main.instance.waterfallManager);

                waterfallCountInfo.SetValue(Main.instance.waterfallManager, structure.findWaterfallCountdown);
                if (structure.waterfalls == null || structure.waterfalls.Length < Main.instance.waterfallManager.maxWaterfallCount)
                    structure.waterfalls = new WaterfallData[Main.instance.waterfallManager.maxWaterfallCount];
                waterfallsInfo.SetValue(Main.instance.waterfallManager, structure.waterfalls);
                waterfallCurrentMaxInfo.SetValue(Main.instance.waterfallManager, structure.waterfallCount);

                Main.instance.waterfallManager.FindWaterfalls(false);
                Main.tileBatch.Begin();
                Main.instance.waterfallManager.Draw(Main.spriteBatch);
                Main.tileBatch.End();
                Main.spriteBatch.End();

                structure.findWaterfallCountdown = (int)waterfallCountInfo.GetValue(Main.instance.waterfallManager);
                structure.waterfallCount = (int)waterfallCurrentMaxInfo.GetValue(Main.instance.waterfallManager);

                waterfallCurrentMaxInfo.SetValue(Main.instance.waterfallManager, waterfallCurrentMax);
                waterfallCountInfo.SetValue(Main.instance.waterfallManager, waterfallCount);
                waterfallsInfo.SetValue(Main.instance.waterfallManager, waterfalls);
            }

            if (structure.data.captureCreature == (int)ProjectorData.CaptureCreatureFlag.All)
            {
                if (!expandFlag)
                {
                    Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, (int)(sourceSize.X), (int)(sourceSize.Y)));
                    Main.GameViewMatrix.Zoom = Vector2.One * structure.data.targetScale;
                    Main.screenPosition = sourceTopLeft + sourceSize * 0.5f * (-1f + 1f / structure.data.targetScale);
                }
                DrawCachedProjsMethod.Invoke(Main.instance, new object[] { Main.instance.DrawCacheProjsBehindNPCsAndTiles, true });
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
                var info = typeof(Main).GetMethod("DrawNPCs", BindingFlags.Instance | BindingFlags.NonPublic);
                info.Invoke(Main.instance, new object[] { true });
                Main.spriteBatch.End();
                Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(Main.graphics.GraphicsDevice);
                Main.screenPosition = sourceTopLeft;
            }

            // Tiles part 2
            if (structure.data.captureSolid != (int)ProjectorData.CaptureSolidFlag.None)
            {
                projectorTileDrawing.PreDrawTiles(true, true, false);
                Main.spriteBatch.Begin(expandFlag ? SpriteSortMode.Deferred : SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None,
                    Main.Rasterizer, null, expandFlag ? Matrix.Identity : Matrix.CreateScale(structure.data.targetScale));
                projectorTileDrawing.Draw(true, false /* Unused */, true, -1);
                if (!expandFlag)
                    spriteBatchHack.HackSpriteBatchScale(Main.spriteBatch, 1 + 0.5f / 16f / structure.data.targetScale);
                Main.spriteBatch.End();
            }

            if (structure.data.captureSolid == (int)ProjectorData.CaptureSolidFlag.All)
            {
                if (!expandFlag)
                {
                    Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, (int)(sourceSize.X), (int)(sourceSize.Y)));
                    Main.GameViewMatrix.Zoom = Vector2.One * structure.data.targetScale;
                    Main.screenPosition = sourceTopLeft + sourceSize * 0.5f * (-1f + 1f / structure.data.targetScale);
                }
                projectorTileDrawing.PostDrawTiles(true, true, false);
                Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(Main.graphics.GraphicsDevice);
                Main.screenPosition = sourceTopLeft;
            }

            // Entities
            if (structure.data.captureCreature == (int)ProjectorData.CaptureCreatureFlag.All)
            {
                if (!expandFlag)
                {
                    Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, (int)(sourceSize.X), (int)(sourceSize.Y)));
                    Main.GameViewMatrix.Zoom = Vector2.One * structure.data.targetScale;
                    Main.screenPosition = sourceTopLeft + sourceSize * 0.5f * (-1f + 1f / structure.data.targetScale);
                }
                var info = typeof(Main).GetMethod("DrawPlayers_BehindNPCs", BindingFlags.Instance | BindingFlags.NonPublic);
                info.Invoke(Main.instance, new object[] {  });
                DrawCachedProjsMethod.Invoke(Main.instance, new object[] { Main.instance.DrawCacheProjsBehindNPCs, true});

                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
                info = typeof(Main).GetMethod("DrawNPCs", BindingFlags.Instance | BindingFlags.NonPublic);
                info.Invoke(Main.instance, new object[] { false });
                Main.spriteBatch.End();

                Main.spriteBatch.Begin();
                DrawCachedNPCsMethod.Invoke(Main.instance, new object[] { Main.instance.DrawCacheNPCProjectiles, false});
                Main.spriteBatch.End();
                // DrawSuperSpecialProjectiles(DrawCacheFirstFractals);
                DrawCachedProjsMethod.Invoke(Main.instance, new object[] { Main.instance.DrawCacheProjsBehindProjectiles, true });
                info = typeof(Main).GetMethod("DrawProjectiles", BindingFlags.Instance | BindingFlags.NonPublic);

                Main.spriteBatch.Begin();
                Main.ParticleSystem_World_BehindPlayers.Settings.AnchorPosition = -Main.screenPosition;
                Main.ParticleSystem_World_BehindPlayers.Draw(Main.spriteBatch);
                Main.spriteBatch.End();

                info.Invoke(Main.instance, new object[] {  });
                info = typeof(Main).GetMethod("DrawPlayers_AfterProjectiles", BindingFlags.Instance | BindingFlags.NonPublic);
                info.Invoke(Main.instance, new object[] {  });

                DrawCachedProjsMethod.Invoke(Main.instance, new object[] { Main.instance.DrawCacheProjsOverPlayers, true });

                Main.spriteBatch.Begin();
                DrawCachedNPCsMethod.Invoke(Main.instance, new object[] { Main.instance.DrawCacheNPCsOverPlayers, false});
                Main.spriteBatch.End();

                Main.spriteBatch.Begin();
                Main.ParticleSystem_World_OverPlayers.Settings.AnchorPosition = -Main.screenPosition;
                Main.ParticleSystem_World_OverPlayers.Draw(Main.spriteBatch);
                Main.spriteBatch.End();

                // DrawItems();
                // FastInvoke(Main.instance, "DrawItems", new object[] { });
                // DrawRain();
                // FastInvoke(Main.instance, "DrawRain", new object[] { });
                // DrawGore();
                // FastInvoke(Main.instance, "DrawGore", new object[] { });

                // There is some problem causing DrawGore to fail (Perhaps IL stuffs)
                // Main.gore = origGore;
                info = typeof(Main).GetMethod("DrawGore", BindingFlags.Instance | BindingFlags.NonPublic);
                Main.spriteBatch.Begin();
                // info.Invoke(Main.instance, new object[] {  });
                Main.gore = projectorGore;
                info.Invoke(Main.instance, new object[] {  });
                Main.spriteBatch.End();

                // DrawDust();
                // FastInvoke(Main.instance, "DrawDust", new object[] { });
                // Main.dust = origDust;
                info = typeof(Main).GetMethod("DrawDust", BindingFlags.Instance | BindingFlags.NonPublic);
                // info.Invoke(Main.instance, new object[] {  });
                Main.dust = tempDustBuffer;
                var origMaxDust = Main.maxDustToDraw;
                Main.maxDustToDraw = 0;
                foreach (var i in structure.dustIdentities)
                    tempDustBuffer[Main.maxDustToDraw++] = i;
                banNewDust = true;
                info.Invoke(Main.instance, new object[] {  });
                banNewDust = false;
                Main.maxDustToDraw = origMaxDust;

                Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(Main.graphics.GraphicsDevice);
                Main.screenPosition = sourceTopLeft;
            }

            if (structure.data.captureSolid == (int)ProjectorData.CaptureSolidFlag.All)
            {
                // Liquid 2
                projectorLiquidProcessing = true;
                projectorLiquidToScale = expandFlag ? 1f : structure.data.targetScale + 0.01f;
                if (!expandFlag)
                {
                    Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, (int)(sourceSize.X), (int)(sourceSize.Y)));
                    Main.GameViewMatrix.Zoom = Vector2.One * structure.data.targetScale;
                    Main.screenPosition = sourceTopLeft + sourceSize * 0.5f * (-1f + 1f / structure.data.targetScale);
                }
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None,
                    Main.Rasterizer, null, Matrix.CreateScale(projectorLiquidToScale));
                LiquidRenderer.Instance.Draw(Main.spriteBatch, -sourceTopLeft, Main.waterStyle, Main.liquidAlpha[Main.waterStyle], false);
                Main.spriteBatch.End();
                projectorLiquidProcessing = false;
                Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(Main.graphics.GraphicsDevice);
                Main.screenPosition = sourceTopLeft;
                // LiquidRenderer.Instance.Draw(Main.spriteBatch, -sourceTopLeft, Main.waterStyle, Main.liquidAlpha[Main.waterStyle], false);
            }

            if (structure.data.captureCreature == (int)ProjectorData.CaptureCreatureFlag.All)
            {
                if (!expandFlag)
                {
                    Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, (int)(sourceSize.X), (int)(sourceSize.Y)));
                    Main.GameViewMatrix.Zoom = Vector2.One * structure.data.targetScale;
                    Main.screenPosition = sourceTopLeft + sourceSize * 0.5f * (-1f + 1f / structure.data.targetScale);
                }
                DrawCachedProjsMethod.Invoke(Main.instance, new object[] { Main.instance.DrawCacheProjsOverWiresUI, true });
                Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(Main.graphics.GraphicsDevice);
                Main.screenPosition = sourceTopLeft;
            }


            Main.drawToScreen = origDrawToScreen;
            Main.screenPosition = origScreenPosition;
            Main.screenWidth = origScreenWidth;
            Main.screenHeight = origScreenHeight;
            Main.offScreenRange = origOffscreenRange;
            Main.DefaultSamplerState.Filter = origSampleState;

            Lighting.Mode = origLightingMode;
            Main.renderCount = origRenderCount;
            _activeEngineInfo.SetValue(null, origActiveEngine);

            LiquidRenderer.Instance = origLiquidRenderer;

            Main.mapDelay = origMapDelay;
            Main.mapTime = origMapTime;
            Main.screenLastPosition = origScreenLastPosition;

            Main.GameViewMatrix = origViewMatrix;

            Main.dust = origDust;
            Main.gore = origGore;
            workingDustIdentityList = tempDustIdentityList;
            workingGoreIdentityList = tempGoreIdentityList;

            projectorProcessing = false;

            if (structure.data.behavior != (int)ProjectorData.BehaviorFlag.None)
            {
                if (hitFlag)
                {
                    bool flipX = (structure.data.targetFlip == (int)ProjectorData.FlipFlag.Horizontal) != structure.cacheFollowFlipFlag;
                    bool flipY = (structure.data.targetFlip == (int)ProjectorData.FlipFlag.Vertical);
                    Vector2 playerOrigin = Main.LocalPlayer.sleeping.visualOffsetOfBedBase.Length() > 0 ? Main.LocalPlayer.Size * 0.5f : Main.LocalPlayer.fullRotationOrigin;
                    Vector2 playerEye = ((Main.LocalPlayer.MountedCenter - Vector2.UnitY * 10 + Vector2.UnitX * 2 * Main.LocalPlayer.direction)
                                        - playerOrigin - Main.LocalPlayer.position)
                                        . RotatedBy(Main.LocalPlayer.fullRotation)
                                        + playerOrigin + Main.LocalPlayer.position + Main.LocalPlayer.sleeping.visualOffsetOfBedBase * Main.LocalPlayer.direction;
                    Vector2 referencePosition = (((playerEye - structure.data.targetPoint)
                                                .RotatedBy(-structure.data.targetRotation * MathF.PI / 180f)
                                                / structure.data.targetScale
                                                * new Vector2(flipX ? -1 : 1, flipY ? -1 : 1))
                                                + structure.data.sourcePoint);
                    Point referencePoint = (referencePosition / 16).ToPoint();
                    if (referencePoint.X >= 5 && referencePoint.Y >= 5 && referencePoint.X < Main.maxTilesX - 5 && referencePoint.Y < Main.maxTilesY - 5)
                    {
                        if (!Main.tile[referencePoint].HasTile && Main.tile[referencePoint].WallType == 0)
                            hitFlag = false;
                    }
                    /*
                    referencePoint.X = Utils.Clamp(referencePoint.X, 0, projectorRT.Width - 1);
                    referencePoint.Y = Utils.Clamp(referencePoint.Y, 0, projectorRT.Height - 1);
                    if (referencePoint.X >= 0 && referencePoint.Y >= 0 && referencePoint.X < projectorRT.Width && referencePoint.Y < projectorRT.Height)
                    {
                        projectorRT.GetData(0, new Rectangle(referencePoint.X, referencePoint.Y, 1, 1), colorBuffer, 0, 1);
                        if (colorBuffer[0].A < 128)
                            hitFlag = false;
                    }
                    */
                }
                if (hitFlag)
                    structure.fadingValue = Utils.Clamp(structure.fadingValue + 1 / 20f, 0, 1);
                else
                    structure.fadingValue = Utils.Clamp(structure.fadingValue - 1 / 15f, 0, 1);
            }
            else
                structure.fadingValue = 0;

            HookedDraw_PostProcessing(structure, expandFlag ? sourceSize : targetSize);

            Main.graphics.GraphicsDevice.SetRenderTargets(origTargets);
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, structure.data.blending == (int)ProjectorData.BlendingFlag.Additive ? BlendState.Additive : BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);

            float rotation = structure.data.targetRotation / 180f * MathF.PI;
            SpriteEffects flip = (SpriteEffects)structure.data.targetFlip;
            if (structure.cacheFollowFlipFlag)
                flip ^= SpriteEffects.FlipHorizontally;

            Vector2 effectOffset = Vector2.Zero;
            var alpha = structure.data.colorA;
            if (structure.data.behavior == (int)ProjectorData.BehaviorFlag.HalfFaded)
                alpha *= 1 - 0.5f * structure.fadingValue;
            if (structure.data.behavior == (int)ProjectorData.BehaviorFlag.FullyFaded)
                alpha *= 1 - 1f * structure.fadingValue;
            if (structure.data.behavior == (int)ProjectorData.BehaviorFlag.Distorted)
            {
                // alpha = ((Main.rand.NextFloat() * 0.1f + 0.7f) * structure.fadingValue + (1 - structure.fadingValue)) * alpha;
                alpha *= 1 - 0.1f * structure.fadingValue;
                effectOffset = Vector2.UnitX.RotatedBy(Main.timeForVisualEffects * 1) * structure.fadingValue;
            }

            /*Color color = structure.data.blending == (int)ProjectorData.BlendingFlag.Additive ? 
                new Color(structure.data.colorR, structure.data.colorG, structure.data.colorB, alpha):
                new Color(structure.data.colorR, structure.data.colorG, structure.data.colorB) * alpha;
            */
            Color color = new Color(structure.data.colorR, structure.data.colorG, structure.data.colorB);
            if (structure.data.filter == (int)ProjectorData.FilterFlag.Holographic)
            {
                color.G = color.B = 0;
            }
            if (alpha != 1 || structure.data.colorH != 0 || structure.data.colorS != 0 || structure.data.colorV != 0 || color != Color.White)
            {
                Vector3 uHSV = new Vector3(
                    structure.data.colorH / 180f + (structure.data.filter == (int)ProjectorData.FilterFlag.Holographic ? 180 / 360f: 0),
                    structure.data.colorS / 100f,
                    structure.data.colorV / 100f
                );
                projectorFilterEffect.Parameters["uHSV"].SetValue(uHSV);
                projectorFilterEffect.Parameters["uRGB"].SetValue(color.ToVector3());
                projectorFilterEffect.Parameters["uAlpha"].SetValue(alpha);
                projectorFilterEffect.Parameters["uPrem"].SetValue(structure.data.blending != (int)ProjectorData.BlendingFlag.Additive);
                projectorFilterEffect.CurrentTechnique.Passes["HSV"].Apply();
            }

            Vector2 ori = structure.data.targetPoint - targetTopLeft;
            var scale = structure.data.targetScale;
            if (expandFlag)
            {
                var offsetFlip = new Vector2
                (
                    flip == SpriteEffects.FlipHorizontally ? structure.data.targetSize.X - MathF.Ceiling(sourceSize.X) * scale + 2 * (structure.data.targetTopLeft.X - targetTopLeft.X) : 0,
                    flip == SpriteEffects.FlipVertically ? structure.data.targetSize.Y - MathF.Ceiling(sourceSize.Y) * scale + 2 * (structure.data.targetTopLeft.Y - targetTopLeft.Y) : 0
                );
                offsetFlip = offsetFlip.RotatedBy(structure.data.targetRotation / 180f * MathF.PI);
                Main.spriteBatch.Draw(projectorRT, targetTopLeft - Main.screenPosition + ori + offsetFlip + effectOffset, new Rectangle(0, 0, (int)Math.Ceiling(sourceSize.X), (int)Math.Ceiling(sourceSize.Y)), color,
                    rotation, ori / structure.data.targetScale, structure.data.targetScale, flip, 0);
            }
            else
            {
                var offsetFlip = new Vector2
                (
                    flip == SpriteEffects.FlipHorizontally ? structure.data.targetSize.X - MathF.Ceiling(targetSize.X) + 2 * (structure.data.targetTopLeft.X - targetTopLeft.X) : 0,
                    flip == SpriteEffects.FlipVertically ? structure.data.targetSize.Y - MathF.Ceiling(targetSize.Y) + 2 * (structure.data.targetTopLeft.Y - targetTopLeft.Y) : 0
                );
                offsetFlip = offsetFlip.RotatedBy(structure.data.targetRotation / 180f * MathF.PI);
                Main.spriteBatch.Draw(projectorRT, targetTopLeft - Main.screenPosition + ori + offsetFlip + effectOffset, new Rectangle(0, 0, (int)Math.Ceiling(targetSize.X), (int)Math.Ceiling(targetSize.Y)), color,
                    rotation, ori, 1.0f, flip, 0);
            }
            Main.spriteBatch.End();
            structure.data.targetPoint -= targetFollowOffset;
            structure.data.targetPoint -= structure.cacheParallaxOffset;
            structure.data.targetRotation -= structure.cacheFollowRotation;
        }

        public void HookedDraw_UpdateLighting(ProjectorInstance structure, ILightingEngine currentEngine, Rectangle lightingArea)
        {
            lightingCombination = (ProjectorData.LightingSourceFlag)structure.data.lightingSource;
            referenceLightingCache = structure.referenceLightingCache;

            Main.mapDelay = 99;
            Main.mapTime = 99;

            structure.updateCounter += MathF.Pow(structure.data.lightFreq, 2);
            if (structure.updateCounter >= 1 || CaptureManager.Instance.IsCapturing)
            {
                if (!CaptureManager.Instance.IsCapturing)
                    structure.updateCounter -= 1 + Main.rand.NextFloat() * (1 - structure.data.lightFreq);
                if (currentEngine == projectorLegacyLighting)
                {
                    // Legacy engine will call PreRenderPass when renderCount > RenderPhases
                    // So we set renderCount to a large number to initialize current area
                    Main.renderCount = 99;
                    // Legacy engine will try to reuse data based on screenLastPosition to reduce calculation.
                    // we set screenLastPosition to current position to prevent this behavior (which causes glitches)
                    Main.screenLastPosition = Main.screenPosition;

                    // Actually only 3 phase is funcional in legacy lighting
                    // phase 1 and 2 are for usual lighting
                    // phase 3 is for map update
                    // phase {RenderPhases} is for PreRenderPass
                    // there is no phase 0 (it only occurs when PrePenderPass is called)
                    // Therefore, we need to start with PreRenderPass
                    // and execute the following 2 phases.
                    if (legacyLightingRebuilt)
                    {
                        for (int i = 0; i < 1 + 2; i++)
                        {
                            currentEngine.ProcessArea(lightingArea);
                        }
                    }
                    else
                    {
                        // This rebuild is to prevent IndexOutOfRange error 
                        // when first enter world
                        currentEngine.Rebuild();
                        legacyLightingRebuilt = true;
                    }
                }
                else if (currentEngine is LightingEngine projectorLightingEngine)
                {
                    WritePerframeLightsTo(projectorLightingEngine);
                    // add a random offset to make updates out of sync
                    // and prevent flooding at some point
                    var stateInfo = typeof(LightingEngine).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
                    // SavePerframeLightsFrom(projectorLightingEngine);
                    // The first 2 states are
                    // EngineState.MinimapUpdate and EngineState.ExportMetrics
                    // we only use lighting engine to obtain lighting data
                    // stateInfo.SetValue(projectorLightingEngine, 2);
                    int newPadding = 23;
                    Rectangle uninflate = lightingArea;
                    uninflate.Inflate(newPadding - 28, newPadding - 28);
                    if (!CaptureManager.Instance.IsCapturing)
                    {
                        if ((int)stateInfo.GetValue(projectorLightingEngine) >= 2)
                        {
                            // Finish the last 2 states (Scan and Blur)
                            currentEngine.ProcessArea(uninflate);
                        }
                        else
                            stateInfo.SetValue(projectorLightingEngine, ((int)stateInfo.GetValue(projectorLightingEngine) + 1) % 4);
                    }
                    else // if (structure.data.lightingSource != (int)ProjectorData.LightingSourceFlag.Target)
                    {
                        stateInfo.SetValue(projectorLightingEngine, 2);
                        for (int i = 0; i < 2; i++)
                            currentEngine.ProcessArea(uninflate);
                    }
                }
                if (structure.data.lightingSource != (int)ProjectorData.LightingSourceFlag.Source)
                {
                    lightingArea.Inflate(10, 10);
                    if (structure.referenceLightingCacheSwap.Item2.Length < (lightingArea.Width + 2) * (lightingArea.Height + 2))
                        structure.referenceLightingCacheSwap = new Tuple<Rectangle, Vector3[]>(
                            new Rectangle(lightingArea.X - 1, lightingArea.Y - 1, lightingArea.Width + 2, lightingArea.Height + 2),
                            new Vector3[(lightingArea.Width + 2) * (lightingArea.Height + 2)]);
                    else
                        structure.referenceLightingCacheSwap = new Tuple<Rectangle, Vector3[]>(
                            new Rectangle(lightingArea.X - 1, lightingArea.Y - 1, lightingArea.Width + 2, lightingArea.Height + 2),
                            structure.referenceLightingCacheSwap.Item2);
                    bool flipX = (structure.data.targetFlip == (int)ProjectorData.FlipFlag.Horizontal) != structure.cacheFollowFlipFlag;
                    bool flipY = (structure.data.targetFlip == (int)ProjectorData.FlipFlag.Vertical);
                    var tuple = structure.referenceLightingCacheSwap as Tuple<Rectangle, Vector3[]>;
                    for (int i = lightingArea.X - 1; i < lightingArea.X + lightingArea.Width + 1; i++)
                        for (int j = lightingArea.Y - 1; j < lightingArea.Y + lightingArea.Height + 1; j++)
                        {
                            Vector2 referencePosition = (((new Vector2(i * 16 + 8, j * 16 + 8) - structure.data.sourcePoint)
                                                        * new Vector2(flipX ? -1 : 1, flipY ? -1 : 1))
                                                        .RotatedBy(structure.data.targetRotation * MathF.PI / 180f)
                                                        * structure.data.targetScale
                                                        + structure.data.targetPoint) / 16f;
                            tuple.Item2
                            [
                                (i - tuple.Item1.Left) * tuple.Item1.Height +
                                 j - tuple.Item1.Top
                            ] = origLightingEngineCache.GetColor((int)referencePosition.X, (int)referencePosition.Y) * Lighting.GlobalBrightness;
                        }
                    Utils.Swap(ref structure.referenceLightingCache, ref structure.referenceLightingCacheSwap);
                    referenceLightingCache = structure.referenceLightingCache;
                }
            }
        }

        public void HookedDraw_PostProcessing(ProjectorInstance structure, Vector2 size)
        {
            if (structure.data.filter == (int)ProjectorData.FilterFlag.Border)
            {
                Utils.Swap(ref projectorRT, ref projectorRTSwap);
                Main.graphics.GraphicsDevice.SetRenderTargets(projectorRT);
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, Matrix.Identity);
                projectorFilterEffect.Parameters["uStep"].SetValue(Vector2.One / size);
                projectorFilterEffect.CurrentTechnique.Passes["Border"].Apply();
                Main.spriteBatch.Draw(projectorRTSwap, Vector2.Zero, new Rectangle(0, 0, (int)(size.X), (int)(size.Y)), Color.White);
                Main.spriteBatch.End();
            }
            if (structure.data.filter == (int)ProjectorData.FilterFlag.Blur)
            {
                for (int i = 0; i < 2; i++)
                {
                    Utils.Swap(ref projectorRT, ref projectorRTSwap);
                    Main.graphics.GraphicsDevice.SetRenderTargets(projectorRT);
                    Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Matrix.Identity);
                    projectorFilterEffect.Parameters["uStep"].SetValue(1 * (i == 0 ? Vector2.UnitX : Vector2.UnitY) / size);
                    projectorFilterEffect.CurrentTechnique.Passes["Blur"].Apply();
                    Main.spriteBatch.Draw(projectorRTSwap, Vector2.Zero, new Rectangle(0, 0, (int)(size.X), (int)(size.Y)), Color.White);
                    Main.spriteBatch.End();
                }
            }
        }

        public void DrawStructureFrame()
        {
            if (!uiSystem.projectorUIState.ValidateFocusedProjector())
                return;
            ProjectorInstance structure = uiSystem.projectorUIState.focusedInstance;
            if (structure == null || structure.data == null)
                return;

            Vector2 targetBoundingR = structure.targetSize.RotatedBy(structure.data.targetRotation / 180f * MathF.PI) * 0.5f;
            targetBoundingR.X = MathF.Abs(targetBoundingR.X);
            targetBoundingR.Y = MathF.Abs(targetBoundingR.Y);
            Vector2 targetBoundingR2 = (structure.targetSize * new Vector2(-1, 1)).RotatedBy(structure.data.targetRotation / 180f * MathF.PI) * 0.5f;
            targetBoundingR2.X = MathF.Abs(targetBoundingR2.X);
            targetBoundingR2.Y = MathF.Abs(targetBoundingR2.Y);
            targetBoundingR.X = MathF.Max(targetBoundingR.X, targetBoundingR2.X);
            targetBoundingR.Y = MathF.Max(targetBoundingR.Y, targetBoundingR2.Y);
            Vector2 targetBoundTopLeft = structure.data.targetPoint - targetBoundingR;
            Vector2 targetBoundBottomRight = structure.data.targetPoint + targetBoundingR;

            if (UISystem.Instance.userInterface.CurrentState != null && !CaptureManager.Instance.IsCapturing)
            {
                Main.spriteBatch.Begin(SpriteSortMode.Texture, BlendState.AlphaBlend,
                    SamplerState.PointWrap, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
                var color = structure.TurnedOn() ? (findingTargetFlag ? Color.LightGreen * 0.8f : new Color(1, 0.95f, 0.3f) * 0.8f) : Color.Red * 0.5f;
                Vector2 s0, s1, t0, t1;
                if ((structure.data.sourceFollow != 0 || structure.data.targetFollow != 0))
                {
                    Vector2 followOffset = structure.data.sourceFollow != 0 && findingTargetFlag ?
                                           structure.cacheSourceOffset : Vector2.Zero;
                    s1 = s0 = structure.data.sourcePoint;
                    t0 = structure.tilePosition.ToWorldCoordinates(8, 8);
                    t1 = structure.tilePosition.ToWorldCoordinates(8, 8);
                    s0 += followOffset;
                    s1 += followOffset;
                    t0 += followOffset;
                    t1 += followOffset;
                    if (structure.cacheNeedDraw || !findingTargetFlag)
                    {
                        QuickDrawBox(structure.sourceTopLeft + followOffset, structure.data.sourceSize, color);
                        QuickDrawLine(s0, t0, color);
                        QuickDrawLine(s1, t1, color);
                        QuickDrawLine(new Vector2(s0.X, s1.Y), new Vector2(t0.X, t1.Y), color);
                        QuickDrawLine(new Vector2(s1.X, s0.Y), new Vector2(t1.X, t0.Y), color);
                    }
                    followOffset = structure.data.targetFollow != 0 && findingTargetFlag ?
                                   structure.cacheTargetOffset : Vector2.Zero;
                    s0 = structure.tilePosition.ToWorldCoordinates(8, 8);
                    s1 = structure.tilePosition.ToWorldCoordinates(8, 8);
                    t1 = t0 = structure.data.targetPoint;
                    s0 += followOffset;
                    s1 += followOffset;
                    t0 += followOffset;
                    t1 += followOffset;
                    QuickDrawLine(s0, t0, color);
                    QuickDrawLine(s1, t1, color);
                    QuickDrawLine(new Vector2(s0.X, s1.Y), new Vector2(t0.X, t1.Y), color);
                    QuickDrawLine(new Vector2(s1.X, s0.Y), new Vector2(t1.X, t0.Y), color);
                    QuickDrawBox(structure.targetTopLeft + followOffset, structure.data.targetSize, color);
                    QuickDrawBox(targetBoundTopLeft + followOffset, targetBoundingR * 2, color);
                    if (structure.cacheParallaxOffset.Length() > 0)
                    {
                        color = Color.Pink;
                        QuickDrawLine(structure.tilePosition.ToWorldCoordinates() + followOffset, Main.Camera.Center, color);
                        if (findingTargetFlag)
                        {
                            QuickDrawLine(structure.data.targetPoint + followOffset, followOffset + structure.data.targetPoint + structure.cacheParallaxOffset, color);
                            QuickDrawBox(targetBoundTopLeft + structure.cacheParallaxOffset + followOffset, targetBoundingR * 2, color);
                        }
                    }
                }
                else
                {
                    s0 = structure.data.sourceTopLeft;
                    s1 = structure.data.sourceBottomRight;
                    t0 = structure.data.targetTopLeft;
                    t1 = structure.data.targetBottomRight;
                    QuickDrawLine(s0, t0, color);
                    QuickDrawLine(s1, t1, color);
                    QuickDrawLine(new Vector2(s0.X, s1.Y), new Vector2(t0.X, t1.Y), color);
                    QuickDrawLine(new Vector2(s1.X, s0.Y), new Vector2(t1.X, t0.Y), color);
                    QuickDrawBox(structure.sourceTopLeft, structure.data.sourceSize, color);
                    QuickDrawBox(structure.targetTopLeft, structure.data.targetSize, color);
                    QuickDrawBox(targetBoundTopLeft, targetBoundingR * 2, color);
                    if (structure.cacheParallaxOffset.Length() > 0)
                    {
                        color = Color.Pink;
                        QuickDrawLine(structure.data.targetPoint, structure.data.targetPoint + structure.cacheParallaxOffset, color);
                        QuickDrawLine(structure.tilePosition.ToWorldCoordinates(), Main.Camera.Center, color);
                        QuickDrawBox(targetBoundTopLeft + structure.cacheParallaxOffset, targetBoundingR * 2, color);
                    }
                }
                Main.spriteBatch.End();

                color = Color.White * 0.5f;
                s0 = uiSystem.projectorUIState.HintPoint() + Main.screenPosition;
                s1 = s0 - Vector2.UnitY * 100 * uiSystem.projectorUIState.GetTweenResult();
                t0 = structure.tilePosition.ToWorldCoordinates();
                t1 = t0 + Vector2.UnitY * 100 * uiSystem.projectorUIState.GetTweenResult();
                Vector2 center = (s0 + t0) * 0.5f;
                QuickDrawBezier(s0, center, s1, color, 20);
                QuickDrawBezier(t0, center, t1, color, 20);
            }
        }

        public void DrawProjectorLayer(int drawLayer)
        {
            // Prevent Recursion
            if (projectorProcessing || drawLayer < 0)
                return;

            if (drawLayer == (int)ProjectorData.LayerFlag.BehindWalls)
                PrepareClippingCache();
            bool needDraw = TestNeedDrawProjectors(drawLayer);
            if (needDraw)
            {
                Main.spriteBatch.End();

                GraphicsDevice graphicsDevice = Main.graphics.GraphicsDevice;
                RenderTargetBinding[] origTargets = graphicsDevice.GetRenderTargets();

                // do not draw in retro mode
                if (origTargets.Length > 0)
                {
                    var screenTarget = (RenderTarget2D)origTargets[0].RenderTarget;

                    EnsureOverlayRT(screenTarget.Width, screenTarget.Height);
                    graphicsDevice.SetRenderTarget(overlayRT);
                    graphicsDevice.Clear(Color.Transparent);
                    Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
                    Main.spriteBatch.Draw(screenTarget, Vector2.Zero, Color.White);
                    Main.spriteBatch.End();

                    DrawProjectors(drawLayer);

                    graphicsDevice.SetRenderTarget(screenTarget);

                    Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
                    Main.spriteBatch.Draw(overlayRT, Vector2.Zero, Color.White);
                    Main.spriteBatch.End();
                }

                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
            }

            if (drawLayer == (int)ProjectorData.LayerFlag.Foreground)
            {
                Main.spriteBatch.End();
                if (uiSystem.projectorUIState.focusedInstance != null &&
                    (uiSystem.projectorUIState.focusedInstance.data.sourceFollow != 0 ||
                     uiSystem.projectorUIState.focusedInstance.data.targetFollow != 0))
                {
                    findingTargetFlag = true;
                    DrawStructureFrame();
                    findingTargetFlag = false;
                }
                DrawStructureFrame();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
            }

            if (drawLayer == (int)ProjectorData.LayerFlag.Foreground && delayedSpriteDraw != null)
            {
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
                delayedSpriteDraw(Main.spriteBatch);
                Main.spriteBatch.End();
                delayedSpriteDraw = null;
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
            }

        }

        public bool TestNeedDrawProjectors(int layerFlag)
        {
            foreach (var s in projectorList)
            {
                TileEntity.ByPosition.TryGetValue(new Point16(s.tilePosition.X, s.tilePosition.Y), out var te);
                if (te != null && te is ProjectorTileEntity entity)
                    if (s.data.layer == layerFlag && s.cacheNeedDraw)
                        return true;
            }
            return false;
        }

        public void DrawProjectors(int layerFlag)
        {
            List<ProjectorInstance> drawList = new List<ProjectorInstance>();
            foreach (var s in projectorList)
            {
                TileEntity.ByPosition.TryGetValue(new Point16(s.tilePosition.X, s.tilePosition.Y), out var te);
                if (te != null && te is ProjectorTileEntity entity && entity.projectorInstance != null)
                {
                    if (entity.projectorInstance != s)
                    {
                        continue;
                    }
                    if (s.cacheNeedDraw && s.data.layer == layerFlag)
                        drawList.Add(s);
                }
            }
            foreach (var s in drawList.OrderBy((ProjectorInstance s) => s.data.priority))
            {
                HookedDraw(s);
            }

        }

        public void TileBatchDrawDecorator(On.Terraria.Graphics.TileBatch.orig_Draw_Texture2D_Vector2_Nullable1_VertexColors_Vector2_float_SpriteEffects orig,
                                           TileBatch self, Texture2D texture, Vector2 position, Rectangle? sourceRectangle, VertexColors colors, Vector2 origin, float scale, SpriteEffects effects)
        {
            if (projectorLiquidProcessing)
                scale = projectorLiquidToScale;
            orig(self, texture, position, sourceRectangle, colors, origin, scale, effects);
        }

        public void TileBatchInternalDrawDecorator(On.Terraria.Graphics.TileBatch.orig_InternalDraw orig,
                                           TileBatch self, Texture2D texture, Vector4 destinationRectangle, Rectangle? sourceRectangle, VertexColors colors, float rotation, Vector2 origin, SpriteEffects effect, float depth)
        {
            if (projectorLiquidProcessing)
            {
                destinationRectangle.X *= projectorLiquidToScale;
                destinationRectangle.Y *= projectorLiquidToScale;
                destinationRectangle.Z *= projectorLiquidToScale;
                destinationRectangle.W *= projectorLiquidToScale;
            }
            orig(self, texture, destinationRectangle, sourceRectangle, colors, rotation, origin, effect, depth);
        }

        public Color LightColorDecorator(On.Terraria.Lighting.orig_GetColor_int_int orig, int i, int j)
        {
            // Hack lighting engine to ensure dark tiles to be drawn
            if (projectorProcessing)
            {
                Color result;
                switch (lightingCombination)
                {
                    case ProjectorData.LightingSourceFlag.Source:
                        result = orig(i, j);
                        break;
                    case ProjectorData.LightingSourceFlag.Target:
                        result = new Color(
                            referenceLightingCache.Item1.Contains(i, j) ?  referenceLightingCache.Item2
                                [(i - referenceLightingCache.Item1.Left) * referenceLightingCache.Item1.Height +
                                  j - referenceLightingCache.Item1.Top] :
                                Vector3.Zero);
                        break;
                    case ProjectorData.LightingSourceFlag.Both:
                        result = orig(i, j);
                        result = new Color(result.ToVector3() + 
                            (referenceLightingCache.Item1.Contains(i, j) ?  referenceLightingCache.Item2
                                [(i - referenceLightingCache.Item1.Left) * referenceLightingCache.Item1.Height +
                                  j - referenceLightingCache.Item1.Top] : Vector3.Zero));
                        break;
                    default:
                        return Color.Black;
                }
                if (result.R < 1 && result.G < 1 && result.B < 1)
                {
                    result.R = 1;
                }
                return result;
            }
            else
                return orig(i, j);
        }

        public Color LightOverrideDecorator(On.Terraria.GameContent.Drawing.TileDrawing.orig_DrawTiles_GetLightOverride orig,
                                            TileDrawing self, int j, int i, Tile tileCache, ushort typeCache, short tileFrameX, short tileFrameY, Color tileLight)
        {
            Color result = orig(self, j, i, tileCache, typeCache, tileFrameX, tileFrameY, tileLight);
            return result;
        }

        public int NewGoreDecorator(On.Terraria.Gore.orig_NewGore_IEntitySource_Vector2_Vector2_int_float orig, 
                                    IEntitySource source, Vector2 position, Vector2 velocity, int type, float scale)
        {
            int id = orig(source, position, velocity, type, scale);
            if (workingGoreIdentityList != null && id < Main.maxGore && !(source is EntitySource_TileUpdate) && type != GoreID.FogMachineCloud1)
                workingGoreIdentityList.Add(Main.gore[id]);
            return id;
        }

        public int NewDustDecorator(On.Terraria.Dust.orig_NewDust orig, Vector2 position, int width, int height, int type,
                                    float speedX, float speedY, int alpha, Color color, float scale)
        {
            if (banNewDust)
                return Main.maxDust;
            var origPosition = position;
            if (workingDustIdentityList == tempDustIdentityList)
                position = Main.screenPosition;
            int id = orig(position, width, height, type, speedX, speedY, alpha, color, scale);
            Main.dust[id].position += origPosition - position;
            if (workingDustIdentityList != null && id < Main.maxDust)
                workingDustIdentityList.Add(Main.dust[id]);
            return id;
        }

        public void UpdateDustILEdit(ILContext context)
        {
            ILCursor cursor = new ILCursor(context);
            ILLabel elseBranch = null;
            if (!cursor.TryGotoNext(i => i.MatchBge(out elseBranch)))
                throw new Exception("Immersive Projector: Dust.UpdateDust hook location not found");
            if (!cursor.Previous.MatchLdsfld(out var value))
                throw new Exception("Immersive Projector: Dust.UpdateDust hook location not found");
            ILLabel noReturn = cursor.DefineLabel();
            cursor.GotoLabel(elseBranch);
            cursor.EmitDelegate<Func<bool>>(() => { return this.overrideDustUpdate; });
            cursor.Emit(OpCodes.Brfalse, noReturn);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(noReturn);
        }

        public void LightingInitializeDecorator(On.Terraria.Lighting.orig_Initialize orig)
        {
            orig();
            // Rebuild our lighting engine whenever the vanilla rebuild method is called
            // projectorLightingEngine.Rebuild();
            projectorLegacyLighting.Rebuild();
            foreach (var projector in projectorList)
            {
                projector.updateCounter = 1f;
                projector.lightingEngine.Rebuild();
            }
        }

        public void ScreenEffectDecorator(On.Terraria.Graphics.Effects.FilterManager.orig_EndCapture orig,
                                          Terraria.Graphics.Effects.FilterManager self,
                                          RenderTarget2D finalTexture, RenderTarget2D screenTarget1,
                                          RenderTarget2D screenTarget2, Color clearColor)
        {
            // GraphicsDevice graphicsDevice = Main.instance.GraphicsDevice;
            orig(self, finalTexture, screenTarget1, screenTarget2, clearColor);
        }

        public override void PreUpdateDusts()
        {
            // Update dusts in each projector area
            var origDust = Main.dust;
            var origDCount = Dust.dCount;
            var origMaxDust = Main.maxDustToDraw;
            var origScreenPosition = Main.screenPosition;
            var origScreenWidth = Main.screenWidth;
            var origScreenHeight = Main.screenHeight;
            var origOffscreenRange = Main.offScreenRange;
            FieldInfo _activeEngineInfo = typeof(Lighting).GetField("_activeEngine", BindingFlags.Static | BindingFlags.NonPublic);
            var origActiveEngine = _activeEngineInfo.GetValue(null);
            Main.dust = projectorDust;
            overrideDustUpdate = true;
            try
            {
                foreach (var structure in projectorList)
                    if (structure.cacheNeedDraw)
                    {
                        Main.maxDustToDraw = origMaxDust;
                        Dust.dCount = origDCount;
                        var topLeft = structure.cacheSourceTopLeft;
                        var bottomRight = structure.cacheSourceBottomRight;
                        var size = bottomRight - topLeft;
                        Main.screenPosition = topLeft;
                        Main.screenWidth = (int)size.X + 1;
                        Main.screenHeight = (int)size.Y + 1;
                        // There is a hook that save reference of new dusts in workingDustIdentityList
                        workingDustIdentityList = structure.dustIdentities;
                        foreach (var dust in tempDustIdentityList)
                            if (dust != null && dust.active)
                            {
                                // Copy newly created dusts from real Main.dust
                                // to dust list of this projector
                                int id = Dust.NewDust(dust.position, 0, 0, dust.type);
                                if (id >= Main.maxDust)
                                    continue;
                                Dust newDust = Main.dust[id];
                                newDust.position = dust.position;
                                newDust.velocity = dust.velocity;
                                newDust.fadeIn = dust.fadeIn;
                                newDust.noGravity = dust.noGravity;
                                newDust.scale = dust.scale;
                                newDust.rotation = dust.rotation;
                                newDust.noLight = dust.noLight;
                                newDust.active = dust.active;
                                newDust.type = dust.type;
                                newDust.color = dust.color;
                                newDust.alpha = dust.alpha;
                                newDust.frame = dust.frame;
                                newDust.shader = dust.shader;
                                newDust.customData = dust.customData;
                            }
                        Main.maxDustToDraw = 0;
                        foreach (var i in structure.dustIdentities)
                            if (i != null && i.active)
                            {
                                tempDustBuffer[Main.maxDustToDraw] = i;
                                Main.maxDustToDraw += 1;
                            }
                        if (Main.maxDustToDraw > 0)
                        {
                            Main.dust = tempDustBuffer;
                            _activeEngineInfo.SetValue(null, structure.lightingEngine);
                            banNewDust = true;
                            Dust.UpdateDust();
                            banNewDust = false;
                            Main.dust = projectorDust;
                        }
                        structure.dustIdentities.RemoveAll((Dust i) => i == null || !i.active);
                    }
            }
            catch
            {
                for (int i = 0; i < 6000; i++)
                {
                    projectorDust[i] = new Dust();
                    projectorDust[i].dustIndex = i;
                }
            }
            for (int i = origMaxDust; i < 6000; i++)
                projectorDust[i].active = false;
            overrideDustUpdate = false;
            Main.dust = origDust;
            Main.maxDustToDraw = origMaxDust;
            Main.screenPosition = origScreenPosition;
            Main.screenWidth = origScreenWidth;
            Main.screenHeight = origScreenHeight;
            Main.offScreenRange = origOffscreenRange;
            Dust.dCount = origDCount;
            workingDustIdentityList = tempDustIdentityList;
            tempDustIdentityList.Clear();
            _activeEngineInfo.SetValue(null, origActiveEngine);
        }

        public override void PreUpdateGores()
        {
            var origGore = Main.gore;
            Main.gore = projectorGore;
            workingGoreIdentityList = null;
            foreach (var gore in tempGoreIdentityList)
                if (gore != null && gore.active)
                {
                    int id = Gore.NewGore(new EntitySource_Misc("Immersive Projector"), gore.position, gore.velocity, gore.type);
                    if (id >= Main.maxGore)
                        continue;
                    Gore newDust = Main.gore[id];
                    newDust.position = gore.position;
                    newDust.velocity = gore.velocity;
                    newDust.scale = gore.scale;
                    newDust.rotation = gore.rotation;
                    newDust.active = gore.active;
                    newDust.type = gore.type;
                    newDust.alpha = gore.alpha;
                    newDust.frame = gore.frame;
                    newDust.frameCounter = gore.frameCounter;
                    newDust.behindTiles = gore.behindTiles;
                    newDust.light = gore.light;
                }
            for (int i = 0; i < 600; i++)
            {
                try
                {
                    projectorGore[i].Update();
                }
                catch
                {
                    projectorGore[i] = new Gore();
                }
            }
            tempGoreIdentityList.Clear();
            workingGoreIdentityList = tempGoreIdentityList;
            Main.gore = origGore;
        }

        public override void PostUpdatePlayers()
        {
        }

        public override void PostUpdateEverything()
        {
            // This is when the vanilla update of tile renderer is called
            projectorTileDrawing.Update();
            // Copy pee frame lights from vanilla updates
            FieldInfo engineInfo = typeof(Lighting).GetField("NewEngine", BindingFlags.Static | BindingFlags.NonPublic);
            SavePerframeLightsFrom((LightingEngine)engineInfo.GetValue(null));

            foreach (var structure in projectorList)
                structure.cacheNeedDraw = false;
            ListInWorld.RemoveAll((ProjectorInstance s) =>
            {
                TileEntity.ByPosition.TryGetValue(new Point16(s.tilePosition.X, s.tilePosition.Y), out var te);
                if (te != null && te is ProjectorTileEntity entity)
                {
                    if (entity.projectorInstance == null)
                    {
                        entity.projectorInstance = s;
                        return false;
                    }
                    if (entity.projectorInstance != s)
                    {
                        if (ImmersiveProjector.DEBUG_MODE)
                            Main.NewText("TileEntity Mismatched {0} vs {1}".FormatWith(s.GetHashCode(), entity.projectorInstance.GetHashCode()));
                        return true;
                    }
                }
                return false;
            });
        }

        public void SavePerframeLightsFrom(LightingEngine engine)
        {
            Type typeOfInfo = typeof(LightingEngine).Assembly.GetTypes().Where((x) => x.IsNestedPrivate && x.Name.Equals("PerFrameLight")).ToArray()[0];
            Type arrayOfInfo = typeOfInfo.MakeArrayType();

            var arrayField = typeof(LightingEngine).GetField("_perFrameLights", BindingFlags.Instance | BindingFlags.NonPublic);
            IList perFrameLights = (IList)arrayField.GetValue(engine);

            if (perFrameLightList == null)
                perFrameLightList = (IList)Activator.CreateInstance(perFrameLights.GetType());
            perFrameLightList.Clear();
            foreach (var p in perFrameLights)
                perFrameLightList.Add(p);
        }

        public void WritePerframeLightsTo(LightingEngine engine)
        {
            Type typeOfInfo = typeof(LightingEngine).Assembly.GetTypes().Where((x) => x.IsNestedPrivate && x.Name.Equals("PerFrameLight")).ToArray()[0];

            var arrayField = typeof(LightingEngine).GetField("_perFrameLights", BindingFlags.Instance | BindingFlags.NonPublic);
            IList perFrameLights = (IList)arrayField.GetValue(engine);

            if (perFrameLightList == null)
                return;
            perFrameLights.Clear();
            foreach (var p in perFrameLightList)
                perFrameLights.Add(p);
        }
    }
}
