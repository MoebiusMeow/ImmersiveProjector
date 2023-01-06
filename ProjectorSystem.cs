using Terraria.DataStructures;
using ImmersiveProjector.DataStructure;
using Microsoft.Win32;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using System.Runtime.InteropServices;
using System.Linq;
using Steamworks;
using Terraria.GameInput;
using Terraria.Graphics.Capture;
using Terraria.GameContent.Events;
using System.Linq.Expressions;
using Humanizer;

namespace ImmersiveProjector
{
	class ProjectorSystem : ModSystem
	{
		public bool inited = false;

		public RenderTarget2D projectorRT = null;
		public RenderTarget2D overlayRT = null;


		// Reference to original Tile drawing
		public TilePaintSystemV2 tilePaintSystem = null;
		public WallDrawing wallDrawing = null;

		// Create new tile renderer 
		public TileDrawing projectorTileDrawing;

		// Dust and Gore
		public Dust[] projectorDust;
		public Gore[] projectorGore;

		// Lighting
		// Create new lighting engines
		public LightingEngine projectorLightingEngine = new LightingEngine();
		public LegacyLighting projectorLegacyLighting = new LegacyLighting(Main.Camera);
		private bool legacyLightingRebuilt;
		public IList perFrameLightList = null;

		// Liquid Renderer
		public LiquidRenderer projectorLiquidRenderer;
		private bool projectorLiquidProcessing = false;
		private float projectorLiquidToScale = 1f;

		// Hook flag
		private bool projectorProcessing = false;

		private bool findingTargetFlag = false;

		public UISystem uiSystem { get => ModContent.GetInstance<UISystem>(); }

		public List<ProjectorInstance> projectorList;
		public static List<ProjectorInstance> ListInWorld { get => ModContent.GetInstance<ProjectorSystem>().projectorList; }

		public Effect projectorRangeDisplayEffect;

		public Action<SpriteBatch> delayedSpriteDraw;

		// For faster reflection
		// WIP
		private Dictionary<string, FieldInfo> _fieldCache = new();
		private Dictionary<string, Func<object, object>> _fieldGetterCache = new();
		private Dictionary<string, Action<object, object>> _fieldSetterCache = new();

		private Dictionary<string, MethodInfo> _methodInfoCache = new();
		private Dictionary<string, Action<object>> _methodInvokerCache = new();


		private void PrepareFastInstancedFieldReflection<T>(T instance, string field, BindingFlags bindingFlags)
        {
			string key = string.Concat(instance.GetType().Name, ".", field);
            FieldInfo fieldInfo = instance.GetType().GetField(field, bindingFlags);
			_fieldCache[key] = fieldInfo;
            ParameterExpression param0 = Expression.Parameter(typeof(object), "instance");
            ParameterExpression param1 = Expression.Parameter(typeof(object), "value");

            UnaryExpression instanceCast = !fieldInfo.DeclaringType.IsValueType ?
                                               Expression.TypeAs(param0, fieldInfo.DeclaringType):
                                               Expression.Convert(param0, fieldInfo.DeclaringType);

			// These two doesn't really improve performance
			// currently placeholders
            Func<object, object> getValueDelegate = Expression.Lambda<Func<object, object>>(
				Expression.TypeAs(
					Expression.Call(instanceCast, typeof(FieldInfo).GetMethod("GetValue", BindingFlags.Instance | BindingFlags.Public)),
                typeof(object)), param0).Compile();

            Action<object, object> setValueDelegate = Expression.Lambda<Action<object, object>>(
                Expression.Call(typeof(FieldInfo).GetMethod("SetValue", BindingFlags.Instance | BindingFlags.Public), param0, param1),
                param0, param1).Compile();

			_fieldGetterCache[key] = getValueDelegate;
			_fieldSetterCache[key] = setValueDelegate;
        }

		private void PrepareFastInstancedMethodReflection<T>(T instance, string method, BindingFlags bindingFlags)
        {
			string key = string.Concat(instance.GetType().Name, ".", method);
            MethodInfo methodInfo = instance.GetType().GetMethod(method, bindingFlags);
			_methodInfoCache[key] = methodInfo;
            ParameterExpression param0 = Expression.Parameter(instance.GetType(), "instance");
			// ParameterExpression param1 = Expression.Parameter(typeof(object[]), "params");

			// MethodCallExpression call = Expression.Call(param0, methodInfo);
			// Action<object> invokeDelegate = (Action<object>)Expression.Lambda(call, param0).Compile();
			Action<object> invokeDelegate = (Action<object>)Delegate.CreateDelegate(typeof(Action<Main>), methodInfo);
			_methodInvokerCache[key] = invokeDelegate;
        }

		public object FastFieldGet<T>(T instance, string field, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic)
        {
			string key = string.Concat(instance.GetType().Name, ".", field);
			if (!_fieldCache.ContainsKey(key))
				PrepareFastInstancedFieldReflection(instance, field, bindingFlags);
			return _fieldGetterCache[key](instance);
        }

		public void FastFieldSet<T>(T instance, string field, object value, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic)
        {
			string key = string.Concat(instance.GetType().Name, ".", field);
			if (!_fieldCache.ContainsKey(key))
				PrepareFastInstancedFieldReflection(instance, field, bindingFlags);
			_fieldSetterCache[key](instance, value);
        }
		public void FastInvoke<T>(T instance, string method, object[] param, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic)
        {
			string key = string.Concat(instance.GetType().Name, ".", method);
			if (!_methodInfoCache.ContainsKey(key))
				PrepareFastInstancedMethodReflection(instance, method, bindingFlags);
			_methodInvokerCache[key](instance);
        }

		public ProjectorSystem()
		{
		}

		public override void OnWorldLoad()
		{
			inited = false;
			// Lighting.Mode = LightMode.Color;
			projectorLightingEngine.Rebuild();
			projectorLegacyLighting.Rebuild();
			projectorDust = new Dust[6002];
			projectorGore = new Gore[602];
			for (int i = 0; i <= 6000; i++)
			{
				projectorDust[i] = new Dust();
				projectorDust[i].dustIndex = i;
			}
			for (int i = 0; i <= 600; i++)
				projectorGore[i] = new Gore();

			projectorList = new List<ProjectorInstance>();
			base.OnWorldLoad();
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
			On.Terraria.Main.DrawCachedNPCs += DrawCachedNPCsDecorator;
			On.Terraria.Lighting.Initialize += LightingInitializeDecorator;
			legacyLightingRebuilt = false;
			base.Load();
		}

		public override void OnModLoad()
		{
			projectorRangeDisplayEffect = Mod.Assets.Request<Effect>("Effects/ProjectorRangeDisplay", AssetRequestMode.ImmediateLoad).Value;
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
			On.Terraria.Main.DrawCachedNPCs -= DrawCachedNPCsDecorator;
			On.Terraria.Lighting.Initialize -= LightingInitializeDecorator;
			base.Unload();
		}

		public void EnsureProjectorRT(float width, float height)
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
		public void EnsureOverlayRT(float width, float height)
		{
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

		public void QuickDrawBox(Vector2 position, Vector2 size, Color color)
		{
			QuickDrawBoxLocal(position - Main.screenPosition, size, color);
		}

		public void QuickDrawBoxLocal(Vector2 position, Vector2 size, Color color)
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

		public void QuickDrawLineLocal(Vector2 from, Vector2 to, Color color)
		{
			SpriteBatch spriteBatch = Main.spriteBatch;
			Rectangle simpleRect = new Rectangle(0, 0, 1, 1);
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, from, simpleRect, color, MathF.Atan2(to.Y - from.Y, to.X - from.X),
				Vector2.UnitY * 0.5f, new Vector2((to - from).Length(), 2f), SpriteEffects.None, 0f);
		}

		public void QuickDrawLine(Vector2 from, Vector2 to, Color color)
		{
			QuickDrawLineLocal(from - Main.screenPosition, to - Main.screenPosition, color);
			if (from != to && false)
			{
				Vector2 dir = Vector2.Normalize(to - from) * 10;
                QuickDrawLineLocal(to - dir.RotatedBy(MathF.PI / 10) - Main.screenPosition, to - Main.screenPosition, color);
                QuickDrawLineLocal(to - dir.RotatedBy(-MathF.PI / 10) - Main.screenPosition, to - Main.screenPosition, color);
			}
		}
		public void QuickDashLineLocal(Vector2 from, Vector2 to, Color color)
		{
			if (from == to)
				return;
			float step = 8f / (from - to).Length();
			float t = (float)Main.timeForVisualEffects * 0.02f;
			for (float v = (t - MathF.Floor(t) - 1) * 2 * step; v < 1; v += 2 * step)
				QuickDrawLineLocal(from * (1 - Utils.Clamp(v, 0, 1)) + to * Utils.Clamp(v, 0, 1), from * (1 - Utils.Clamp(v + step, 0, 1)) + to * Utils.Clamp(v + step, 0, 1), color);
		}

		public void QuickDrawBezier(Vector2 from, Vector2 to, Vector2 control, Color color, int steps = 10)
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

		public void HookedDraw(ProjectorInstance structure)
		{
			var targetSize = structure.data.targetSize;
			var targetFollowOffset = Vector2.Zero;

			switch (structure.data.targetFollow)
			{
				case (int)ProjectorData.FollowingFlag.Player:
					for (var i = 0; i < Main.player.Length; i++)
					{
						var player = Main.player[(i + (int)(structure.data.targetFollowId * Main.player.Length)) % Main.npc.Length];
						if (player.active)
						{
							targetFollowOffset = -structure.data.targetPoint + player.Center;
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
							break;
						}
					}
					break;
			}
			structure.data.targetPoint += targetFollowOffset;

			Vector2 targetBoundingR = targetSize.RotatedBy(structure.data.targetRotation / 180f * MathF.PI) * 0.5f;
			targetBoundingR.X = MathF.Abs(targetBoundingR.X);
			targetBoundingR.Y = MathF.Abs(targetBoundingR.Y);
			Vector2 targetBoundingR2 = (targetSize * new Vector2(-1, 1)).RotatedBy(structure.data.targetRotation / 180f * MathF.PI) * 0.5f;
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
				structure.data.targetPoint -= targetFollowOffset;
				return;
			}

            var hitFlag = (targetBoundTopLeft.X < Main.LocalPlayer.position.X + Main.LocalPlayer.width && targetBoundBottomRight.X > Main.LocalPlayer.position.X
                        && targetBoundTopLeft.Y < Main.LocalPlayer.position.Y + Main.LocalPlayer.height && targetBoundBottomRight.Y > Main.LocalPlayer.position.Y);
			if (structure.data.behavior != (int)ProjectorData.BehaviorFlag.None)
			{
				if (hitFlag)
					structure.fadingValue = Utils.Clamp(structure.fadingValue + 1 / 20f, 0, 1);
				else
					structure.fadingValue = Utils.Clamp(structure.fadingValue - 1 / 15f, 0, 1);
			}
			else
				structure.fadingValue = 0;

			float clipL = Main.screenPosition.X + 00 - 4;
			float clipT = Main.screenPosition.Y + 00 - 4;
			float clipR = Main.screenPosition.X + Main.screenWidth + 00 + 4;
			float clipB = Main.screenPosition.Y + Main.screenHeight + 00 + 4;

			targetBoundTopLeft = new Vector2
            (
                MathF.Max(targetBoundTopLeft.X, clipL),
                MathF.Max(targetBoundTopLeft.Y, clipT)
            );

			targetBoundBottomRight = new Vector2
            (
                MathF.Min(targetBoundBottomRight.X, clipR),
                MathF.Min(targetBoundBottomRight.Y, clipB)
            );

			if (targetBoundTopLeft.X > targetBoundBottomRight.X || targetBoundTopLeft.Y > targetBoundBottomRight.Y)
			{
				structure.data.targetPoint -= targetFollowOffset;
				return;
			}

			Vector2 targetBoundingT1 = (targetBoundTopLeft - structure.data.targetPoint).RotatedBy(-structure.data.targetRotation / 180f * MathF.PI);
			Vector2 targetBoundingT2 = (new Vector2(targetBoundTopLeft.X, targetBoundBottomRight.Y) - structure.data.targetPoint).RotatedBy(-structure.data.targetRotation / 180f * MathF.PI);
			Vector2 targetBoundingT3 = (targetBoundBottomRight - structure.data.targetPoint).RotatedBy(-structure.data.targetRotation / 180f * MathF.PI);
			Vector2 targetBoundingT4 = (new Vector2(targetBoundBottomRight.X, targetBoundTopLeft.Y) - structure.data.targetPoint).RotatedBy(-structure.data.targetRotation / 180f * MathF.PI);

			if (structure.data.targetFlip == (int)ProjectorData.FlipFlag.Horizontal)
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

			var targetTopLeft = new Vector2
            (
                MathF.Max(expandTargetBoundTopLeft.X, structure.data.targetTopLeft.X),
                MathF.Max(expandTargetBoundTopLeft.Y, structure.data.targetTopLeft.Y)
            );

			var targetBottomRight = new Vector2
            (
                MathF.Min(expandTargetBoundBottomRight.X, structure.data.targetBottomRight.X),
                MathF.Min(expandTargetBoundBottomRight.Y, structure.data.targetBottomRight.Y)
            );

			// Rounding down top-left corner to ensure that the clipped area is aligned
			// otherwise there would be annoying sub-pixel glitches

			// This is quite complex since this mod implement rendering in 2 different mode
			// if the target scale is greater than 1, a render target of the size of source area is used and then scaled up to draw
			// if the target scale is less than 1, a render target of the size of target area is used. 
			bool expandFlag = (structure.data.targetScale >= 1.0f);
			float align = expandFlag ?
				1 * structure.data.targetScale : // 1 pixel in source rect
				2; // 2 pixels in target rect
            targetTopLeft = (targetTopLeft - structure.data.targetTopLeft) / align;
            targetTopLeft = new Vector2(MathF.Floor(targetTopLeft.X), MathF.Floor(targetTopLeft.Y)) * align + structure.data.targetTopLeft;

			targetSize = targetBottomRight - targetTopLeft;
			if (targetSize.X <= 0 || targetSize.Y <= 0)
				return;

			var sourceTopLeft = (targetTopLeft - structure.data.targetPoint) / structure.data.targetScale + structure.data.sourcePoint;
			var sourceBottomRight = (targetBottomRight - structure.data.targetPoint) / structure.data.targetScale + structure.data.sourcePoint;
			var sourceSize = sourceBottomRight - sourceTopLeft;
			var sourceFollowOffset = Vector2.Zero;

			switch (structure.data.sourceFollow)
			{
				case (int)ProjectorData.FollowingFlag.Player:
					for (var i = 0; i < Main.player.Length; i++)
					{
						var player = Main.player[(i + (int)(structure.data.sourceFollowId * Main.player.Length)) % Main.npc.Length];
						if (player.active)
						{
							sourceFollowOffset = -structure.data.sourcePoint + player.Center;
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
							break;
						}
					}
					break;
			}
			sourceTopLeft += sourceFollowOffset;
			sourceBottomRight += sourceFollowOffset;
			
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

			var origDust = Main.dust;
			var origGore = Main.gore;
			// Hook newly created dust and gore
			Main.dust = projectorDust;
			Main.gore = projectorGore;

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
			var origActiveEngine = _activeEngineInfo.GetValue(null);

			var origLiquidRenderer = LiquidRenderer.Instance;
			LiquidRenderer.Instance = projectorLiquidRenderer;

			GraphicsDevice graphicDevice = Main.graphics.GraphicsDevice;

			// Lighting.Mode = LightMode.Color;

			Rectangle lightingArea = new Rectangle(Math.Max(5, (int)(sourceTopLeft.X / 16)), Math.Max(5, (int)(sourceTopLeft.Y / 16)),
												   (int)(sourceSize.X / 16) + 1, (int)(sourceSize.Y / 16) + 1);
            Rectangle liquidArea = new Rectangle(Math.Max(5, lightingArea.X - 1) - 2, Math.Max(5, lightingArea.Y - 1),
                                                 Math.Min(Main.maxTilesX - 5 - lightingArea.X, lightingArea.Width + 3) + 2,
                                                 Math.Min(Main.maxTilesY - 5 - lightingArea.Y, lightingArea.Height + 6) + 4);
            // These magical numbers cannot be changed for some reason

			ILightingEngine currentEngine = origLightingMode == LightMode.Color ? projectorLightingEngine : projectorLegacyLighting;
			projectorLegacyLighting.Mode = Lighting.LegacyEngine.Mode;

			_activeEngineInfo.SetValue(null, currentEngine);
			Main.mapDelay = 99;
			Main.mapTime = 99;
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
			else
			{
				WritePerframeLightsTo(projectorLightingEngine);
				var stateInfo = typeof(LightingEngine).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
                // The first 2 states are
				// EngineState.MinimapUpdate and EngineState.ExportMetrics
				// we only use lighting engine to obtain lighting data
				stateInfo.SetValue(projectorLightingEngine, 2);
				// Finish the last 2 states (Scan and Blur)
				for (int i = 0; i < 2; i++)
					currentEngine.ProcessArea(lightingArea);
			}



			if (expandFlag)
				EnsureProjectorRT(sourceSize.X, sourceSize.Y);
			else
				EnsureProjectorRT(targetSize.X, targetSize.Y);

			Main.drawToScreen = false;
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


			// Save states of special tiles
			/*
			FieldInfo _specialTilesCountInfo = typeof(TileDrawing).GetField("_specialTilesCount", BindingFlags.Instance | BindingFlags.NonPublic);
			var specialTilesCount = _specialTilesCountInfo.GetValue(tileDrawing);

			FieldInfo _specialsCountInfo = typeof(TileDrawing).GetField("_specialsCount", BindingFlags.Instance | BindingFlags.NonPublic);
			int[] _specialsCount = (int[])_specialsCountInfo.GetValue(tileDrawing);
			int[] specialsCount = (int[])_specialsCount.Clone();
			*/

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
				Main.spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None,
					Main.Rasterizer, null, expandFlag ? Matrix.Identity : Matrix.CreateScale(structure.data.targetScale));
                if (structure.data.captureSolid == (int)ProjectorData.CaptureSolidFlag.All)
                    projectorTileDrawing.Draw(false, false /* Unused */, false, -1);
				HackSpriteBatchScale(Main.spriteBatch, 1 + 0.1f / 16f / structure.data.targetScale);
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

			// Restore state of special points
			/*
			_specialTilesCountInfo.SetValue(tileDrawing, specialTilesCount);
			for (var i = 0; i < specialsCount.Length; i++)
				if (_specialsCount[i] != specialsCount[i])
                    _specialsCount[i] = specialsCount[i];
			*/

			// Tiles part 2
			if (structure.data.captureSolid != (int)ProjectorData.CaptureSolidFlag.None)
			{
                projectorTileDrawing.PreDrawTiles(true, true, false);
				Main.spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None,
					Main.Rasterizer, null, expandFlag ? Matrix.Identity : Matrix.CreateScale(structure.data.targetScale));
				projectorTileDrawing.Draw(true, false /* Unused */, false, -1);
				HackSpriteBatchScale(Main.spriteBatch, 1 + 0.5f / 16f / structure.data.targetScale);
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
				info.Invoke(Main.instance, new object[] {  });
				info = typeof(Main).GetMethod("DrawPlayers_AfterProjectiles", BindingFlags.Instance | BindingFlags.NonPublic);
				info.Invoke(Main.instance, new object[] {  });

                DrawCachedProjsMethod.Invoke(Main.instance, new object[] { Main.instance.DrawCacheProjsOverPlayers, true });

                Main.spriteBatch.Begin();
                DrawCachedNPCsMethod.Invoke(Main.instance, new object[] { Main.instance.DrawCacheNPCsOverPlayers, false});
                Main.spriteBatch.End();

                // DrawItems();
				// FastInvoke(Main.instance, "DrawItems", new object[] { });
                // DrawRain();
				// FastInvoke(Main.instance, "DrawRain", new object[] { });
                // DrawGore();
				// FastInvoke(Main.instance, "DrawGore", new object[] { });

				// There is some problem causing DrawGore to fail (Perhaps IL stuffs)
				info = typeof(Main).GetMethod("DrawGore", BindingFlags.Instance | BindingFlags.NonPublic);
				// info.Invoke(Main.instance, new object[] {  });
                // DrawDust();
				// FastInvoke(Main.instance, "DrawDust", new object[] { });
				info = typeof(Main).GetMethod("DrawDust", BindingFlags.Instance | BindingFlags.NonPublic);
				info.Invoke(Main.instance, new object[] {  });

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
				LiquidRenderer.Instance.Draw(Main.spriteBatch, -sourceTopLeft, Main.waterStyle, Main.liquidAlpha[Main.waterStyle], true);
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

			// Restore state of special points
			/*
			_specialTilesCountInfo.SetValue(tileDrawing, specialTilesCount);
			for (var i = 0; i < specialsCount.Length; i++)
				if (_specialsCount[i] != specialsCount[i])
                    _specialsCount[i] = specialsCount[i];
			*/

			// void Draw(bool solidLayer, bool forRenderTargets, bool intoRenderTargets, int waterStyleOverride = -1)

			Main.drawToScreen = origDrawToScreen;
			Main.screenPosition = origScreenPosition;
			Main.screenWidth = origScreenWidth;
			Main.screenHeight = origScreenHeight;
			Main.offScreenRange = origOffscreenRange;

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

			projectorProcessing = false;

			// Main.graphics.GraphicsDevice.SetRenderTarget(overlayRT);
			Main.graphics.GraphicsDevice.SetRenderTargets(origTargets);
			Main.spriteBatch.Begin(SpriteSortMode.Texture, structure.data.blending == (int)ProjectorData.BlendingFlag.Additive ? BlendState.Additive : BlendState.AlphaBlend,
				Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);

			float rotation = structure.data.targetRotation / 180f * MathF.PI;
			SpriteEffects flip = (SpriteEffects)structure.data.targetFlip;

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

			Color color = structure.data.blending == (int)ProjectorData.BlendingFlag.Additive ? 
				new Color(structure.data.colorR, structure.data.colorG, structure.data.colorB, alpha):
				new Color(structure.data.colorR, structure.data.colorG, structure.data.colorB) * alpha;

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
			// Main.spriteBatch.Draw(projectorRT, targetTopLeft - Main.screenPosition,  Color.White);

			// QuickDrawBoxLocal(Vector2.One * 400, new Vector2(Main.screenWidth, Main.screenHeight) - Vector2.One * 800, Color.White);
            Main.spriteBatch.End();
			if (targetFollowOffset.Length() > 0 || sourceFollowOffset.Length() > 0)
			{
				findingTargetFlag = true;
				structure.data.sourcePoint += sourceFollowOffset;
				DrawStructureFrame();
				findingTargetFlag = false;
				structure.data.sourcePoint -= sourceFollowOffset;
				structure.data.targetPoint -= targetFollowOffset;
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
                    Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
				var color = structure.TurnedOn() ? (findingTargetFlag ? Color.LightGreen * 0.5f : new Color(1, 0.95f, 0.3f) * 0.8f) : Color.Red * 0.5f;
				QuickDrawBox(structure.sourceTopLeft, structure.data.sourceSize, color);
				QuickDrawBox(structure.targetTopLeft, structure.data.targetSize, color);
				QuickDrawBox(targetBoundTopLeft, targetBoundingR * 2, color);
				Vector2 s0 = structure.data.sourceTopLeft;
				Vector2 s1 = structure.data.sourceBottomRight;
				Vector2 t0 = structure.data.targetTopLeft;
				Vector2 t1 = structure.data.targetBottomRight;
				QuickDrawLine(s0, t0, color);
				QuickDrawLine(s1, t1, color);
				QuickDrawLine(new Vector2(s0.X, s1.Y), new Vector2(t0.X, t1.Y), color);
				QuickDrawLine(new Vector2(s1.X, s0.Y), new Vector2(t1.X, t0.Y), color);
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
			Color result = orig(i, j);
			// Hack lighting engine to ensure dark tiles to be drawn
			if(projectorProcessing)
                if (result.R < 1 && result.G < 1 && result.B < 1)
                    result.R = 1;
			return result;
		}
		public Color LightOverrideDecorator(On.Terraria.GameContent.Drawing.TileDrawing.orig_DrawTiles_GetLightOverride orig,
											TileDrawing self, int j, int i, Tile tileCache, ushort typeCache, short tileFrameX, short tileFrameY, Color tileLight)
		{
			Color result = orig(self, j, i, tileCache, typeCache, tileFrameX, tileFrameY, tileLight);
			return result;
		}

		public void LightingInitializeDecorator(On.Terraria.Lighting.orig_Initialize orig)
		{
			orig();
			// Rebuild our lighting engine whenever the vanilla rebuild method is called
			projectorLightingEngine.Rebuild();
			projectorLegacyLighting.Rebuild();
		}

		public void DrawCachedNPCsDecorator(On.Terraria.Main.orig_DrawCachedNPCs orig, Main self,
											List<int> cachedList, bool behindTiles)
		{
			int drawLayer = -1;
			if (cachedList == Main.instance.DrawCacheNPCsMoonMoon)
                drawLayer = (int)ProjectorData.LayerFlag.BehindWalls;
            if (cachedList == Main.instance.DrawCacheNPCsBehindNonSolidTiles)
                drawLayer = (int)ProjectorData.LayerFlag.BehindFurnitures;
            if (cachedList == Main.instance.DrawCacheNPCProjectiles)
                drawLayer = (int)ProjectorData.LayerFlag.Normal;
            if (cachedList == Main.instance.DrawCacheNPCsOverPlayers)
                drawLayer = (int)ProjectorData.LayerFlag.Foreground;

			// Prevent Recursion
			if (projectorProcessing)
				drawLayer = -1;

			if (drawLayer != -1)
			{
				bool needDraw = TestNeedDrawProjectors(drawLayer);
				if (needDraw)
				{
					Main.spriteBatch.End();

                    GraphicsDevice graphicsDevice = Main.graphics.GraphicsDevice;
                    RenderTargetBinding[] origTargets = graphicsDevice.GetRenderTargets();
					if (origTargets.Length > 0)
					{
						var screenTarget = (RenderTarget2D)origTargets[0].RenderTarget;

						EnsureOverlayRT(screenTarget.Width, screenTarget.Height);
						graphicsDevice.SetRenderTarget(overlayRT);
						// graphicsDevice.Clear(Color.Transparent);
						Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
						Main.spriteBatch.Draw(screenTarget, Vector2.Zero, Color.White);
						Main.spriteBatch.End();

						DrawProjectors(drawLayer);

						graphicsDevice.SetRenderTarget(screenTarget);

						Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
						Main.spriteBatch.Draw(overlayRT, Vector2.Zero, Color.White);
						Main.spriteBatch.End();
					}

                    Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
				}

                if (cachedList == Main.instance.DrawCacheNPCsOverPlayers)
                {
                    Main.spriteBatch.End();
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

            orig(self, cachedList, behindTiles);
		}

		public bool TestProjectorOutOfScreen(ProjectorInstance structure)
		{
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

			// Target out of screen
			if (uiSystem.projectorUIState.focusedInstance != structure &&
				(targetBoundTopLeft.X > Main.screenPosition.X + Main.screenWidth || targetBoundBottomRight.X < Main.screenPosition.X ||
				 targetBoundTopLeft.Y > Main.screenPosition.Y + Main.screenHeight || targetBoundBottomRight.Y < Main.screenPosition.Y))
				return true;
			return false;
		}

		public bool TestNeedDrawProjectors(int layerFlag)
		{
			foreach (var s in projectorList)
			{
				TileEntity.ByPosition.TryGetValue(new Point16(s.tilePosition.X, s.tilePosition.Y), out var te);
				if (te != null && te is ProjectorTileEntity entity)
				{
					if (entity.projectorInstance != s)
					{
						continue;
					}
					if (entity.TurnedOn && s.data.layer == layerFlag && !TestProjectorOutOfScreen(s))
						return true;
				}
			}
			return false;
		}

		public void DrawProjectors(int layerFlag)
		{
            FieldInfo _drawAreaInfo;
            Rectangle drawArea = new Rectangle(0, 0, 1, 1);
			if (layerFlag == (int)ProjectorData.LayerFlag.Foreground)
			{
				_drawAreaInfo = typeof(LiquidRenderer).GetField("_drawArea", BindingFlags.Instance | BindingFlags.NonPublic);
				drawArea = (Rectangle)_drawAreaInfo.GetValue(LiquidRenderer.Instance);
			}

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
					if (entity.TurnedOn && s.data.layer == layerFlag)
						drawList.Add(s);
				}
			}
			foreach (var s in drawList.OrderBy((ProjectorInstance s) => s.data.priority))
				HookedDraw(s);

			if (layerFlag == (int)ProjectorData.LayerFlag.Foreground)
			{
				// Restore water cache
				LiquidRenderer.Instance.PrepareDraw(drawArea);
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
			var origDust = Main.dust;
			Main.dust = projectorDust;
            try
            {
                Dust.UpdateDust();
            }
            catch
            {
                for (int i = 0; i < 6000; i++)
                {
                    projectorDust[i] = new Dust();
                    projectorDust[i].dustIndex = i;
                }
            }
			Main.dust = origDust;
        }

        public override void PostUpdateEverything()
		{
			// This is when the vanilla update of tile renderer is called
			projectorTileDrawing.Update();
			// Copy pee frame lights from vanilla updates
			FieldInfo engineInfo = typeof(Lighting).GetField("NewEngine", BindingFlags.Static | BindingFlags.NonPublic);
			SavePerframeLightsFrom((LightingEngine)engineInfo.GetValue(null));

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

		// Hack for XNA sprite batch
		// Hack XNA sprite batch by adding scales to eradicate gaps between tiles
		public void HackSpriteBatchScale(SpriteBatch spriteBatch, float scaleFactor)
		{
			Type typeOfSpriteInfo = typeof(SpriteBatch).Assembly.GetTypes().Where((x) => x.IsNestedPrivate && x.Name.Equals("SpriteInfo")).ToArray()[0];
			Type arrayOfSpriteInfo = typeOfSpriteInfo.MakeArrayType();

			var spriteInfosField = typeof(SpriteBatch).GetField("spriteInfos", BindingFlags.Instance | BindingFlags.NonPublic);
			Array spriteInfos = (Array)spriteInfosField.GetValue(spriteBatch);

			var numSpritesField = typeof(SpriteBatch).GetField("numSprites", BindingFlags.Instance | BindingFlags.NonPublic);
			int numSprites = (int)numSpritesField.GetValue(spriteBatch);

			var destinationWField = typeOfSpriteInfo.GetField("destinationW", BindingFlags.Public | BindingFlags.Instance);
			var destinationHField = typeOfSpriteInfo.GetField("destinationH", BindingFlags.Public | BindingFlags.Instance);
			var depthField = typeOfSpriteInfo.GetField("depth", BindingFlags.Public | BindingFlags.Instance);

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

		public Matrix CreateMVP(Vector2 worldPosOffset)
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
