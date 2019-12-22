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
	public class ClassicSidebarTabsLogic : ChromeLogic
	{
		readonly ProductionPaletteWidget productionPalette;
		readonly SelectionPaletteWidget selectionPalette;
		readonly World world;
		ButtonWidget scrollUp;
		ButtonWidget scrollDown;
		ButtonWidget selectionTabButton;
		Widget background;
		Widget foreground;
		Widget backgroundTemplate;
		Widget backgroundBottom;
		Widget foregroundTemplate;

		void SetupProductionGroupButton(OrderManager orderManager, ProductionTypeButtonWidget button)
		{
			if (button == null)
				return;

			// Classic production queues are initialized at game start, and then never change.
			var queues = world.LocalPlayer.PlayerActor.TraitsImplementing<ProductionQueue>()
				.Where(q => (q.Info.Group ?? q.Info.Type) == button.ProductionGroup)
				.ToArray();

			Action selectTab = () =>
			{
				selectionPalette.Visible = false;
				productionPalette.Visible = true;

				productionPalette.CurrentQueue = queues.FirstOrDefault(q => q.Enabled);

				// When a tab is selected, scroll to the top because the current row position may be invalid for the new tab
				productionPalette.ScrollToTop();

				// Attempt to pick up a completed building (if there is one) so it can be placed
				productionPalette.PickUpCompletedBuilding();

				HookScrollUpDownButtons();
				UpdateBackground(productionPalette.DisplayedIconCount);
			};

			button.IsDisabled = () => !queues.Any(q => q.BuildableItems().Any());
			button.OnMouseUp = mi => selectTab();
			button.OnKeyPress = e => selectTab();
			button.OnClick = () => selectTab();
			button.IsHighlighted = () => productionPalette.IsVisible() && queues.Contains(productionPalette.CurrentQueue);

			var chromeName = button.ProductionGroup.ToLowerInvariant();
			var icon = button.Get<ImageWidget>("ICON");
			icon.GetImageName = () => button.IsDisabled() ? chromeName + "-disabled" :
				queues.Any(q => q.AllQueued().Any(i => i.Done)) ? chromeName + "-alert" : chromeName;
		}

		[ObjectCreator.UseCtor]
		public ClassicSidebarTabsLogic(Widget widget, OrderManager orderManager, World world)
		{
			this.world = world;
			productionPalette = widget.Get<ProductionPaletteWidget>("PRODUCTION_PALETTE");
			selectionPalette = widget.Get<SelectionPaletteWidget>("SELECTION_PALETTE");
			scrollUp = widget.GetOrNull<ButtonWidget>("SCROLL_UP_BUTTON");
			scrollDown = widget.GetOrNull<ButtonWidget>("SCROLL_DOWN_BUTTON");
			selectionTabButton = widget.GetOrNull<ButtonWidget>("SELECTION_TAB_BUTTON");
			background = widget.GetOrNull("PALETTE_BACKGROUND");
			foreground = widget.GetOrNull("PALETTE_FOREGROUND");

			var manualSelectionHash = world.Selection.ManualHash;

			if (background != null)
			{
				backgroundTemplate = background.Get("ROW_TEMPLATE");
				backgroundBottom = background.GetOrNull("BOTTOM_CAP");
			}

			if (foreground != null)
				foregroundTemplate = foreground.Get("ROW_TEMPLATE");

			if (background != null || foreground != null)
				SetupBackground();

			var typesContainer = widget.Get("PRODUCTION_TYPES");
			foreach (var i in typesContainer.Children)
				SetupProductionGroupButton(orderManager, i as ProductionTypeButtonWidget);

			SetupSelectionTabButton();

			var ticker = widget.Get<LogicTickerWidget>("PRODUCTION_TICKER");
			ticker.OnTick = () =>
			{
				if (!selectionPalette.HasSelection() && (productionPalette.CurrentQueue == null || productionPalette.DisplayedIconCount == 0))
				{
					// Select the first active tab
					foreach (var b in typesContainer.Children)
					{
						var button = b as ProductionTypeButtonWidget;
						if (button == null || button.IsDisabled())
							continue;

						button.OnClick();
						break;
					}
				}
				else if (manualSelectionHash != world.Selection.ManualHash)
				{
					manualSelectionHash = world.Selection.ManualHash;

					if (selectionPalette.IsProducer)
					{
						selectionPalette.Visible = false;
						productionPalette.Visible = true;
						productionPalette.ScrollToTop();
						HookScrollUpDownButtons();
						UpdateBackground(productionPalette.DisplayedIconCount);
					}
					else
						selectionTabButton.OnClick();
				}
			};

			HookScrollUpDownButtons();
			SetMaximumVisibleRows(productionPalette, selectionPalette);
		}

		void SetupBackground()
		{
			Action<int, int> updateBackground = (_, icons) => UpdateBackground(icons);

			productionPalette.OnIconCountChanged += updateBackground;
			selectionPalette.OnIconCountChanged += updateBackground;

			// Set the initial palette state
			UpdateBackground(0);
		}

		void UpdateBackground(int icons)
		{
			var rows = 0;

			if (productionPalette.IsVisible())
			{
				rows = Math.Max(productionPalette.MinimumRows, (icons + productionPalette.Columns - 1) / productionPalette.Columns);
				rows = Math.Min(rows, productionPalette.MaximumRows);
			}
			else if (selectionPalette.IsVisible())
			{
				rows = Math.Max(selectionPalette.MinimumRows, (icons + selectionPalette.Columns - 1) / selectionPalette.Columns);
				rows = Math.Min(rows, selectionPalette.MaximumRows);
			}

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
		}

		void SetupSelectionTabButton()
		{
			if (selectionTabButton == null)
				return;

			Action activateSelectionTab = () =>
			{
				selectionPalette.Visible = true;
				productionPalette.Visible = false;

				selectionPalette.ScrollToTop();
				HookScrollUpDownButtons();
				UpdateBackground(selectionPalette.DisplayedIconCount);
			};

			selectionTabButton.IsDisabled = () => !selectionPalette.HasSelection();
			selectionTabButton.OnMouseUp = (mi) => activateSelectionTab();
			selectionTabButton.OnKeyPress = e => activateSelectionTab();
			selectionTabButton.OnClick = () => activateSelectionTab();
			selectionTabButton.IsHighlighted = () => selectionPalette.IsVisible();

			var icon = selectionTabButton.Get<ImageWidget>("ICON");
			icon.GetImageName = () => selectionTabButton.IsDisabled() ? icon.ImageName + "-disabled" : icon.ImageName;
		}

		void HookScrollUpDownButtons()
		{
			if (scrollDown != null)
			{
				if (productionPalette.IsVisible())
				{
					scrollDown.OnClick = productionPalette.ScrollDown;
					scrollDown.IsVisible = () => productionPalette.TotalIconCount > (productionPalette.MaxIconRowOffset * productionPalette.Columns);
					scrollDown.IsDisabled = () => !productionPalette.CanScrollDown;
				}
				else if (selectionPalette.IsVisible())
				{
					scrollDown.OnClick = selectionPalette.ScrollDown;
					scrollDown.IsVisible = () => selectionPalette.TotalIconCount > (selectionPalette.MaxIconRowOffset * selectionPalette.Columns);
					scrollDown.IsDisabled = () => !selectionPalette.CanScrollDown;
				}
			}

			if (scrollUp != null)
			{
				if (productionPalette.IsVisible())
				{
					scrollUp.OnClick = productionPalette.ScrollUp;
					scrollUp.IsVisible = () => productionPalette.TotalIconCount > (productionPalette.MaxIconRowOffset * productionPalette.Columns);
					scrollUp.IsDisabled = () => !productionPalette.CanScrollUp;
				}
				else if (selectionPalette.IsVisible())
				{
					scrollUp.OnClick = selectionPalette.ScrollUp;
					scrollUp.IsVisible = () => selectionPalette.TotalIconCount > (selectionPalette.MaxIconRowOffset * selectionPalette.Columns);
					scrollUp.IsDisabled = () => !selectionPalette.CanScrollUp;
				}
			}
		}

		static void SetMaximumVisibleRows(ProductionPaletteWidget productionPalette, SelectionPaletteWidget selectionPalette)
		{
			var screenHeight = Game.Renderer.Resolution.Height;

			// Get height of currently displayed icons
			var containerWidget = Ui.Root.GetOrNull<ContainerWidget>("SIDEBAR_PRODUCTION");

			if (containerWidget == null)
				return;

			var sidebarProductionHeight = containerWidget.Bounds.Y;

			// Check if icon heights exceed y resolution
			var maxItemsHeight = screenHeight - sidebarProductionHeight;

			var maxIconRowOffestProduciton = (maxItemsHeight / productionPalette.IconSize.Y) - 1;
			productionPalette.MaxIconRowOffset = Math.Min(maxIconRowOffestProduciton, productionPalette.MaximumRows);

			var maxIconRowOffestSelection = (maxItemsHeight / selectionPalette.IconSize.Y) - 1;
			selectionPalette.MaxIconRowOffset = Math.Min(maxIconRowOffestSelection, selectionPalette.MaximumRows);
		}
	}
}
