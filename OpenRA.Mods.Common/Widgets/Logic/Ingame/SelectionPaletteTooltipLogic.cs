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
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class SelectionPaletteTooltipLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public SelectionPaletteTooltipLogic(Widget widget, TooltipContainerWidget tooltipContainer, Player player, Func<SelectionIcon> getTooltipIcon)
		{
			var world = player.World;
			var mapRules = world.Map.Rules;

			widget.IsVisible = () => getTooltipIcon() != null && getTooltipIcon().Actor != null;
			var nameLabel = widget.Get<LabelWidget>("NAME");
			var hotkeyLabel = widget.Get<LabelWidget>("HOTKEY");

			var font = Game.Renderer.Fonts[nameLabel.Font];
			var formatBuildTime = new CachedTransform<int, string>(time => WidgetUtils.FormatTime(time, world.Timestep));

			ActorInfo lastActor = null;
			Hotkey lastHotkey = Hotkey.Invalid;

			tooltipContainer.BeforeRender = () =>
			{
				var tooltipIcon = getTooltipIcon();
				if (tooltipIcon == null)
					return;

				var actor = tooltipIcon.Actor;
				if (actor == null)
					return;

				var hotkey = tooltipIcon.Hotkey != null ? tooltipIcon.Hotkey.GetValue() : Hotkey.Invalid;
				if (actor == lastActor && hotkey == lastHotkey)
					return;

				var tooltip = actor.TraitInfos<TooltipInfo>().FirstOrDefault(info => info.EnabledByDefault);
				var name = tooltip != null ? tooltip.Name : actor.Name;

				nameLabel.Text = name;

				var nameSize = font.Measure(name);
				var hotkeyWidth = 0;
				hotkeyLabel.Visible = hotkey.IsValid();

				if (hotkeyLabel.Visible)
				{
					var hotkeyText = "({0})".F(hotkey.DisplayString());

					hotkeyWidth = font.Measure(hotkeyText).X + 2 * nameLabel.Bounds.X;
					hotkeyLabel.Text = hotkeyText;
					hotkeyLabel.Bounds.X = nameSize.X + 2 * nameLabel.Bounds.X;
				}
				else
				{
					hotkeyWidth = nameLabel.Bounds.X;
				}

				widget.Bounds.Width = nameSize.X + hotkeyWidth + nameLabel.Bounds.X;

				lastActor = actor;
				lastHotkey = hotkey;
			};
		}
	}
}
