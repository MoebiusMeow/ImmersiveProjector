using ImmersiveProjector.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.UI.Elements;
using Terraria.UI;
using static Terraria.WaterfallManager;

namespace ImmersiveProjector.DataStructure
{
	public class ProjectorInstance
    {
        public bool active = true;
        public ProjectorData data;

        public Point tilePosition;

        public int findWaterfallCountdown = 0;
        public int waterfallCount = 0;
        public WaterfallData[] waterfalls = null;

        public float fadingValue;

        public Vector2 targetSize => data.targetSize;
        public Vector2 sourceTopLeft => data.sourceTopLeft;
        public Vector2 sourceBottomRight => data.sourceBottomRight;
        public Vector2 targetTopLeft => data.targetTopLeft;
        public Vector2 targetBottomRight => data.targetBottomRight;
        /*
        public Vector2 anchor { get => data.anchor; set => data.anchor = value }
        public Vector2 sourcePoint { get => data.sourcePoint; set => data.sourcePoint = value; }
        public Vector2 sourceSize { get => data.sourceSize; set => data.sourceSize = value; }
        public Vector2 targetPoint { get => data.targetPoint; set => data.targetPoint = value; }
        public float targetScale { get => data.targetScale; set => data.targetScale = value; }
        */

        private ProjectorInstance(Point tilePosition)
        {
            data = new ProjectorData(Vector2.Zero, Vector2.One * 100, Vector2.Zero);
            this.tilePosition = tilePosition;
        }

        static public ProjectorInstance CreateInstance(Point tilePosition, List<ProjectorInstance> listInWorld)
        {
            var instance = new ProjectorInstance(tilePosition);
            instance.data.SetDefault(tilePosition.ToWorldCoordinates());
            listInWorld.Add(instance);
            return instance;
        }

        public bool TurnedOn()
        {
			if (data == null) return false;
			if (!TileEntity.ByPosition.TryGetValue(tilePosition.ToVector2().ToPoint16(), out var te))
				return false;
			if (!(te is ProjectorTileEntity entity))
				return false;
            return entity.TurnedOn;
        }
        /*
        static public ProjectorInstance CreateInstance(List<ProjectorInstance> pool)
        {
            var instance = new ProjectorInstance();
            for (var i = 0; i < pool.Count; i++)
                if (!pool[i].active)
                {
                    pool[i] = instance;
                    return instance;
                }
            pool.Add(instance);
            return instance;
        }
        */
    }
}
