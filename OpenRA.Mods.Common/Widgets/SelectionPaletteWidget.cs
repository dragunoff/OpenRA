#region Copyright & License Information
/*
 * Copyright 2007-2019 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Lint;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class SelectionIcon
	{
		public ActorInfo Actor;
		public string Class;
		public HotkeyReference Hotkey;
		public Sprite Sprite;
		public PaletteReference Palette;
		public int Count;
		public float2 Pos;
	}

	public class SelectionPaletteWidget : Widget
	{
		public readonly World World;
		readonly ModData modData;
		readonly WorldRenderer worldRenderer;

		IEnumerable<Actor> selectedActors;
		HashSet<string> selectedClasses;
		public bool IsProducer = false;
		public int SelectionHash;
		Dictionary<Rectangle, SelectionIcon> icons = new Dictionary<Rectangle, SelectionIcon>();

		public readonly string Origin = "top-left";
		public int2 OriginPos;

		public readonly string ClickSound = ChromeMetrics.Get<string>("ClickSound");
		public readonly string ClickDisabledSound = ChromeMetrics.Get<string>("ClickDisabledSound");

		public readonly int Columns = 3;
		public int MinimumRows = 4;
		public int MaximumRows = int.MaxValue;

		public int IconRowOffset = 0;
		public int MaxIconRowOffset = int.MaxValue;

		public int DisplayedIconCount { get; private set; }
		public int TotalIconCount { get; private set; }
		public event Action<int, int> OnIconCountChanged = (a, b) => { };

		public readonly string IconScale = "1";
		public readonly int2 IconSize = new int2(62, 48);
		public readonly int2 IconMargin = int2.Zero;
		public readonly int2 IconSpriteOffset = int2.Zero;

		public readonly string TooltipContainer;
		public readonly string TooltipTemplate = "SELECTION_PALETTE_TOOLTIP";
		Lazy<TooltipContainerWidget> tooltipContainer;
		public SelectionIcon TooltipIcon { get; private set; }
		public Func<SelectionIcon> GetTooltipIcon;

		// Note: LinterHotkeyNames assumes that these are disabled by default
		public readonly string HotkeyPrefix = null;
		public readonly int HotkeyCount = 0;

		HotkeyReference[] hotkeys;

		public override Rectangle EventBounds { get { return eventBounds; } }
		Rectangle eventBounds = Rectangle.Empty;

		SpriteFont overlayFont;
		public readonly int2 CountOffset = new int2(4, 2);

		[CustomLintableHotkeyNames]
		public static IEnumerable<string> LinterHotkeyNames(MiniYamlNode widgetNode, Action<string> emitError, Action<string> emitWarning)
		{
			var prefix = "";
			var prefixNode = widgetNode.Value.Nodes.FirstOrDefault(n => n.Key == "HotkeyPrefix");
			if (prefixNode != null)
				prefix = prefixNode.Value.Value;

			var count = 0;
			var countNode = widgetNode.Value.Nodes.FirstOrDefault(n => n.Key == "HotkeyCount");
			if (countNode != null)
				count = FieldLoader.GetValue<int>("HotkeyCount", countNode.Value.Value);

			if (count == 0)
				return new string[0];

			if (string.IsNullOrEmpty(prefix))
				emitError("{0} must define HotkeyPrefix if HotkeyCount > 0.".F(widgetNode.Location));

			return Exts.MakeArray(count, i => prefix + (i + 1).ToString("D2"));
		}

		[ObjectCreator.UseCtor]
		public SelectionPaletteWidget(ModData modData, World world, WorldRenderer worldRenderer)
		{
			this.modData = modData;
			this.worldRenderer = worldRenderer;
			World = world;
			GetTooltipIcon = () => TooltipIcon;
			tooltipContainer = Exts.Lazy(() =>
				Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));

			overlayFont = Game.Renderer.Fonts["TinyBold"];
		}

		public override void Initialize(WidgetArgs args)
		{
			base.Initialize(args);

			Bounds.Width = Columns * (IconSize.X + IconMargin.X);
			OriginPos = new int2(RenderBounds.X, RenderBounds.Y);

			hotkeys = Exts.MakeArray(HotkeyCount,
				i => modData.Hotkeys[HotkeyPrefix + (i + 1).ToString("D2")]);
		}

		public void ScrollDown()
		{
			if (CanScrollDown)
			{
				IconRowOffset++;
				RefreshIcons(force: true);
			}
		}

		public bool CanScrollDown
		{
			get
			{
				var totalRows = (TotalIconCount + Columns - 1) / Columns;

				return IconRowOffset < totalRows - MaxIconRowOffset;
			}
		}

		public void ScrollUp()
		{
			if (CanScrollUp)
			{
				IconRowOffset--;
				RefreshIcons(force: true);
			}
		}

		public bool CanScrollUp
		{
			get { return IconRowOffset > 0; }
		}

		public void ScrollToTop()
		{
			IconRowOffset = 0;
		}

		public override void Tick()
		{
			RefreshIcons();
		}

		public override void MouseEntered()
		{
			if (TooltipContainer != null)
				tooltipContainer.Value.SetTooltip(TooltipTemplate,
					new WidgetArgs() { { "player", World.LocalPlayer }, { "getTooltipIcon", GetTooltipIcon }, { "world", World } });
		}

		public override void MouseExited()
		{
			if (TooltipContainer != null)
				tooltipContainer.Value.RemoveTooltip();
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			var icon = icons.Where(i => i.Key.Contains(mi.Location))
				.Select(i => i.Value).FirstOrDefault();

			if (icon == null)
				return false;

			if (mi.Event == MouseInputEvent.Move)
				TooltipIcon = icon;

			// Eat mouse-up events
			if (mi.Event != MouseInputEvent.Down)
				return true;

			return HandleEvent(icon, mi.Button, mi.Modifiers);
		}

		bool HandleEvent(SelectionIcon icon, MouseButton btn, Modifiers modifiers)
		{
			var handled = btn == MouseButton.Left ? HandleLeftClick(icon, modifiers)
				: btn == MouseButton.Right ? HandleRightClick(icon)
				: false;

			if (!handled)
				Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Sounds", ClickDisabledSound, null);

			return true;
		}

		bool HandleLeftClick(SelectionIcon icon, Modifiers modifiers)
		{
			if (modifiers.HasModifier(Modifiers.Shift))
			{
				foreach (var removed in selectedActors.Where(a => a.Trait<Selectable>().Class == icon.Class).ToList())
					World.Selection.Remove(removed);
			}
			else
			{
				var newSelection = selectedActors.Where(a => a.Trait<Selectable>().Class == icon.Class).ToList();
				World.Selection.Combine(World, newSelection, false, false);
			}

			Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Sounds", ClickSound, null);

			return true;
		}

		bool HandleRightClick(SelectionIcon icon)
		{
			foreach (var removed in selectedActors.Where(a => a.Trait<Selectable>().Class == icon.Class).ToList())
				World.Selection.Remove(removed);

			Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Sounds", ClickSound, null);

			return true;
		}

		public override bool HandleKeyPress(KeyInput e)
		{
			if (e.Event == KeyInputEvent.Up || !HasSelection())
				return false;

			var batchModifiers = e.Modifiers.HasModifier(Modifiers.Shift) ? Modifiers.Shift : Modifiers.None;

			// HACK: enable deselection if the shift key is pressed
			e.Modifiers &= ~Modifiers.Shift;
			var toSelect = icons.Values.FirstOrDefault(i => i.Hotkey != null && i.Hotkey.IsActivatedBy(e));
			return toSelect != null ? HandleEvent(toSelect, MouseButton.Left, batchModifiers) : false;
		}

		void RefreshIcons(bool force = false)
		{
			if (!force && SelectionHash == World.Selection.Hash)
				return;

			SelectionHash = World.Selection.Hash;

			icons = new Dictionary<Rectangle, SelectionIcon>();

			var oldIconCount = DisplayedIconCount;
			DisplayedIconCount = 0;

			var player = World.RenderPlayer ?? World.LocalPlayer;

			selectedActors = World.Selection.Actors
				.Where(x => !x.IsDead && x.IsInWorld && x.Owner == player);

			selectedClasses = selectedActors
				.Select(a => a.Trait<Selectable>().Class)
				.ToHashSet();

			TotalIconCount = selectedClasses.Count();

			GroupIconsByClass();

			// recalculate bounds
			var oldHeight = Bounds.Height;
			Bounds.Height = GetRowsCount() * (IconSize.Y + IconMargin.Y);

			switch (Origin)
			{
				case "bottom-right":
				{
					Bounds.Y += (oldHeight - Bounds.Height);
					break;
				}
			}

			eventBounds = icons.Any() ? icons.Keys.Aggregate(Rectangle.Union) : Rectangle.Empty;

			if (oldIconCount != DisplayedIconCount)
				OnIconCountChanged(oldIconCount, DisplayedIconCount);
		}

		void GroupIconsByClass()
		{
			foreach (var currentClass in selectedClasses.Skip(IconRowOffset * Columns).Take(CalculateMaxIcons()))
			{
				var rect = CalculateIconRectangle();

				var actors = selectedActors.Where(a => a.Trait<Selectable>().Class == currentClass);
				var faction = actors.First().Owner.Faction.InternalName;
				var actorInfo = actors.First().Info;

				var rsi = actorInfo.TraitInfoOrDefault<RenderSpritesInfo>();
				var icon = new Animation(World, rsi.GetImage(actorInfo, World.Map.Rules.Sequences, faction));
				var si = actorInfo.TraitInfoOrDefault<SelectableInfo>();
				if (icon.HasSequence(si.Icon))
					icon.Play(si.Icon);

				var pi = new SelectionIcon()
				{
					Actor = actorInfo,
					Class = currentClass,
					Hotkey = DisplayedIconCount < HotkeyCount ? hotkeys[DisplayedIconCount] : null,
					Sprite = icon.HasSequence(si.Icon) ? icon.Image : null,
					Palette = worldRenderer.Palette(si.IconPalette),
					Count = actors.Count(),
					Pos = new float2(rect.Location)
				};

				icons.Add(rect, pi);
				DisplayedIconCount++;
			}
		}

		int CalculateMaxIcons()
		{
			try
			{
				checked
				{
					return MaxIconRowOffset * Columns;
				}
			}
			catch (OverflowException)
			{
				return int.MaxValue;
			}
		}

		Rectangle CalculateIconRectangle()
		{
			var rb = RenderBounds;
			var xMultiplier = DisplayedIconCount % Columns;
			var yMultiplier = DisplayedIconCount / Columns;
			var rectX = OriginPos.X;
			var rectY = OriginPos.Y;

			switch (Origin)
			{
				case "top-left":
				{
					rectX += xMultiplier * (IconSize.X + IconMargin.X);
					rectY += yMultiplier * (IconSize.Y + IconMargin.Y);
					break;
				}

				case "bottom-right":
				default:
				{
					rectX -= (IconSize.X + IconMargin.X) + xMultiplier * (IconSize.X + IconMargin.X);
					rectY -= (IconSize.Y + IconMargin.Y) + yMultiplier * (IconSize.Y + IconMargin.Y);
					break;
				}
			}

			return new Rectangle(rectX, rectY, IconSize.X, IconSize.Y);
		}

		public int GetRowsCount()
		{
			var rows = Math.Max(MinimumRows, (DisplayedIconCount + Columns - 1) / Columns);
			rows = Math.Min(rows, MaximumRows);

			return rows;
		}

		public bool HasSelection()
		{
			return selectedActors == null || selectedActors.Count() > 0;
		}

		public override void Draw()
		{
			var iconOffset = 0.5f * IconSize.ToFloat2() + IconSpriteOffset;

			// Icons
			foreach (var icon in icons.Values)
			{
				if (icon.Sprite != null)
					WidgetUtils.DrawSHPCentered(icon.Sprite, icon.Pos + iconOffset, icon.Palette, float.Parse(IconScale));

				if (icon.Count > 1)
					overlayFont.DrawTextWithContrast(icon.Count.ToString(), icon.Pos + CountOffset, Color.White, Color.Black, 1);
			}
		}
	}
}
