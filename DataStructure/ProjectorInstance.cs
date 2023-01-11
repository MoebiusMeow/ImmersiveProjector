using ImmersiveProjector.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.UI.Elements;
using Terraria.Graphics.Light;
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

        public LightingEngine lightingEngine;

        public List<Dust> dustIdentities;
        // public List<Gore> goreIdentities;

        public Vector2 cacheTopLeft;
        public Vector2 cacheBottomRight;
        public Vector2 cacheTargetOffset;
        public bool cacheHitFlag;
        public bool cacheNeedDraw;
        public Vector2 cacheSourceTopLeft;
        public Vector2 cacheSourceBottomRight;

        public float fadingValue;

        public Vector2 targetSize => data.targetSize;
        public Vector2 sourceTopLeft => data.sourceTopLeft;
        public Vector2 sourceBottomRight => data.sourceBottomRight;
        public Vector2 targetTopLeft => data.targetTopLeft;
        public Vector2 targetBottomRight => data.targetBottomRight;

        private ProjectorInstance(Point tilePosition)
        {
            data = new ProjectorData(Vector2.Zero, Vector2.One * 100, Vector2.Zero);
            lightingEngine = new LightingEngine();
            lightingEngine.Rebuild();
            dustIdentities = new List<Dust>();
            // goreIdentities = new List<Gore>();
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
			if (data == null)
                return false;
            return data.turnedOn > 0;
            /*
			if (!TileEntity.ByPosition.TryGetValue(tilePosition.ToVector2().ToPoint16(), out var te))
				return false;
			if (!(te is ProjectorTileEntity entity))
				return false;
            return entity.TurnedOn;
            */
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
