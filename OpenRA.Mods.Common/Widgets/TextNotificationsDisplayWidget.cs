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

using System;
using System.Collections.Generic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class TextNotificationsDisplayWidget : Widget
	{
		public readonly int RemoveTime = 0;
		public readonly int ItemSpacing = 4;
		public readonly int BottomSpacing = 0;
		public readonly int LogLength = 8;
		public readonly bool HideOverflow = true;

		public string ChatTemplate = "CHAT_LINE_TEMPLATE";
		public string SystemTemplate = "SYSTEM_LINE_TEMPLATE";
		public string MissionTemplate = "SYSTEM_LINE_TEMPLATE";
		public string FeedbackTemplate = "SYSTEM_LINE_TEMPLATE";
		readonly Dictionary<string, string> templates = new Dictionary<string, string>();

		readonly List<int> expirations = new List<int>();

		public override Rectangle EventBounds => Rectangle.Empty;

		public override void Initialize(WidgetArgs args)
		{
			base.Initialize(args);

			templates.Add("Chat", ChatTemplate);
			templates.Add("System", SystemTemplate);
			templates.Add("Mission", MissionTemplate);
			templates.Add("Feedback", FeedbackTemplate);
		}

		public override void DrawOuter()
		{
			if (!IsVisible() || Children.Count == 0)
				return;

			var lastChildOverflows = Bounds.Height < Children[Children.Count - 1].Bounds.Height;

			if (HideOverflow && lastChildOverflows)
				Game.Renderer.EnableScissor(GetDrawBounds());

			foreach (var child in Children)
				if (!HideOverflow || lastChildOverflows || Bounds.Contains(child.Bounds))
					child.DrawOuter();

			if (HideOverflow && lastChildOverflows)
				Game.Renderer.DisableScissor();
		}

		Rectangle GetDrawBounds()
		{
			var drawBounds = new Rectangle(RenderOrigin.X, RenderOrigin.Y, Bounds.Width, Bounds.Height);
			var visibleChildrenHeight = 0;
			var lastChild = Children[Children.Count - 1];

			if (lastChild.Bounds.Height > Bounds.Height)
			{
				var lineHeight = Game.Renderer.Fonts[lastChild.Get<LabelWidget>("TEXT").Font].Measure("").Y;
				var wholeLines = (int)Math.Floor((double)((Bounds.Height - BottomSpacing) / lineHeight));
				visibleChildrenHeight += wholeLines * lineHeight;
			}
			else
			{
				for (var i = Children.Count - 1; i >= 0; i--)
				{
					var childHeight = Children[i].Bounds.Height;
					childHeight += visibleChildrenHeight == 0 ? BottomSpacing : ItemSpacing;

					if (visibleChildrenHeight + childHeight >= Bounds.Height)
						break;

					visibleChildrenHeight += childHeight;
				}
			}

			if (visibleChildrenHeight > 0)
			{
				drawBounds.Y += Bounds.Height - visibleChildrenHeight;
				drawBounds.Height = visibleChildrenHeight;
			}

			return drawBounds;
		}

		public void AddNotification(TextNotification notification)
		{
			var notificationWidget = Ui.LoadWidget(templates[notification.Pool.ToString()], null, new WidgetArgs
			{
				{ "notification", notification },
				{ "boxWidth", Bounds.Width },
				{ "withTimestamp", false },
			});

			if (Children.Count == 0)
				notificationWidget.Bounds.Y = Bounds.Bottom - notificationWidget.Bounds.Height - BottomSpacing;
			else
			{
				foreach (var line in Children)
					line.Bounds.Y -= notificationWidget.Bounds.Height + ItemSpacing;

				var lastLine = Children[Children.Count - 1];
				notificationWidget.Bounds.Y = lastLine.Bounds.Bottom + ItemSpacing;
			}

			AddChild(notificationWidget);
			expirations.Add(Game.LocalTick + RemoveTime);

			while (Children.Count > LogLength)
				RemoveNotification();
		}

		public void RemoveMostRecentNotification()
		{
			if (Children.Count == 0)
				return;

			var mostRecentChild = Children[Children.Count - 1];

			RemoveChild(mostRecentChild);
			expirations.RemoveAt(expirations.Count - 1);

			for (var i = Children.Count - 1; i >= 0; i--)
				Children[i].Bounds.Y += mostRecentChild.Bounds.Height + ItemSpacing;
		}

		public void RemoveNotification()
		{
			if (Children.Count == 0)
				return;

			RemoveChild(Children[0]);
			expirations.RemoveAt(0);
		}

		public override void Tick()
		{
			if (RemoveTime == 0)
				return;

			// This takes advantage of the fact that recentLines is ordered by expiration, from sooner to later
			while (Children.Count > 0 && Game.LocalTick >= expirations[0])
				RemoveNotification();
		}
	}
}
