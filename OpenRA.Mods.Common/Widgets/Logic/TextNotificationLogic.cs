#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class TextNotificationLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public TextNotificationLogic(Widget widget, TextNotification notification, int boxWidth, bool withTimestamp)
		{
			var timeLabel = widget.GetOrNull<LabelWidget>("TIME");
			var prefixLabel = widget.GetOrNull<LabelWidget>("PREFIX");
			var textLabel = widget.Get<LabelWidget>("TEXT");

			var textFont = Game.Renderer.Fonts[textLabel.Font];
			var textWidth = boxWidth - widget.Bounds.X - textLabel.Bounds.X;

			var hasPrefix = !string.IsNullOrEmpty(notification.Prefix) && prefixLabel != null;
			var timeOffset = 0;

			if (withTimestamp && timeLabel != null)
			{
				var time = $"{notification.Time.Hour:D2}:{notification.Time.Minute:D2}";
				timeOffset = timeLabel.Bounds.Width + timeLabel.Bounds.X;

				timeLabel.GetText = () => time;

				textWidth -= timeOffset;
				textLabel.Bounds.X += timeOffset;

				if (hasPrefix)
					prefixLabel.Bounds.X += timeOffset;
			}

			if (hasPrefix)
			{
				var prefix = notification.Prefix + ":";
				var prefixSize = Game.Renderer.Fonts[prefixLabel.Font].Measure(prefix);
				var prefixOffset = prefixSize.X + prefixLabel.Bounds.X;

				prefixLabel.GetColor = () => notification.PrefixColor ?? prefixLabel.TextColor;
				prefixLabel.GetText = () => prefix;
				prefixLabel.Bounds.Width = prefixSize.X;

				textWidth -= prefixOffset;
				textLabel.Bounds.X += prefixOffset - timeOffset;
			}

			textLabel.GetColor = () => notification.TextColor ?? textLabel.TextColor;
			textLabel.Bounds.Width = textWidth;

			// Hack around our hacky wordwrap behavior: need to resize the widget to fit the text
			var text = WidgetUtils.WrapText(notification.Text, textLabel.Bounds.Width, textFont);
			textLabel.GetText = () => text;
			var dh = textFont.Measure(text).Y - textLabel.Bounds.Height;
			if (dh > 0)
			{
				textLabel.Bounds.Height += dh;
				widget.Bounds.Height += dh;
			}

			widget.Bounds.Width = boxWidth - widget.Bounds.X;
		}
	}
}
