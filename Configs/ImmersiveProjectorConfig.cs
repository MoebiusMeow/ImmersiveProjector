using Microsoft.Xna.Framework;
using System.ComponentModel;
using System.Runtime.Serialization;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Creative;
using Terraria.Graphics.Renderers;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace ImmersiveProjector.Configs
{
    public class ImmersiveProjectorConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Header("$Mods.ImmersiveProjector.Configs.Debug")]

        public bool EnableDebug;

        internal void OnDeserializedMethod(StreamingContext context) {
        }
    }
}