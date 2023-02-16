using ImmersiveProjector.DataStructure;
using ImmersiveProjector.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI.States;
using Terraria.GameInput;
using Terraria.Graphics.Renderers;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.UI;
using Terraria.ModLoader.UI.Elements;
using Terraria.UI;

namespace ImmersiveProjector.UI
{

    public class ProjectorUI : UIState
    {
        readonly static public Color DefaultBackground = new Color(63, 82, 151) * 0.7f;
        readonly static public Color GreenBackground = new Color(45, 171, 61) * 0.7f;
        readonly static public Color RedBackground = new Color(144, 38, 38) * 0.7f;

        public bool visible = true;
        public bool dragging = false;
        public DragablePanel panel;
        public UserInterface userInterface { get => ModContent.GetInstance<UISystem>().userInterface; }

        public ProjectorUI()
        {
        }

        public override void OnInitialize()
        {
            base.OnInitialize();
        }

        public override void OnActivate()
        {
            base.OnActivate();
            if (Main.netMode != NetmodeID.Server)
            {
                InitializeAll();
            }
        }

        DragablePanel container;
        string[] tabNames = new string[] { "UI.1", "2", "3", "4" };
        int[] tabHeight;
        UIElement[] tabPages;
        UIImageFramed[] tabButtons;
        int[] tabButtonIconFrame;

        int currentPage;
        int tweenToPage;
        float tweenProgress;
        float tweenDuration => 0.3f;
        float tweenValue { get => 1f - (float)Math.Pow(1f - Utils.Clamp<float>(tweenProgress, 0, 1), 3); }
        public Func<float, float> tweenValueInverse = (float v) => 1f - (float)Math.Pow(Utils.Clamp<float>(1f - v, 0, 1), 1 / 3.0);

        private Dictionary<UIImageFramed, UIElement> iconsByButtons = new Dictionary<UIImageFramed, UIElement>();
        private Dictionary<UIImageFramed, int> indexByButtons = new Dictionary<UIImageFramed, int>();

        public HandleGridGroup sourceGrid, targetGrid;
        public ProjectorInstance focusedInstance = null;
        public ProjectorData tempData = null;
        public List<(string, ProjectorData)> presetData = null;

        public bool configFlipH;
        public float testF;

        public Asset<Texture2D> panelBackground;
        public Asset<Texture2D> panelBorder;
        public Asset<Texture2D> toggleItemTexture;
        public MapHintPanel mapHintPanel;

        public int snapping = 16;
        public List<TogglePanel> togglePanels;
        public List<SliderPanel> sliderPanels;

        public void FocusInstance(ProjectorInstance instance)
        {
            if (Main.netMode == NetmodeID.Server)
                return;
            if (focusedInstance != instance)
            {
                focusedInstance = instance;
                tempData = new ProjectorData();
                tempData.CopyDataFrom(instance.data);
            }
            if (focusedInstance != null && focusedInstance.data != null)
                RebindData();
            sourceGrid.SetSnapping(snapping);
            targetGrid.SetSnapping(snapping);
            sourceGrid.SetFromAnchorAndSize(instance.data.sourcePoint, instance.data.anchor, instance.data.sourceSize);
            targetGrid.SetFromAnchorAndSize(instance.data.targetPoint, instance.data.anchor, instance.data.targetSize);
            if (mapHintPanel != null)
                mapHintPanel.projector = instance;
        }

        public bool ValidateFocusedProjector()
        {
            if (focusedInstance == null || focusedInstance.data == null) return false;
            if (!TileEntity.ByPosition.TryGetValue(focusedInstance.tilePosition.ToVector2().ToPoint16(), out var te))
                return false;
            if (!(te is ProjectorTileEntity))
                return false;
            return true;
        }

        public void RebindData()
        {
            foreach (var p in togglePanels)
                p.bindedObject = focusedInstance.data;
            foreach (var p in sliderPanels)
                p.bindedObject = focusedInstance.data;
        }

        public bool FocusedDataChanged()
        {
            if (focusedInstance == null) return false;
            if (tempData == null) return false;
            return !focusedInstance.data.DataEqualsTo(tempData);
        }

        public void RevertFocusedDataChange()
        {
            if (focusedInstance == null || tempData == null) return;
            focusedInstance.data.CopyDataFrom(tempData);
            sourceGrid.SetFromAnchorAndSize(tempData.sourcePoint, tempData.anchor, tempData.sourceSize);
            targetGrid.SetFromAnchorAndSize(tempData.targetPoint, tempData.anchor, tempData.targetSize);
        }

        public void CommitFocusedDataChange()
        {
            if (focusedInstance == null || tempData == null) return;
            tempData.CopyDataFrom(focusedInstance.data);
            TileEntity.ByPosition.TryGetValue(focusedInstance.tilePosition.ToVector2().ToPoint16(), out var te);
            if (ImmersiveProjector.DEBUG_MODE)
                Main.NewText("Commit");
            if (te is ProjectorTileEntity entity)
            {
                entity.MarkNetUpdate();
                if (ImmersiveProjector.DEBUG_MODE)
                    Main.NewText("Mark");
            }
        }

        public void AnimatedClose()
        {
            DoTweenTo(-1);
        }

        public Vector2 HintPoint()
        {
            if (container == null) return Vector2.Zero;
            Vector2 zoomOffset = new Vector2(Main.Transform.Translation.X, Main.Transform.Translation.Y);
            return (new Vector2(container.Left.Pixels + container.Width.Pixels * 0.5f, container.Top.Pixels) * Main.UIScale - zoomOffset) / Main.GameZoomTarget;
        }

        public float GetTweenResult()
        {
            return 2 * MathF.Abs(tweenValue - 0.5f);
        }

        public void initializePresetData()
        {
            presetData ??= new List<(string, ProjectorData)>();
            presetData.Clear();
        }

        public void InitializePage(UIElement page, int index)
        {
            UIText headerText = new UIText(ImmersiveProjector.ModTranslate("Description" + index.ToString(), "Config."), 0.5f, true);
            headerText.Top.Set(-(tabHeight[index] - 20), 1);
            page.Append(headerText);

            UIHorizontalSeparator headerBar = new UIHorizontalSeparator();
            headerBar.Width.Set(0, 1);
            headerBar.Height.Set(10, 0);
            headerBar.Top.Set(-(tabHeight[index] - 50), 1);
            headerBar.Color = Color.White * 0.5f;
            page.Append(headerBar);
            // container.Append(uiTextPanel);
            switch (index)
            {
                case 0: InitializePresetPage(page, index); break;
                case 1: InitializeSelectionPage(page, index); break;
                case 2: InitializeSettingPage(page, index); break;
                case 3: InitializeBehaviorPage(page, index); break;
                case 4: InitializeColorPage(page, index); break;
            }

            string[] editButtonName = new string[]{"Cancel", "Hide", "Confirm"};
            for (var i = 0; i < 3; i++)
            {
                UITextPanel<string> button = new UITextPanel<string>(ImmersiveProjector.ModTranslate(editButtonName[i], "Config."), 0.4f, large: true);
                button.Width.Set(112f, 0);
                button.Height.Set(1f, 0);
                button.VAlign = 1f;
                button.HAlign = 0.5f;
                button.Left.Set((i - 1) * 130, 0f);
                // uiTextPanel.OnMouseOver += FadedMouseOver;
                // uiTextPanel.OnMouseOut += FadedMouseOut;
                switch(i)
                {
                    case 1:
                        button.OnClick += (UIMouseEvent evt, UIElement listening) => { DoTweenTo(-1); };
                        button.OnMouseOver += (UIMouseEvent evt, UIElement listening) =>
                        {
                            button.BorderColor = Color.Gold;
                            button.BackgroundColor = DefaultBackground * (1 / 0.7f);
                        };
                        button.OnMouseOut += (UIMouseEvent evt, UIElement listening) =>
                        {
                            button.BorderColor = Color.Black;
                            button.BackgroundColor = DefaultBackground;
                        };
                        break;
                    case 0:
                        button.OnUpdate += CancelButtonUpdate;
                        button.OnClick += (UIMouseEvent evt, UIElement listening) => { RevertFocusedDataChange(); };
                        break;
                    case 2:
                        button.OnUpdate += ConfirmButtonUpdate;
                        button.OnClick += (UIMouseEvent evt, UIElement listening) => { CommitFocusedDataChange(); };
                        break;
                }
                page.Append(button);
            }
        }

        private void InitializePresetPage(UIElement page, int index)
        {
            UIDynamicItemCollection items = new UIDynamicItemCollection();
            PresetGrid uiList = new PresetGrid();
            uiList.Width.Set(-30f, 1f);
            uiList.Height.Set(320, 0);
            uiList.Top.Set(-370, 1f);
            uiList.Left.Set(0, 0);
            uiList.ListPadding = 14f;
            uiList.PaddingTop = 10f;
            UIScrollbar uiScrollbar = new UIScrollbar();
            uiScrollbar.SetView(100f, 2000f);
            uiScrollbar.Height.Set(310, 0f);
            uiScrollbar.HAlign = 1f;
            uiScrollbar.Top.Set(-360, 1f);
            uiList.Add(items);
            uiList.SetScrollbar(uiScrollbar);
            // items.SetContentsToShow(new int[] { 2, 233, 2333, 1, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 }.ToList<int>());
            page.Append(uiList);
            Asset<Texture2D> assetSlotBack = Main.Assets.Request<Texture2D>("Images/UI/Bestiary/Slot_Back");
            Asset<Texture2D> assetSlotFront = Main.Assets.Request<Texture2D>("Images/UI/Bestiary/Slot_Front");
            Asset<Texture2D> assetSlotHover = Main.Assets.Request<Texture2D>("Images/UI/Bestiary/Slot_Selection");
            Asset<Texture2D> asset= Main.Assets.Request<Texture2D>("Images/UI/Bestiary/Icon_Locked");

            UIElement btn = new PresetFrame(assetSlotFront, assetSlotHover, assetSlotBack, asset, 1, "Coming Soon");
            uiList.Add(btn);
            /*
            string[] testAssets = new string[]{
                    "Camera_0",
                    "Camera_1",
                    "Camera_2",
                    "Camera_3",
                    "Camera_4",
                    "Camera_5",
                    "Camera_6",
                    "Creative/Infinite_Icons",
                    // "Bestiary/Slot_Back",
                    // "Bestiary/Slot_Front",
                    // "Bestiary/Slot_Overlay",
                    "Bestiary/Icon_Locked",
                    "Bestiary/Icon_Rank_Dim",
                    "Bestiary/Icon_Rank_Light",
                    "Bestiary/Icon_Tags_Shadow",
                    "Camera_7",
                    // "Bestiary/Slot_Selection",
                };
            for (var i = 0; i < testAssets.Length; i++)
            {
                Asset<Texture2D> asset2 = Main.Assets.Request<Texture2D>("Images/UI/" + testAssets[i]);
                PresetFrame btn2 = new PresetFrame(assetSlotFront, assetSlotHover, assetSlotBack, asset2, -1, i.ToString() + "Testing Testing")
                {
                    // Height = new StyleDimension(28f, 0f),
                    Top = new StyleDimension(1000 + 40 * i, 0f),
                    VAlign = 0f,
                    HAlign = 0f
                };
                // btn.Color = Color.Aqua;
                uiList.Add(btn2);
            }
            UIHorizontalSeparator minorSep = new UIHorizontalSeparator();
            minorSep.Width.Set(0, 1f);
            minorSep.Color = new Color(0.1f, 0.2f, 0.4f) * 0.4f;
            uiList.Add(minorSep);
            for (var i = 0; i < testAssets.Length; i++)
            {
                Asset<Texture2D> asset2 = Main.Assets.Request<Texture2D>("Images/UI/" + testAssets[testAssets.Length - 1]);
                UIElement btn2 = new PresetFrame(assetSlotFront, assetSlotHover, assetSlotBack, asset2, 1, "This is long uwu")
                {
                    // Height = new StyleDimension(28f, 0f),
                    Top = new StyleDimension(2000 + 40 * i, 0f),
                    VAlign = 0f,
                    HAlign = 0f
                };
                uiList.Add(btn2);
            }
            */
            uiList.RecalculateChildren();
            page.Append(uiScrollbar);
        }

        private void InitializeSelectionPage(UIElement page, int index)
        {
            Asset<Texture2D> assetGridB = ImmersiveProjector.Instance.Assets.Request<Texture2D>("Textures/Handle9Grid");
            Asset<Texture2D> assetGridY = ImmersiveProjector.Instance.Assets.Request<Texture2D>("Textures/Handle9Grid2");
            Func<bool> activeFunc = () => (page.Parent != null);
            sourceGrid = new HandleGridGroup(assetGridB, Vector2.Zero, Vector2.Zero, Vector2.One * 200, activeFunc);
            targetGrid = new HandleGridGroup(assetGridY, Vector2.Zero, Vector2.Zero, Vector2.One * 200, activeFunc);
            sourceGrid.AddChildrenTo(this);
            targetGrid.AddChildrenTo(this);
            sourceGrid.transformType = HandleGridGroup.TransformType.FreeTransform;
            targetGrid.transformType = HandleGridGroup.TransformType.ScaleAndMove;

            UIText text;
            ElementEvent updateMouseOver = (UIElement o) => { ((UIPanel)o).BackgroundColor = DefaultBackground * (o.IsMouseHovering ? 1.5f : 1);  };
            ElementEvent updateMouseOverResetScale = (UIElement o) =>
            {
                ((UIPanel)o).BackgroundColor = DefaultBackground * (o.IsMouseHovering ? 1.5f : 1);
                ((UIText)o.Children.First()).SetText(ImmersiveProjector.ModTranslate("DoubleClickResetScale", "Config.") + " ("
                    + (focusedInstance == null || focusedInstance.data == null ? "?" : (focusedInstance.data.targetScale * 100).ToString("0.00")) + "%)");
            };
            MouseEvent resetPositionAction = (UIMouseEvent evt, UIElement o) =>
            {
                if (focusedInstance != null && focusedInstance.data != null)
                {
                    var s = focusedInstance.data;
                    s.SetDefaultPosition(Main.LocalPlayer.Center);
                    sourceGrid.SetFromAnchorAndSize(s.sourcePoint, s.anchor, s.sourceSize);
                    targetGrid.SetFromAnchorAndSize(s.targetPoint, s.anchor, s.targetSize);
                }
            };

            MouseEvent resetScaleAction = (UIMouseEvent evt, UIElement o) =>
            {
                if (focusedInstance != null && focusedInstance.data != null)
                {
                    var s = focusedInstance.data;
                    s.targetScale = 1f;
                    sourceGrid.SetFromAnchorAndSize(s.sourcePoint, s.anchor, s.sourceSize);
                    targetGrid.SetFromAnchorAndSize(s.targetPoint, s.anchor, s.targetSize);
                }
            };

            mapHintPanel = new MapHintPanel(panelBorder);
            mapHintPanel.BorderColor = Color.Gray;
            mapHintPanel.Width.Set(0, 1);
            mapHintPanel.Top.Set(-(tabHeight[index] - 190), 1);
            mapHintPanel.Height.Set(140, 0);
            mapHintPanel.IgnoresMouseInteraction = true;
            page.Append(mapHintPanel);

            UIPanel resetPanel = new UIPanel(panelBackground, null);
            resetPanel.Width.Set(0, 1);
            resetPanel.Top.Set(-(tabHeight[index] - 375), 1);
            resetPanel.Height.Set(40, 0);
            text = new UIText(ImmersiveProjector.ModTranslate("DoubleClickResetPosition", "Config."));
            text.HAlign = 0.5f;
            resetPanel.Append(text);
            page.Append(resetPanel);

            resetPanel.OnUpdate += updateMouseOver;
            resetPanel.OnDoubleClick += resetPositionAction;

            UIPanel resetScalePanel = new UIPanel(panelBackground, null);
            resetScalePanel.Width.Set(0, 1);
            resetScalePanel.Top.Set(-(tabHeight[index] - 335), 1);
            resetScalePanel.Height.Set(40, 0);
            text = new UIText(ImmersiveProjector.ModTranslate("DoubleClickResetScale", "Config."));
            text.HAlign = 0.5f;
            resetScalePanel.Append(text);
            page.Append(resetScalePanel);

            resetScalePanel.OnUpdate += updateMouseOverResetScale;
            resetScalePanel.OnClick += resetScaleAction;

            TogglePanel snapVPanel = new TogglePanel(3, toggleItemTexture, new Rectangle(0, 256, 32, 32), panelBackground);
            snapVPanel.Width.Set(0, 1);
            snapVPanel.Top.Set(-(tabHeight[index] - 100), 1);
            snapVPanel.Height.Set(40, 0);
            snapVPanel.configName = ImmersiveProjector.ModTranslate("Snapping", "Config.");
            snapVPanel.currentValue = (snapping == 16 ? 0 : snapping == 8 ? 1 : 2);
            snapVPanel.tooltips = new string[]
            {
                "1 " + ImmersiveProjector.ModTranslate("SnappingBlock", "Config."),
                "1/2 " + ImmersiveProjector.ModTranslate("SnappingBlock", "Config."),
                "1/4 " + ImmersiveProjector.ModTranslate("SnappingBlock", "Config.")
            };
            snapVPanel.onValueChanged += (int value) =>
            {
                snapping = (value == 0 ? 16 : value == 1 ? 8 : 4);
                sourceGrid.SetSnapping(snapping);
                targetGrid.SetSnapping(snapping);
            };
            page.Append(snapVPanel);

            TogglePanel flipVPanel = new TogglePanel(3, toggleItemTexture, new Rectangle(0, 224, 32, 32), panelBackground);
            flipVPanel.Width.Set(0, 1);
            flipVPanel.Top.Set(-(tabHeight[index] - 140), 1);
            flipVPanel.Height.Set(40, 0);
            flipVPanel.configName = ImmersiveProjector.ModTranslate("Flip", "Config.");
            // flipVPanel.bindedObject = tempData;
            flipVPanel.bindedValue = typeof(ProjectorData).GetProperty("targetFlip");
            togglePanels.Add(flipVPanel);

            flipVPanel.tooltips = new string[]
            {
                ImmersiveProjector.ModTranslate("FlipN", "Config."),
                ImmersiveProjector.ModTranslate("FlipH", "Config."),
                ImmersiveProjector.ModTranslate("FlipV", "Config.")
            };
            page.Append(flipVPanel);

            SliderPanel rotationVPanel = new SliderPanel(0, 360, Main.OurFavoriteColor, panelBackground, null);
            rotationVPanel.valueSnapping = 5;
            rotationVPanel.configName = ImmersiveProjector.ModTranslate("TargetRotation", "Config.");
            rotationVPanel.tooltip = ImmersiveProjector.ModTranslate("TargetRotation", "Config.");
            rotationVPanel.bindedValue = typeof(ProjectorData).GetProperty("targetRotation");
            sliderPanels.Add(rotationVPanel);
            /*
            UIPanel rotationEntry = new UIPanel(panelBackground, null);
            rotationEntry.OnUpdate += updateMouseOver;

            // UIColoredSliderSimple slider = new UIColoredSliderSimple();
            UIColoredSlider slider = new UIColoredSlider(LocalizedText.Empty, () => testF, (float v) => { testF = v; }, () => { }, (float v) => Main.OurFavoriteColor * v, Color.White);
            slider.Width.Set(0, 1);
            slider.Height.Set(0, 1);
            slider.HAlign = 1;
            slider.Top.Set(-10, 0);
            slider.Left.Set(10, 0);
            rotationEntry.Append(slider);

            */
            rotationVPanel.Width.Set(0, 1);
            rotationVPanel.Top.Set(-(tabHeight[index] - 60), 1);
            rotationVPanel.Height.Set(40, 0);

            /*
            text = new UIText(ImmersiveProjector.ModTranslate("TargetRotation", "Config."));
            text.IgnoresMouseInteraction = true;
            rotationEntry.Append(text);
            */

            page.Append(rotationVPanel);
        }

        private void InitializeSettingPage(UIElement page, int index)
        {
            TogglePanel captureSolidVPanel = new TogglePanel(3, toggleItemTexture, new Rectangle(0, 32 * 9, 32, 32), panelBackground);
            captureSolidVPanel.Width.Set(0, 1);
            captureSolidVPanel.Top.Set(-(tabHeight[index] - 60), 1);
            captureSolidVPanel.Height.Set(40, 0);
            captureSolidVPanel.configName = ImmersiveProjector.ModTranslate("CaptureTiles", "Config.");
            captureSolidVPanel.bindedValue = typeof(ProjectorData).GetProperty("captureSolid");
            togglePanels.Add(captureSolidVPanel);

            captureSolidVPanel.tooltips = new string[]
            {
                ImmersiveProjector.ModTranslate("CaptureTilesN", "Config."),
                ImmersiveProjector.ModTranslate("CaptureTilesS", "Config."),
                ImmersiveProjector.ModTranslate("CaptureTilesA", "Config.")
            };
            page.Append(captureSolidVPanel);


            TogglePanel captureWallVPanel = new TogglePanel(2, toggleItemTexture, new Rectangle(0, 32 * 10, 32, 32), panelBackground);
            captureWallVPanel.Width.Set(0, 1);
            captureWallVPanel.Top.Set(-(tabHeight[index] - 100), 1);
            captureWallVPanel.Height.Set(40, 0);
            captureWallVPanel.configName = ImmersiveProjector.ModTranslate("CaptureWalls", "Config.");
            captureWallVPanel.bindedValue = typeof(ProjectorData).GetProperty("captureWall");
            togglePanels.Add(captureWallVPanel);

            captureWallVPanel.tooltips = new string[]
            {
                ImmersiveProjector.ModTranslate("CaptureWallsN", "Config."),
                ImmersiveProjector.ModTranslate("CaptureWallsT", "Config."),
            };
            page.Append(captureWallVPanel);


            TogglePanel captureCreatureVPanel = new TogglePanel(2, toggleItemTexture, new Rectangle(0, 32 * 11, 32, 32), panelBackground);
            captureCreatureVPanel.Width.Set(0, 1);
            captureCreatureVPanel.Top.Set(-(tabHeight[index] - 140), 1);
            captureCreatureVPanel.Height.Set(40, 0);
            captureCreatureVPanel.configName = ImmersiveProjector.ModTranslate("CaptureCreatures", "Config.");
            captureCreatureVPanel.bindedValue = typeof(ProjectorData).GetProperty("captureCreature");
            togglePanels.Add(captureCreatureVPanel);

            captureCreatureVPanel.tooltips = new string[]
            {
                ImmersiveProjector.ModTranslate("CaptureCreaturesN", "Config."),
                ImmersiveProjector.ModTranslate("CaptureCreaturesA", "Config."),
            };
            page.Append(captureCreatureVPanel);

            UIHorizontalSeparator sep;
            sep = new UIHorizontalSeparator();
            sep.Width.Set(0, 1);
            sep.Top.Set(-(tabHeight[index] - 190), 1);
            sep.Color = Color.White * 0.1f;
            page.Append(sep);

            TogglePanel captureLayerVPanel = new TogglePanel(4, toggleItemTexture, new Rectangle(0, 32 * 4, 32, 32), panelBackground);
            captureLayerVPanel.Width.Set(0, 1);
            captureLayerVPanel.Top.Set(-(tabHeight[index] - 200), 1);
            captureLayerVPanel.Height.Set(40, 0);
            captureLayerVPanel.configName = ImmersiveProjector.ModTranslate("CaptureLayers", "Config.");
            captureLayerVPanel.bindedValue = typeof(ProjectorData).GetProperty("layer");
            togglePanels.Add(captureLayerVPanel);

            captureLayerVPanel.tooltips = new string[]
            {
                ImmersiveProjector.ModTranslate("CaptureLayersF", "Config."),
                ImmersiveProjector.ModTranslate("CaptureLayersN", "Config."),
                ImmersiveProjector.ModTranslate("CaptureLayersBF", "Config."),
                ImmersiveProjector.ModTranslate("CaptureLayersBW", "Config."),
            };
            page.Append(captureLayerVPanel);


            SliderPanel PriorityVPanel = new SliderPanel(0, 1, Color.Green, panelBackground, null);
            PriorityVPanel.valueSnapping = 0.01f;
            PriorityVPanel.configName = ImmersiveProjector.ModTranslate("CapturePriority", "Config.");
            PriorityVPanel.tooltip = ImmersiveProjector.ModTranslate("CapturePriorityToolTip", "Config.");
            PriorityVPanel.bindedValue = typeof(ProjectorData).GetProperty("priority");
            sliderPanels.Add(PriorityVPanel);
            PriorityVPanel.Width.Set(0, 1);
            PriorityVPanel.Top.Set(-(tabHeight[index] - 240), 1);
            PriorityVPanel.Height.Set(40, 0);
            page.Append(PriorityVPanel);

            sep = new UIHorizontalSeparator();
            sep.Width.Set(0, 1);
            sep.Top.Set(-(tabHeight[index] - 290), 1);
            sep.Color = Color.White * 0.1f;
            page.Append(sep);

            TogglePanel captureLightingSourceVPanel = new TogglePanel(3, toggleItemTexture, new Rectangle(0, 32 * 12, 32, 32), panelBackground);
            captureLightingSourceVPanel.Width.Set(0, 1);
            captureLightingSourceVPanel.Top.Set(-(tabHeight[index] - 300), 1);
            captureLightingSourceVPanel.Height.Set(40, 0);
            captureLightingSourceVPanel.configName = ImmersiveProjector.ModTranslate("CaptureLightingSources", "Config.");
            captureLightingSourceVPanel.bindedValue = typeof(ProjectorData).GetProperty("lightingSource");
            togglePanels.Add(captureLightingSourceVPanel);

            captureLightingSourceVPanel.tooltips = new string[]
            {
                ImmersiveProjector.ModTranslate("CaptureLightingSourcesS", "Config."),
                ImmersiveProjector.ModTranslate("CaptureLightingSourcesT", "Config."),
                ImmersiveProjector.ModTranslate("CaptureLightingSourcesB", "Config."),
            };
            page.Append(captureLightingSourceVPanel);

            SliderPanel FreqencyLPanel = new SliderPanel(0.05f, 1, Color.OrangeRed, panelBackground, null);
            FreqencyLPanel.valueSnapping = 0.01f;
            FreqencyLPanel.configName = ImmersiveProjector.ModTranslate("CaptureLightFreqency", "Config.");
            FreqencyLPanel.tooltip = ImmersiveProjector.ModTranslate("CaptureLightFreqencyToolTip", "Config.");
            FreqencyLPanel.bindedValue = typeof(ProjectorData).GetProperty("lightFreq");
            sliderPanels.Add(FreqencyLPanel);
            FreqencyLPanel.Width.Set(0, 1);
            FreqencyLPanel.Top.Set(-(tabHeight[index] - 340), 1);
            FreqencyLPanel.Height.Set(40, 0);
            page.Append(FreqencyLPanel);

            /*
            SliderPanel FreqencyVPanel = new SliderPanel(0.05f, 1, Color.OrangeRed, panelBackground, null);
            FreqencyVPanel.valueSnapping = 0.01f;
            FreqencyVPanel.configName = ImmersiveProjector.ModTranslate("CaptureFreqency", "Config.");
            FreqencyVPanel.tooltip = ImmersiveProjector.ModTranslate("CaptureFreqencyToolTip", "Config.");
            FreqencyVPanel.bindedValue = typeof(ProjectorData).GetProperty("updateFreq");
            sliderPanels.Add(FreqencyVPanel);
            FreqencyVPanel.Width.Set(0, 1);
            FreqencyVPanel.Top.Set(-(tabHeight[index] - 300), 1);
            FreqencyVPanel.Height.Set(40, 0);
            page.Append(FreqencyVPanel);
            */
        }

        private void InitializeBehaviorPage(UIElement page, int index)
        {
            TogglePanel captureBehaviorVPanel = new TogglePanel(4, toggleItemTexture, new Rectangle(0, 32 * 3, 32, 32), panelBackground);
            captureBehaviorVPanel.Width.Set(0, 1);
            captureBehaviorVPanel.Top.Set(-(tabHeight[index] - 60), 1);
            captureBehaviorVPanel.Height.Set(40, 0);
            captureBehaviorVPanel.configName = ImmersiveProjector.ModTranslate("CaptureBehavior", "Config.");
            captureBehaviorVPanel.bindedValue = typeof(ProjectorData).GetProperty("behavior");
            togglePanels.Add(captureBehaviorVPanel);

            captureBehaviorVPanel.tooltips = new string[]
            {
                ImmersiveProjector.ModTranslate("CaptureBehaviorN", "Config."),
                ImmersiveProjector.ModTranslate("CaptureBehaviorH", "Config."),
                ImmersiveProjector.ModTranslate("CaptureBehaviorF", "Config."),
                ImmersiveProjector.ModTranslate("CaptureBehaviorD", "Config.")
            };
            page.Append(captureBehaviorVPanel);

            UIHorizontalSeparator sep;
            sep = new UIHorizontalSeparator();
            sep.Width.Set(0, 1);
            sep.Top.Set(-(tabHeight[index] - 110), 1);
            sep.Color = Color.White * 0.1f;
            page.Append(sep);

            TogglePanel captureFollowSVPanel = new TogglePanel(4, toggleItemTexture, new Rectangle(0, 32 * 5, 32, 32), panelBackground);
            captureFollowSVPanel.Width.Set(0, 1);
            captureFollowSVPanel.Top.Set(-(tabHeight[index] - 120), 1);
            captureFollowSVPanel.Height.Set(40, 0);
            captureFollowSVPanel.configName = ImmersiveProjector.ModTranslate("CaptureFollowS", "Config.");
            captureFollowSVPanel.bindedValue = typeof(ProjectorData).GetProperty("sourceFollow");
            togglePanels.Add(captureFollowSVPanel);

            captureFollowSVPanel.tooltips = new string[]
            {
                ImmersiveProjector.ModTranslate("CaptureFollowN", "Config."),
                ImmersiveProjector.ModTranslate("CaptureFollowP", "Config."),
                ImmersiveProjector.ModTranslate("CaptureFollowTN", "Config."),
                ImmersiveProjector.ModTranslate("CaptureFollowB", "Config."),
            };

            SliderPanel SourceIdVPanel = new SliderPanel(0, 1, Color.Cyan, panelBackground, null);
            SourceIdVPanel.valueSnapping = 0.01f;
            SourceIdVPanel.tooltip = SourceIdVPanel.configName = ImmersiveProjector.ModTranslate("CaptureFollowSId", "Config.");
            SourceIdVPanel.bindedValue = typeof(ProjectorData).GetProperty("sourceFollowId");
            sliderPanels.Add(SourceIdVPanel);
            SourceIdVPanel.Width.Set(0, 1);
            SourceIdVPanel.Top.Set(-(tabHeight[index] - 160), 1);
            SourceIdVPanel.Height.Set(40, 0);
            page.Append(SourceIdVPanel);
            page.Append(captureFollowSVPanel);

            sep = new UIHorizontalSeparator();
            sep.Width.Set(0, 1);
            sep.Top.Set(-(tabHeight[index] - 210), 1);
            sep.Color = Color.White * 0.1f;
            page.Append(sep);

            TogglePanel captureFollowTVPanel = new TogglePanel(4, toggleItemTexture, new Rectangle(0, 32 * 5, 32, 32), panelBackground);
            captureFollowTVPanel.Width.Set(0, 1);
            captureFollowTVPanel.Top.Set(-(tabHeight[index] - 220), 1);
            captureFollowTVPanel.Height.Set(40, 0);
            captureFollowTVPanel.configName = ImmersiveProjector.ModTranslate("CaptureFollowT", "Config.");
            captureFollowTVPanel.bindedValue = typeof(ProjectorData).GetProperty("targetFollow");
            togglePanels.Add(captureFollowTVPanel);

            captureFollowTVPanel.tooltips = new string[]
            {
                ImmersiveProjector.ModTranslate("CaptureFollowN", "Config."),
                ImmersiveProjector.ModTranslate("CaptureFollowP", "Config."),
                ImmersiveProjector.ModTranslate("CaptureFollowTN", "Config."),
                ImmersiveProjector.ModTranslate("CaptureFollowB", "Config."),
            };

            SliderPanel TargetIdVPanel = new SliderPanel(0, 1, Color.Orange, panelBackground, null);
            TargetIdVPanel.valueSnapping = 0.01f;
            TargetIdVPanel.tooltip = TargetIdVPanel.configName = ImmersiveProjector.ModTranslate("CaptureFollowTId", "Config.");
            TargetIdVPanel.bindedValue = typeof(ProjectorData).GetProperty("targetFollowId");
            sliderPanels.Add(TargetIdVPanel);
            TargetIdVPanel.Width.Set(0, 1);
            TargetIdVPanel.Top.Set(-(tabHeight[index] - 260), 1);
            TargetIdVPanel.Height.Set(40, 0);
            page.Append(TargetIdVPanel);
            page.Append(captureFollowTVPanel);

            TogglePanel captureFollowFlipVPanel = new TogglePanel(2, toggleItemTexture, new Rectangle(0, 32 * 1, 32, 32), panelBackground);
            captureFollowFlipVPanel.Width.Set(0, 0.5f);
            captureFollowFlipVPanel.Top.Set(-(tabHeight[index] - 300), 1);
            captureFollowFlipVPanel.Height.Set(40, 0);
            captureFollowFlipVPanel.configName = ImmersiveProjector.ModTranslate("CaptureFollowFlip", "Config.");
            captureFollowFlipVPanel.bindedValue = typeof(ProjectorData).GetProperty("targetFollowFlip");
            togglePanels.Add(captureFollowFlipVPanel);

            captureFollowFlipVPanel.tooltips = new string[]
            {
                ImmersiveProjector.ModTranslate("No", "Config."),
                ImmersiveProjector.ModTranslate("Yes", "Config."),
            };
            page.Append(captureFollowFlipVPanel);

            TogglePanel captureFollowRotationVPanel = new TogglePanel(2, toggleItemTexture, new Rectangle(0, 32 * 1, 32, 32), panelBackground);
            captureFollowRotationVPanel.Width.Set(0, 0.5f);
            captureFollowRotationVPanel.Left.Set(0, 0.5f);
            captureFollowRotationVPanel.Top.Set(-(tabHeight[index] - 300), 1);
            captureFollowRotationVPanel.Height.Set(40, 0);
            captureFollowRotationVPanel.configName = ImmersiveProjector.ModTranslate("CaptureFollowRotation", "Config.");
            captureFollowRotationVPanel.bindedValue = typeof(ProjectorData).GetProperty("targetFollowRotation");
            togglePanels.Add(captureFollowRotationVPanel);

            captureFollowRotationVPanel.tooltips = new string[]
            {
                ImmersiveProjector.ModTranslate("No", "Config."),
                ImmersiveProjector.ModTranslate("Yes", "Config."),
            };
            page.Append(captureFollowRotationVPanel);

            sep = new UIHorizontalSeparator();
            sep.Width.Set(0, 1);
            sep.Top.Set(-(tabHeight[index] - 350), 1);
            sep.Color = Color.White * 0.1f;
            page.Append(sep);

            SliderPanel ParallaxVPanel = new SliderPanel(-1, 1, Color.Purple, panelBackground, null);
            ParallaxVPanel.valueSnapping = 0.05f;
            ParallaxVPanel.configName = ImmersiveProjector.ModTranslate("CaptureParallax", "Config.");
            ParallaxVPanel.tooltip = ImmersiveProjector.ModTranslate("CaptureParallaxToolTip", "Config.");
            ParallaxVPanel.bindedValue = typeof(ProjectorData).GetProperty("parallax");
            sliderPanels.Add(ParallaxVPanel);
            ParallaxVPanel.Width.Set(0, 1);
            ParallaxVPanel.Top.Set(-(tabHeight[index] - 360), 1);
            ParallaxVPanel.Height.Set(40, 0);
            page.Append(ParallaxVPanel);
        }

        private void InitializeColorPage(UIElement page, int index)
        {
            SliderPanel ColorHVPanel = new SliderPanel(-180, 180, Color.Gray, panelBackground, null);
            ColorHVPanel.valueSnapping = 5.0f;
            ColorHVPanel.tooltip = ColorHVPanel.configName = ImmersiveProjector.ModTranslate("ColorH", "Config.");
            ColorHVPanel.bindedValue = typeof(ProjectorData).GetProperty("colorH");
            sliderPanels.Add(ColorHVPanel);
            ColorHVPanel.Width.Set(0, 1);
            ColorHVPanel.Top.Set(-(tabHeight[index] - 60), 1);
            ColorHVPanel.Height.Set(40, 0);
            page.Append(ColorHVPanel);

            SliderPanel ColorSVPanel = new SliderPanel(-100, 100, Color.Gray, panelBackground, null);
            ColorSVPanel.valueSnapping = 2.0f;
            ColorSVPanel.tooltip = ColorSVPanel.configName = ImmersiveProjector.ModTranslate("ColorS", "Config.");
            ColorSVPanel.bindedValue = typeof(ProjectorData).GetProperty("colorS");
            sliderPanels.Add(ColorSVPanel);
            ColorSVPanel.Width.Set(0, 1);
            ColorSVPanel.Top.Set(-(tabHeight[index] - 100), 1);
            ColorSVPanel.Height.Set(40, 0);
            page.Append(ColorSVPanel);

            SliderPanel ColorVVPanel = new SliderPanel(-100, 100, Color.Gray, panelBackground, null);
            ColorVVPanel.valueSnapping = 2.0f;
            ColorVVPanel.tooltip = ColorVVPanel.configName = ImmersiveProjector.ModTranslate("ColorV", "Config.");
            ColorVVPanel.bindedValue = typeof(ProjectorData).GetProperty("colorV");
            sliderPanels.Add(ColorVVPanel);
            ColorVVPanel.Width.Set(0, 1);
            ColorVVPanel.Top.Set(-(tabHeight[index] - 140), 1);
            ColorVVPanel.Height.Set(40, 0);
            page.Append(ColorVVPanel);

            SliderPanel ColorAVPanel = new SliderPanel(0, 1, Color.Gray, panelBackground, null);
            ColorAVPanel.valueSnapping = 0.01f;
            ColorAVPanel.tooltip = ColorAVPanel.configName = ImmersiveProjector.ModTranslate("Opacity", "Config.");
            ColorAVPanel.bindedValue = typeof(ProjectorData).GetProperty("colorA");
            sliderPanels.Add(ColorAVPanel);
            ColorAVPanel.Width.Set(0, 1);
            ColorAVPanel.Top.Set(-(tabHeight[index] - 180), 1);
            ColorAVPanel.Height.Set(40, 0);
            page.Append(ColorAVPanel);

            TogglePanel captureFilterVPanel = new TogglePanel(4, toggleItemTexture, new Rectangle(0, 32 * 6, 32, 32), panelBackground);
            captureFilterVPanel.Width.Set(0, 1);
            captureFilterVPanel.Top.Set(-(tabHeight[index] - 220), 1);
            captureFilterVPanel.Height.Set(40, 0);
            captureFilterVPanel.configName = ImmersiveProjector.ModTranslate("CaptureFilter", "Config.");
            captureFilterVPanel.bindedValue = typeof(ProjectorData).GetProperty("filter");
            togglePanels.Add(captureFilterVPanel);

            captureFilterVPanel.tooltips = new string[]
            {
                ImmersiveProjector.ModTranslate("CaptureFilterN", "Config."),
                ImmersiveProjector.ModTranslate("CaptureFilterH", "Config."),
                ImmersiveProjector.ModTranslate("CaptureFilterO", "Config."),
                ImmersiveProjector.ModTranslate("CaptureFilterB", "Config."),
            };
            page.Append(captureFilterVPanel);

            TogglePanel captureBlendVPanel = new TogglePanel(2, toggleItemTexture, new Rectangle(0, 32 * 2, 32, 32), panelBackground);
            captureBlendVPanel.Width.Set(0, 1);
            captureBlendVPanel.Top.Set(-(tabHeight[index] - 260), 1);
            captureBlendVPanel.Height.Set(40, 0);
            captureBlendVPanel.configName = ImmersiveProjector.ModTranslate("CaptureBlends", "Config.");
            captureBlendVPanel.bindedValue = typeof(ProjectorData).GetProperty("blending");
            togglePanels.Add(captureBlendVPanel);

            captureBlendVPanel.tooltips = new string[]
            {
                ImmersiveProjector.ModTranslate("CaptureBlendsD", "Config."),
                ImmersiveProjector.ModTranslate("CaptureBlendsA", "Config."),
            };
            page.Append(captureBlendVPanel);

            SliderPanel ColorRVPanel = new SliderPanel(0, 1, Color.Red, panelBackground, null);
            ColorRVPanel.valueSnapping = 0.01f;
            ColorRVPanel.tooltip = ColorRVPanel.configName = ImmersiveProjector.ModTranslate("ColorR", "Config.");
            ColorRVPanel.bindedValue = typeof(ProjectorData).GetProperty("colorR");
            sliderPanels.Add(ColorRVPanel);
            ColorRVPanel.Width.Set(0, 1);
            ColorRVPanel.Top.Set(-(tabHeight[index] - 300), 1);
            ColorRVPanel.Height.Set(40, 0);
            page.Append(ColorRVPanel);

            SliderPanel ColorGVPanel = new SliderPanel(0, 1, Color.Green, panelBackground, null);
            ColorGVPanel.valueSnapping = 0.01f;
            ColorGVPanel.tooltip = ColorGVPanel.configName = ImmersiveProjector.ModTranslate("ColorG", "Config.");
            ColorGVPanel.bindedValue = typeof(ProjectorData).GetProperty("colorG");
            sliderPanels.Add(ColorGVPanel);
            ColorGVPanel.Width.Set(0, 1);
            ColorGVPanel.Top.Set(-(tabHeight[index] - 340), 1);
            ColorGVPanel.Height.Set(40, 0);
            page.Append(ColorGVPanel);

            SliderPanel ColorBVPanel = new SliderPanel(0, 1, Color.Blue, panelBackground, null);
            ColorBVPanel.valueSnapping = 0.01f;
            ColorBVPanel.tooltip = ColorBVPanel.configName = ImmersiveProjector.ModTranslate("ColorB", "Config.");
            ColorBVPanel.bindedValue = typeof(ProjectorData).GetProperty("colorB");
            sliderPanels.Add(ColorBVPanel);
            ColorBVPanel.Width.Set(0, 1);
            ColorBVPanel.Top.Set(-(tabHeight[index] - 380), 1);
            ColorBVPanel.Height.Set(40, 0);
            page.Append(ColorBVPanel);
        }

        public void InitializeAll()
        {
            RemoveAllChildren();

            currentPage = -1;
            tweenToPage = 1;
            tabNames = new string[] { "Prefabs", "SelectArea", "Settings", "Behavior", "Color" };
            tabHeight = new int[] {420, 470, 470, 470, 470};
            tabButtonIconFrame = new int[] {10, 4, 6, 7, 3};
            for (var i = 0; i < tabNames.Length; i++)
                tabNames[i] = ImmersiveProjector.ModTranslate(tabNames[i], "Config.");
            togglePanels = new();
            sliderPanels = new();

            Asset<Texture2D> assetTab = Main.Assets.Request<Texture2D>("Images/UI/Creative/Infinite_Tabs_B");
            Asset<Texture2D> assetIcon = Main.Assets.Request<Texture2D>("Images/UI/Creative/Infinite_Icons");
            panelBackground = Main.Assets.Request<Texture2D>("Images/UI/PanelBackground");
            panelBorder = ImmersiveProjector.Instance.Assets.Request<Texture2D>("Textures/Panel");
            toggleItemTexture = ImmersiveProjector.Instance.Assets.Request<Texture2D>("Textures/ConfigToggleItems");

            tabPages = new UIElement[tabNames.Length];
            for (var i = 0; i < tabPages.Length; i++)
            {
                tabPages[i] = new UIElement();
                InitializePage(tabPages[i], i);
            }

            DragablePanel uiPanel = new DragablePanel();
            uiPanel.Width.Set(400f, 0f);
            uiPanel.Height.Set(400f, 0f);
            uiPanel.Top.Set(0.5f * (Main.screenHeight - 400), 0);
            uiPanel.Left.Set(0.5f * (Main.screenWidth - 400) - 400, 0);
            uiPanel.BackgroundColor = new Color(33, 43, 79) * 0.8f;
            uiPanel.PaddingTop = 0;
            uiPanel.MarginTop = 0;
            uiPanel.OverflowHidden = true;
            Append(uiPanel);
            container = uiPanel;

            tabButtons = new UIImageFramed[tabNames.Length];
            for (var i = 0; i < tabButtons.Length; i++)
            {
                tabButtons[i] = new UIImageFramed(assetTab, assetTab.Frame(2, 4).OffsetSize(-2, -2));
                var o = tabButtons[i];
                o.Left.Set(40 * i, 0f);
                o.HAlign = o.VAlign = 0;
                UIElement icon = new UIImageFramed(assetIcon, assetIcon.Frame(11, 1, tabButtonIconFrame[i]));
                icon.IgnoresMouseInteraction = true;
                icon.Left.Set(6, 0);
                icon.Top.Set(6, 0);
                o.Append(icon);
                iconsByButtons[o] = icon;
                indexByButtons[o] = i;
                Append(o);
                o.OnUpdate += (UIElement o) => { TabButtonUpdate(o as UIImageFramed); };
                o.OnClick += OnTabClick;
                TabButtonUpdate(o);
            }

            /*
            for (var i = 0; i < testAssets.Length; i++)
            {
                Asset<Texture2D> asset = Main.Assets.Request<Texture2D>("Images/UI/" + testAssets[i]);
                UIImageButton btn = new UIImageButton(asset)
                {
                    Height = new StyleDimension(28f, 0f),
                    Top = new StyleDimension(40 * i, 0f),
                    VAlign = 0f,
                    HAlign = 1f
                };
                container.Append(btn);
            }
            */
            // Main.InGameUI.DrawDebugHitbox(Main.DebugDrawer);

            /*
            UIList uiList = new UIList();
            uiList.Width.Set(-25f, 1f);
            uiList.Height.Set(-50f, 1f);
            uiList.Top.Set(50f, 0f);
            uiList.HAlign = 0.5f;
            uiList.ListPadding = 14f;
            // uiPanel.Append(uiList);
            _list = uiList;
            UIScrollbar uiScrollbar = new UIScrollbar();
            uiScrollbar.SetView(100f, 1000f);
            uiScrollbar.Height.Set(-20f, 1f);
            uiScrollbar.HAlign = 1f;
            uiScrollbar.VAlign = 1f;
            uiScrollbar.Top = StyleDimension.FromPixels(-5f);
            uiList.SetScrollbar(uiScrollbar);
            _scrollBar = uiScrollbar;
            */
            UITextPanel<LocalizedText> uiTextPanel = new UITextPanel<LocalizedText>(Language.GetText("UI.Back"), 0.5f, large: true);
            uiTextPanel.Width.Set(-10f, 0.1f);
            uiTextPanel.Height.Set(5f, 0f);
            uiTextPanel.VAlign = 1f;
            uiTextPanel.HAlign = 0.5f;
            uiTextPanel.Top.Set(-45f, 0f);
            // uiTextPanel.OnMouseOver += FadedMouseOver;
            // uiTextPanel.OnMouseOut += FadedMouseOut;
            uiTextPanel.OnClick += (UIMouseEvent evt, UIElement listening) => { userInterface.SetState(null); };
            //uiTextPanel.SetSnapPoint("Back", 0);
            // container.Append(uiTextPanel);
            /*
            int currentGroupIndex = 0;
            TryAddingList(Language.GetText("UI.EmoteCategoryGeneral"), ref currentGroupIndex, 10, GetEmotesGeneral());
            TryAddingList(Language.GetText("UI.EmoteCategoryRPS"), ref currentGroupIndex, 10, GetEmotesRPS());
            TryAddingList(Language.GetText("UI.EmoteCategoryItems"), ref currentGroupIndex, 11, GetEmotesItems());
            TryAddingList(Language.GetText("UI.EmoteCategoryBiomesAndEvents"), ref currentGroupIndex, 8, GetEmotesBiomesAndEvents());
            TryAddingList(Language.GetText("UI.EmoteCategoryTownNPCs"), ref currentGroupIndex, 9, GetEmotesTownNPCs());
            TryAddingList(Language.GetText("UI.EmoteCategoryCritters"), ref currentGroupIndex, 7, GetEmotesCritters());
            TryAddingList(Language.GetText("UI.EmoteCategoryBosses"), ref currentGroupIndex, 8, GetEmotesBosses());
            */
            // container.Append(tabPages[0]);
            tweenProgress = tweenValueInverse(0.8f);
            // Update(Main.gameTimeCache);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            // _container.Left.Set(100, 0);
            // for (var i = 0; i < tabButtons.Length; i++) UpdateVisuals(tabButtons[i]);
            if (ModContent.GetInstance<UISystem>() != null && ModContent.GetInstance<UISystem>().MouseBlocked())
                PlayerInput.LockVanillaMouseScroll("nya");

            if (!ValidateFocusedProjector())
                DoTweenTo(-1);

            tweenProgress += (float)gameTime.ElapsedGameTime.TotalSeconds / tweenDuration;
            // container.Height.Set(200 * (1 + 0.1f * (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 10)), 0);
            if (currentPage != -1)
                container.Height.Set(20 + tabHeight[currentPage] * GetTweenResult(), 0);

            if (tweenValue >= 0.5f && currentPage != tweenToPage)
            {
                if (currentPage >= 0) container.RemoveChild(tabPages[currentPage]);
                currentPage = tweenToPage;
                if (currentPage >= 0) container.Append(tabPages[currentPage]);
                else
                {
                    ModContent.GetInstance<UISystem>().userInterface.SetState(null);
                    return;
                }
            }
            if (currentPage != -1)
            {
                tabPages[currentPage].Width.Set(0, 1);
                tabPages[currentPage].Height.Set(0, 1);
            }
            else if (tweenToPage == -1)
            {
                ModContent.GetInstance<UISystem>().userInterface.SetState(null);
                return;
            }

            // sourceGrid.SetFromAnchorAndSize(Main.LocalPlayer.Center, Vector2.One * 0.5f, Vector2.One * 200);
            if (currentPage == 1)
            {
                if (focusedInstance != null)
                {
                    // 	FocusInstance(focusedInstance);
                    focusedInstance.data.anchor = Vector2.One * 0.5f;
                    focusedInstance.data.sourcePoint = sourceGrid.anchorPosition;
                    focusedInstance.data.sourceSize = sourceGrid.Size;
                    focusedInstance.data.targetPoint = targetGrid.anchorPosition;
                    /*
                    focusedInstance.data.targetScale = sourceGrid.draggingExpanding ?
                        MathF.Max(targetGrid.Size.X / sourceGrid.Size.X, targetGrid.Size.Y / sourceGrid.Size.Y):
                        MathF.Min(targetGrid.Size.X / sourceGrid.Size.X, targetGrid.Size.Y / sourceGrid.Size.Y);
                    */
                    if (targetGrid.buttons[targetGrid.draggingStyleX, targetGrid.draggingStyleY].dragging)
                        focusedInstance.data.targetScale = MathF.Max(0.25f, MathF.Min(targetGrid.Size.X / sourceGrid.Size.X, targetGrid.Size.Y / sourceGrid.Size.Y));
                    targetGrid.SetFromAnchorAndSize(focusedInstance.data.targetPoint, focusedInstance.data.anchor, focusedInstance.data.targetSize);
                }
                sourceGrid.UpdateHandlePositions();
                targetGrid.UpdateHandlePositions();
            }

            Recalculate();
        }

        public void DoTweenTo(int targetTab)
        {
            if (tweenToPage == targetTab) return;
            tweenToPage = targetTab;
            if (tweenValue >= 0.5f)
                tweenProgress = tweenValueInverse(1f - tweenValue);
        }






        private void OnTabClick(UIMouseEvent evt, UIElement listeningElement)
        {
            int index = indexByButtons[listeningElement as UIImageFramed];
            DoTweenTo(index);
        }

        private void MouseOverSound(UIMouseEvent evt, UIElement listeningElement)
        {
            SoundEngine.PlaySound(SoundID.MenuTick with { });
        }

        private void ConfirmButtonUpdate(UIElement o)
        {
            UITextPanel<string> button = (UITextPanel<string>)o;
            bool changed = FocusedDataChanged();
            if (changed)
            {
                button.IgnoresMouseInteraction = false;
                button.TextColor = Color.White;
                button.BorderColor = button.IsMouseHovering ? Color.Gold : Color.Black;
                button.BackgroundColor = GreenBackground * (button.IsMouseHovering ? 1 / 0.7f : 1);
            }
            else
            {
                button.IgnoresMouseInteraction = true;
                button.TextColor = Color.White * 0.3f;
                button.BorderColor = Color.Black;
                button.BackgroundColor = GreenBackground  * 0.3f;
            }
        }

        private void CancelButtonUpdate(UIElement o)
        {
            UITextPanel<string> button = (UITextPanel<string>)o;
            bool changed = FocusedDataChanged();
            if (changed)
            {
                button.IgnoresMouseInteraction = false;
                button.TextColor = Color.White;
                button.BorderColor = button.IsMouseHovering ? Color.Gold : Color.Black;
                button.BackgroundColor = RedBackground * (button.IsMouseHovering ? 1 / 0.7f : 1);
            }
            else
            {
                button.IgnoresMouseInteraction = true;
                button.TextColor = Color.White * 0.3f;
                button.BorderColor = Color.Black;
                button.BackgroundColor = RedBackground  * 0.3f;
            }
        }

        private void TabButtonUpdate(UIImageFramed button)
        {
            IColorable colorable = iconsByButtons[button] as IColorable;
            int index = indexByButtons[button];
            bool flag = (currentPage == index);
            bool isMouseHovering = button.IsMouseHovering;
            int frameX = flag.ToInt();
            int frameY = flag.ToInt() * 2 + isMouseHovering.ToInt();
            button.SetFrame(2, 4, frameX, frameY, -2, -2);
            button.Top.Set(-34 + container.Top.Pixels, 0);
            button.Left.Set(40 * index + 10 + container.Left.Pixels, 0);
            if (colorable != null)
            {
                colorable.Color = (flag ? (tweenToPage != index ? (Color.White * (1 - tweenValue)) : Color.Gold) : (Color.White * 0.5f));
            }
            if (button.IsMouseHovering)
            {
                Main.instance.MouseText(tabNames[index], 0, 0);
                Main.LocalPlayer.mouseInterface = true;
            }
            button.Recalculate();
        }
    }
}
