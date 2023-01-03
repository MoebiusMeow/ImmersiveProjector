using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader.UI.Elements;
using Terraria.UI;

namespace ImmersiveProjector.UI
{
	public class PresetGrid : UIGrid
	{
		public int selectedType = -1;
		public string selectedName = "";

		public void handleClick(PresetFrame target)
		{
			selectedType = target.presetTypeKey;
			selectedName = target.presetName;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			foreach (var i in _items)
				if (i is PresetFrame frame)
				{
					frame.selected = (frame.presetTypeKey == selectedType && frame.presetName == selectedName);
				}
		}
	}
}
