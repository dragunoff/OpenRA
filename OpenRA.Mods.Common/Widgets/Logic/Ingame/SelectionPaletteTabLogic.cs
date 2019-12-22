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
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Network;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class SelectionPaletteTabLogic : ChromeLogic
	{
		readonly SelectionPaletteWidget palette;
		readonly ProductionPaletteWidget productionPalette;
		readonly World world;

		[ObjectCreator.UseCtor]
		public SelectionPaletteTabLogic(Widget widget, World world)
		{
			this.world = world;
			palette = widget.Get<SelectionPaletteWidget>("SELECTION_PALETTE");
			productionPalette = widget.Get<ProductionPaletteWidget>("PRODUCTION_PALETTE");

			var background = widget.GetOrNull("PALETTE_BACKGROUND");
			var foreground = widget.GetOrNull("PALETTE_FOREGROUND");
			if (background != null || foreground != null)
			{
				Widget backgroundTemplate = null;
				Widget backgroundBottom = null;
				Widget foregroundTemplate = null;

				if (background != null)
				{
					backgroundTemplate = background.Get("ROW_TEMPLATE");
					backgroundBottom = background.GetOrNull("BOTTOM_CAP");
				}

				if (foreground != null)
					foregroundTemplate = foreground.Get("ROW_TEMPLATE");

				Action<int, int> updateBackground = (_, icons) =>
				{
					if (!palette.Visible)
						return;

					var rows = Math.Max(palette.MinimumRows, (icons + palette.Columns - 1) / palette.Columns);
					rows = Math.Min(rows, palette.MaximumRows);

					if (background != null)
					{
						background.RemoveChildren();

						var rowHeight = backgroundTemplate.Bounds.Height;
						for (var i = 0; i < rows; i++)
						{
							var row = backgroundTemplate.Clone();
							row.Bounds.Y = i * rowHeight;
							background.AddChild(row);
						}

						if (backgroundBottom == null)
							return;

						backgroundBottom.Bounds.Y = rows * rowHeight;
						background.AddChild(backgroundBottom);
					}

					if (foreground != null)
					{
						foreground.RemoveChildren();

						var rowHeight = foregroundTemplate.Bounds.Height;
						for (var i = 0; i < rows; i++)
						{
							var row = foregroundTemplate.Clone();
							row.Bounds.Y = i * rowHeight;
							foreground.AddChild(row);
						}
					}
				};

				palette.OnIconCountChanged += updateBackground;

				// Set the initial palette state
				updateBackground(0, 0);
			}

			var ticker = widget.Get<LogicTickerWidget>("SELECTION_TICKER");
			ticker.OnTick = () =>
			{
				if (palette.SelectionHash != world.Selection.Hash)
				{
					productionPalette.Visible = false;
					palette.Visible = true;
				}
			};

			// Hook up scroll up and down buttons on the palette
			var scrollDown = widget.GetOrNull<ButtonWidget>("SELECTION_SCROLL_DOWN_BUTTON");

			if (scrollDown != null)
			{
				scrollDown.OnClick = palette.ScrollDown;
				scrollDown.IsVisible = () => palette.TotalIconCount > (palette.MaxIconRowOffset * palette.Columns);
				scrollDown.IsDisabled = () => !palette.CanScrollDown;
			}

			var scrollUp = widget.GetOrNull<ButtonWidget>("SELECTION_SCROLL_UP_BUTTON");

			if (scrollUp != null)
			{
				scrollUp.OnClick = palette.ScrollUp;
				scrollUp.IsVisible = () => palette.TotalIconCount > (palette.MaxIconRowOffset * palette.Columns);
				scrollUp.IsDisabled = () => !palette.CanScrollUp;
			}

			SetMaximumVisibleRows(palette);
		}

		static void SetMaximumVisibleRows(SelectionPaletteWidget selectionPalette)
		{
			var screenHeight = Game.Renderer.Resolution.Height;

			// Get height of currently displayed icons
			var containerWidget = Ui.Root.GetOrNull<ContainerWidget>("SIDEBAR_PRODUCTION");

			if (containerWidget == null)
				return;

			var sidebarProductionHeight = containerWidget.Bounds.Y;

			// Check if icon heights exceed y resolution
			var maxItemsHeight = screenHeight - sidebarProductionHeight;

			var maxIconRowOffest = (maxItemsHeight / selectionPalette.IconSize.Y) - 1;
			selectionPalette.MaxIconRowOffset = Math.Min(maxIconRowOffest, selectionPalette.MaximumRows);
		}
	}
}
