using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.UI.Elements;
using Terraria.UI;
using static Terraria.WaterfallManager;

namespace ImmersiveProjector.DataStructure
{
    [Serializable]
	public class ProjectorData 
	{
        public enum FlipFlag
        {
            None = 0,
            Horizontal = 1,
            Vertical = 2
        }

        public enum BlendingFlag
        {
            Default = 0,
            Additive = 1
        }

        public enum BehaviorFlag
        {
            None = 0,
            HalfFaded = 1,
            FullyFaded = 2,
            Distorted = 3
        }

        public enum LayerFlag
        {
            Foreground = 0,
            Normal = 1,
            BehindFurnitures = 2,
            BehindWalls = 3
        }
        public enum LightingSourceFlag
        {
            Source = 0,
            Target = 1,
            Both = 2
        }

        public enum FollowingFlag
        {
            None = 0,
            Player = 1,
            TownNPC = 2,
            Boss = 3
        }

        public enum FilterFlag
        {
            None = 0,
            Holographic = 1
        }
        public enum CaptureSolidFlag
        {
            None = 0,
            SolidOnly = 1,
            All = 2
        }
        public enum CaptureWallFlag
        {
            None = 0,
            TileWall = 1
        }
        public enum CaptureCreatureFlag
        {
            None = 0,
            All = 1
        }
        public enum MiscFollowOptionMask
        {
            None = 0,
            FollowRotation = 1,
            FollowSpeedRotation = 2,
            FollowFlip = 4,
            Full = 7
        }

        public int turnedOn { get; set; }
        public Vector2 anchor { get; set; } // (0, 0) to (1, 1)
        public Vector2 sourcePoint { get; set; }
        public Vector2 sourceSize { get; set; }
        public Vector2 targetPoint { get; set; }
        public float targetScale { get; set; }

        public float targetRotation { get; set; }
        public int targetFlip { get; set; }

        public int captureSolid { get; set; }
        public int captureWall { get; set; }
        public int captureCreature { get; set; }

        public int blending { get; set; }
        public int behavior { get; set; }
        public int layer { get; set; }
        public int lightingSource { get; set; }
        public int sourceFollow { get; set; }
        public float sourceFollowId { get; set; }

        public int targetFollow { get; set; }
        public float targetFollowId { get; set; }
        private int _targetFollowMisc;
        public int targetFollowRotation 
        { 
            get => ((_targetFollowMisc & (int)MiscFollowOptionMask.FollowRotation) > 0).ToInt(); 
            set => _targetFollowMisc = (_targetFollowMisc | (int)MiscFollowOptionMask.FollowRotation) ^ ((1 - value) * (int)MiscFollowOptionMask.FollowRotation); 
        }
        public int targetFollowSpeedRotation 
        { 
            get => ((_targetFollowMisc & (int)MiscFollowOptionMask.FollowSpeedRotation) > 0).ToInt(); 
            set => _targetFollowMisc = (_targetFollowMisc | (int)MiscFollowOptionMask.FollowSpeedRotation) ^ ((1 - value) * (int)MiscFollowOptionMask.FollowSpeedRotation); 
        }
        public int targetFollowFlip
        { 
            get => ((_targetFollowMisc & (int)MiscFollowOptionMask.FollowFlip) > 0).ToInt(); 
            set => _targetFollowMisc = (_targetFollowMisc | (int)MiscFollowOptionMask.FollowFlip) ^ ((1 - value) * (int)MiscFollowOptionMask.FollowFlip); 
        }
        public int filter { get; set; }

        public float priority { get; set; }

        public float colorH { get; set; }
        public float colorS { get; set; }
        public float colorV { get; set; }
        public float colorA { get; set; }
        public float parallax { get; set; }
        public float lightFreq { get; set; }
        public float updateFreq { get; set; }
        public float colorR { get; set; }
        public float colorG { get; set; }
        public float colorB { get; set; }


        public ProjectorData()
        {
            SetDefault();
        }

        /*
        private ProjectorData(Vector2 sourcePoint, Vector2 sourceSize, Vector2 targetPoint, Vector2? anchor = null, float targetScale = 1f)
        {
            this.anchor = anchor != null ? (Vector2)anchor : Vector2.Zero;
            this.sourcePoint = sourcePoint;
            this.sourceSize = sourceSize;
            this.targetPoint = targetPoint;
            this.targetScale = targetScale;
        }
        */

        public Vector2 targetSize { get => sourceSize * targetScale; }
        public Vector2 sourceTopLeft { get => sourcePoint - anchor * sourceSize; }
        public Vector2 sourceBottomRight { get => sourcePoint + (Vector2.One - anchor) * sourceSize; }
        public Vector2 targetTopLeft { get => targetPoint - anchor * targetSize;  }
        public Vector2 targetBottomRight { get => targetPoint + (Vector2.One - anchor) * targetSize; }

        public void SetDefaultPosition(Vector2 center)
        {
            anchor = Vector2.One * 0.5f;
            center = 8 * new Vector2(MathF.Round(center.X / 8), MathF.Round(center.Y / 8));
            targetScale = 1f;
            sourcePoint = center + new Vector2(-120, -80);
            sourceSize = Vector2.One * 320;
            targetPoint = sourcePoint - new Vector2(-240, 160);
        }

        public void SetDefault(Vector2? center = null)
        {
            if (center != null)
                SetDefaultPosition((Vector2)center);
            captureSolid = 2;
            captureWall = 1;
            colorA = 1;
            colorH = colorS = colorV = 0;
            turnedOn = 1;
            layer = 3;

            lightFreq = 0.5f;
            updateFreq = 1.0f;
            colorR = colorG = colorB = 1;
        }

        public void CopyDataFrom(ProjectorData other)
        {
            Type type = typeof(ProjectorData);
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetSetMethod() == null)
                    continue;
                property.SetValue(this, property.GetValue(other));
            }
        }

        public bool DataEqualsTo(ProjectorData other)
        {
            Type type = typeof(ProjectorData);
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetSetMethod() == null)
                    continue;
                if (!property.GetValue(this).Equals(property.GetValue(other)))
                    return false;
            }
            return true;
        }

        public string ToJson()
        {
            var options = new JsonSerializerOptions();
            options.IncludeFields = true;
            options.IgnoreReadOnlyFields = true;
            options.IgnoreReadOnlyProperties = true;
            var result = JsonSerializer.Serialize<ProjectorData>(this, options);
            return result;
        }
	}

}
