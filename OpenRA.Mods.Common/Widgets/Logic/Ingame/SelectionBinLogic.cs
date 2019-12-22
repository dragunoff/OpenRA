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
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class SelectionBinLogic : ChromeLogic
	{
		readonly World world;
		readonly Widget widget;
		readonly SelectionPaletteWidget palette;

		[ObjectCreator.UseCtor]
		public SelectionBinLogic(Widget widget, World world)
		{
			this.world = world;
			this.widget = widget;
			palette = widget.Get<SelectionPaletteWidget>("SELECTION_PALETTE");

			widget.Bounds.Width = palette.Columns * (palette.IconSize.X + palette.IconMargin.X);

			switch (palette.Origin)
			{
				case "bottom-right":
				{
					widget.Bounds.X -= widget.Bounds.Width;
					break;
				}
			}

			var background = widget.GetOrNull("PALETTE_BACKGROUND");
			if (background != null)
			{
				var icontemplate = background.Get("ICON_TEMPLATE");

				Action<int, int> updateBackground = (oldCount, newCount) =>
				{
					background.RemoveChildren();

					var oldHeight = widget.Bounds.Height;
					widget.Bounds.Height = palette.GetRowsCount() * (palette.IconSize.Y + palette.IconMargin.Y);

					switch (palette.Origin)
					{
						case "bottom-right":
						{
							widget.Bounds.Y += (oldHeight - widget.Bounds.Height);
							palette.OriginPos = new int2(widget.Bounds.X + widget.Bounds.Width, widget.Bounds.Y + widget.Bounds.Height);
							break;
						}
					}

					for (var i = 0; i < newCount; i++)
					{
						var xMultiplier = i % palette.Columns;
						var yMultiplier = i / palette.Columns;
						var bg = icontemplate.Clone();

						switch (palette.Origin)
						{
							case "top-left":
							{
								bg.Bounds.X = xMultiplier * (palette.IconSize.X + palette.IconMargin.X);
								bg.Bounds.Y = yMultiplier * (palette.IconSize.Y + palette.IconMargin.Y);
								break;
							}

							case "bottom-right":
							{
								bg.Bounds.X = widget.Bounds.Width - palette.IconSize.X - xMultiplier * (palette.IconSize.X + palette.IconMargin.X);
								bg.Bounds.Y = widget.Bounds.Height - palette.IconSize.Y - yMultiplier * (palette.IconSize.Y + palette.IconMargin.Y);
								break;
							}
						}

						background.AddChild(bg);
					}
				};

				palette.OnIconCountChanged += updateBackground;

				// Set the initial palette state
				updateBackground(0, 0);
			}
		}
	}
}
