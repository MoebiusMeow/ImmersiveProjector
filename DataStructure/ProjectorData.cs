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

        [JsonInclude]
        public int turnedOn;
        [JsonInclude]
        public Vector2 anchor; // (0, 0) to (1, 1)
        [JsonInclude]
        public Vector2 sourcePoint;
        [JsonInclude]
        public Vector2 sourceSize;
        [JsonInclude]
        public Vector2 targetPoint;
        [JsonInclude]
        public float targetScale;

        [JsonInclude]
        public float targetRotation;
        [JsonInclude]
        public int targetFlip;

        [JsonInclude]
        public int captureSolid;
        [JsonInclude]
        public int captureWall;
        [JsonInclude]
        public int captureCreature;

        [JsonInclude]
        public int blending;
        [JsonInclude]
        public int behavior;
        [JsonInclude]
        public int layer;
        [JsonInclude]
        public int sourceFollow;
        [JsonInclude]
        public float sourceFollowId;
        [JsonInclude]
        public int targetFollow;
        [JsonInclude]
        public float targetFollowId;
        [JsonInclude]
        public int filter;

        [JsonInclude]
        public float priority;

        [JsonInclude]
        public float colorR;
        [JsonInclude]
        public float colorG;
        [JsonInclude]
        public float colorB;
        [JsonInclude]
        public float colorA;

        public ProjectorData()
        {
        }

        public ProjectorData(Vector2 sourcePoint, Vector2 sourceSize, Vector2 targetPoint, Vector2? anchor = null, float targetScale = 1f)
        {
            this.anchor = anchor != null ? (Vector2)anchor : Vector2.Zero;
            this.sourcePoint = sourcePoint;
            this.sourceSize = sourceSize;
            this.targetPoint = targetPoint;
            this.targetScale = targetScale;
        }

        public Vector2 targetSize { get => sourceSize * targetScale; }
        public Vector2 sourceTopLeft { get => sourcePoint - anchor * sourceSize; }
        public Vector2 sourceBottomRight { get => sourcePoint + (Vector2.One - anchor) * sourceSize; }
        public Vector2 targetTopLeft { get => targetPoint - anchor * targetSize;  }
        public Vector2 targetBottomRight { get => targetPoint + (Vector2.One - anchor) * targetSize; }

        public void SetDefaultPosition(Vector2 center)
        {
            anchor = Vector2.One * 0.5f;
            targetScale = 1f;
            sourcePoint = center + new Vector2(-160, 80);
            sourceSize = Vector2.One * 160;
            targetPoint = sourcePoint - new Vector2(-320, 160);
        }

        public void SetDefault(Vector2 center)
        {
            SetDefaultPosition(center);
            captureSolid = 2;
            captureWall = 1;
            colorA = colorR = colorG = colorB = 1;
            turnedOn = 1;
        }

        public void CopyDataFrom(ProjectorData other)
        {
            Type type = typeof(ProjectorData);
            foreach (var field in type.GetFields())
            {
                field.SetValue(this, field.GetValue(other));
            }
        }

        public bool DataEqualsTo(ProjectorData other)
        {
            Type type = typeof(ProjectorData);
            foreach (var field in type.GetFields())
                if (!field.GetValue(this).Equals(field.GetValue(other)))
                    return false;
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
