using ImmersiveProjector.DataStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.Json;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;
using Point = Microsoft.Xna.Framework.Point;

namespace ImmersiveProjector.Tiles
{
    public class ProjectorTileEntity : ModTileEntity
    {
        protected bool statusChanged = false;

        public ProjectorInstance projectorInstance = null;
        public ProjectorData data => projectorInstance != null ? projectorInstance.data : null;
        public bool TurnedOn => data == null ? Main.tile[Position.ToPoint()].TileFrameY >= 18 : data.turnedOn > 0;

        public void ResetProjector()
        {
        }

        public void MarkNetUpdate()
        {
            statusChanged = true;
        }

        public override void Update()
        {
            if (statusChanged && projectorInstance != null)
            {
                NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, ID, Position.X, Position.Y);
                statusChanged = false;
            }
        }

        public string GetSerializedData()
        {
            return data != null ? data.ToJson() : "ennia";
        }

        public bool BuildFromSerializedData(string doc)
        {
            ProjectorData result = null;
            try
            {
                var options = new JsonSerializerOptions();
                options.IncludeFields = true;
                options.IgnoreReadOnlyFields = true;
                options.IgnoreReadOnlyProperties = true;
                result = JsonSerializer.Deserialize<ProjectorData>(doc, options);
            }
            catch (Exception ex)
            {
                // Deserialize failed
            }
            if (result != null)
            {
                if (projectorInstance == null)
                    projectorInstance = ProjectorInstance.CreateInstance(Position.ToPoint(), ProjectorSystem.ListInWorld);
                projectorInstance.data = result;
                return true;
            }
            return false;
        }

        public override void NetReceive(BinaryReader reader)
        {
            var doc = reader.ReadString();
            if (Main.netMode != NetmodeID.Server && ImmersiveProjector.DEBUG_MODE)
            {
                Main.NewText("Rece");
                Main.NewText(doc);
            }
            BuildFromSerializedData(doc);
        }

        public override void NetSend(BinaryWriter writer)
        {
            var doc = GetSerializedData();
            if (Main.netMode != NetmodeID.Server && ImmersiveProjector.DEBUG_MODE)
            {
                Main.NewText("Send");
                Main.NewText(doc);
            }
            writer.Write(doc);
        }

        public override void SaveData(TagCompound tag)
        {
            tag.Set("data", GetSerializedData(), true);
        }

        public override void LoadData(TagCompound tag)
        {
            var doc = tag.Get<string>("data");
            BuildFromSerializedData(doc);
        }

        public override void OnNetPlace()
        {
            Main.tile[Position.X, Position.Y].TileFrameY = 18;
            projectorInstance = ProjectorInstance.CreateInstance(Position.ToPoint(), ProjectorSystem.ListInWorld);
            projectorInstance.data.turnedOn = 1;
            MarkNetUpdate();
        }

        public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
        {
            // Main.NewText("i " + i + " j " + j + " t " + type + " s " + style + " d " + direction);
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                NetMessage.SendTileSquare(Main.myPlayer, i, j, 3);
                NetMessage.SendData(MessageID.TileEntityPlacement, -1, -1, null, i, j, Type, 0f, 0, 0, 0);
                Main.tile[i, j].TileFrameY = 18;
                return -1;
            }
            int id = Place(i, j);
            Main.tile[i, j].TileFrameY = 18;
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                ((ProjectorTileEntity)ByID[id]).projectorInstance = ProjectorInstance.CreateInstance(new Point(i, j), ProjectorSystem.ListInWorld);
            }
            return id;
        }

        public override void OnKill()
        {
            if (projectorInstance != null)
                ProjectorSystem.ListInWorld.Remove(projectorInstance);
            base.OnKill();
        }

        public override bool IsTileValidForEntity(int i, int j)
        {
            Tile tile = Main.tile[i, j];
            bool flag = tile.TileType == ModContent.TileType<MiniProjectorTile>();
            return flag;
        }
    }
}
