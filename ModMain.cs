using ImmersiveProjector.Configs;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace ImmersiveProjector
{
    public class ImmersiveProjector : Mod
    {

        static public bool DEBUG_MODE => ModContent.GetInstance<ImmersiveProjectorConfig>().EnableDebug;
        static public ImmersiveProjector Instance {  get { return ModContent.GetInstance<ImmersiveProjector>(); } }

        static public string ModTranslate(string raw, string prefix = "") { return Language.GetTextValue("Mods." + Instance.Name + "." + prefix + raw); }
        static public LocalizedText ModTranslateL(string raw, string prefix = "") { return Language.GetText("Mods." + Instance.Name + "." + prefix + raw); }

        public override void Load()
        {
            base.Load();
        }
    }
}