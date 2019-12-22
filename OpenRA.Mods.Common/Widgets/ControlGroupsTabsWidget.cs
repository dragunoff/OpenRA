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
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class ControlGroupTab
	{
		public string Name;
		public ProductionQueue Queue;
	}

	public class ControlGroupTabGroup
	{
		public List<ControlGroupTab> Tabs = new List<ControlGroupTab>();
		public string Group;
		public int NextQueueName = 1;
	}

	public class ControlGroupTabsWidget : Widget
	{
		readonly World world;

		public readonly string PaletteWidget = null;
		public readonly string TypesContainer = null;
		public readonly string BackgroundContainer = null;

		public readonly int TabWidth = 30;
		public readonly int ArrowWidth = 20;

		public readonly string ClickSound = ChromeMetrics.Get<string>("ClickSound");
		public readonly string ClickDisabledSound = ChromeMetrics.Get<string>("ClickDisabledSound");

		public readonly Dictionary<string, ControlGroupTabGroup> Groups;

		public string Button = "button";
		public string Background = "panel-black";

		int contentWidth = 0;
		float listOffset = 0;
		bool leftPressed = false;
		bool rightPressed = false;
		Rectangle leftButtonRect;
		Rectangle rightButtonRect;
		Lazy<ProductionPaletteWidget> paletteWidget;
		string queueGroup;

		[ObjectCreator.UseCtor]
		public ControlGroupTabsWidget(World world)
		{
			this.world = world;

			Groups = world.Map.Rules.Actors.Values.SelectMany(a => a.TraitInfos<ProductionQueueInfo>())
				.Select(q => q.Group).Distinct().ToDictionary(g => g, g => new ControlGroupTabGroup() { Group = g });

			paletteWidget = Exts.Lazy(() => Ui.Root.Get<ProductionPaletteWidget>(PaletteWidget));
		}

		public bool SelectNextTab(bool reverse)
		{
			if (queueGroup == null)
				return true;

			// Prioritize alerted queues
			var queues = Groups[queueGroup].Tabs.Select(t => t.Queue)
					.OrderByDescending(q => q.AllQueued().Any(i => i.Done) ? 1 : 0)
					.ToList();

			if (reverse) queues.Reverse();

			CurrentQueue = queues.SkipWhile(q => q != CurrentQueue)
				.Skip(1).FirstOrDefault() ?? queues.FirstOrDefault();

			return true;
		}

		public void PickUpCompletedBuilding()
		{
			// This is called from ControlGroupTabsLogic
			paletteWidget.Value.PickUpCompletedBuilding();
		}

		public string QueueGroup
		{
			get
			{
				return queueGroup;
			}

			set
			{
				listOffset = 0;
				queueGroup = value;
				SelectNextTab(false);
			}
		}

		public ProductionQueue CurrentQueue
		{
			get
			{
				return paletteWidget.Value.CurrentQueue;
			}

			set
			{
				paletteWidget.Value.CurrentQueue = value;
				queueGroup = value != null ? value.Info.Group : null;

				// TODO: Scroll tabs so selected queue is visible
			}
		}

		public override void Draw()
		{
			var tabs = Groups[queueGroup].Tabs.Where(t => t.Queue.BuildableItems().Any());

			if (!tabs.Any())
				return;

			var rb = RenderBounds;
			leftButtonRect = new Rectangle(rb.X, rb.Y, ArrowWidth, rb.Height);
			rightButtonRect = new Rectangle(rb.Right - ArrowWidth, rb.Y, ArrowWidth, rb.Height);

			var leftDisabled = listOffset >= 0;
			var leftHover = Ui.MouseOverWidget == this && leftButtonRect.Contains(Viewport.LastMousePos);
			var rightDisabled = listOffset <= Bounds.Width - rightButtonRect.Width - leftButtonRect.Width - contentWidth;
			var rightHover = Ui.MouseOverWidget == this && rightButtonRect.Contains(Viewport.LastMousePos);

			WidgetUtils.DrawPanel(Background, rb);
			ButtonWidget.DrawBackground(Button, leftButtonRect, leftDisabled, leftPressed, leftHover, false);
			ButtonWidget.DrawBackground(Button, rightButtonRect, rightDisabled, rightPressed, rightHover, false);

			WidgetUtils.DrawRGBA(ChromeProvider.GetImage("scrollbar", leftPressed || leftDisabled ? "left_pressed" : "left_arrow"),
				new float2(leftButtonRect.Left + 2, leftButtonRect.Top + 2));
			WidgetUtils.DrawRGBA(ChromeProvider.GetImage("scrollbar", rightPressed || rightDisabled ? "right_pressed" : "right_arrow"),
				new float2(rightButtonRect.Left + 2, rightButtonRect.Top + 2));

			// Draw tab buttons
			Game.Renderer.EnableScissor(new Rectangle(leftButtonRect.Right, rb.Y + 1, rightButtonRect.Left - leftButtonRect.Right - 1, rb.Height));
			var origin = new int2(leftButtonRect.Right - 1 + (int)listOffset, leftButtonRect.Y);
			var font = Game.Renderer.Fonts["TinyBold"];
			contentWidth = 0;

			foreach (var tab in tabs)
			{
				var rect = new Rectangle(origin.X + contentWidth, origin.Y, TabWidth, rb.Height);
				var hover = !leftHover && !rightHover && Ui.MouseOverWidget == this && rect.Contains(Viewport.LastMousePos);
				var highlighted = tab.Queue == CurrentQueue;
				ButtonWidget.DrawBackground(Button, rect, false, false, hover, highlighted);
				contentWidth += TabWidth - 1;

				var textSize = font.Measure(tab.Name);
				var position = new int2(rect.X + (rect.Width - textSize.X) / 2, rect.Y + (rect.Height - textSize.Y) / 2);
				font.DrawTextWithContrast(tab.Name, position, tab.Queue.AllQueued().Any(i => i.Done) ? Color.Gold : Color.White, Color.Black, 1);
			}

			Game.Renderer.DisableScissor();
		}

		void Scroll(int amount)
		{
			listOffset += amount * Game.Settings.Game.UIScrollSpeed;
			listOffset = Math.Min(0, Math.Max(Bounds.Width - rightButtonRect.Width - leftButtonRect.Width - contentWidth, listOffset));
		}

		public override void Tick()
		{
			if (leftPressed) Scroll(1);
			if (rightPressed) Scroll(-1);
		}

		public override bool YieldMouseFocus(MouseInput mi)
		{
			leftPressed = rightPressed = false;
			return base.YieldMouseFocus(mi);
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event == MouseInputEvent.Scroll)
			{
				Scroll(mi.Delta.Y);
				return true;
			}

			if (mi.Button != MouseButton.Left)
				return true;

			if (mi.Event == MouseInputEvent.Down && !TakeMouseFocus(mi))
				return true;

			if (!HasMouseFocus)
				return true;

			if (HasMouseFocus && mi.Event == MouseInputEvent.Up)
				return YieldMouseFocus(mi);

			leftPressed = leftButtonRect.Contains(mi.Location);
			rightPressed = rightButtonRect.Contains(mi.Location);
			var leftDisabled = listOffset >= 0;
			var rightDisabled = listOffset <= Bounds.Width - rightButtonRect.Width - leftButtonRect.Width - contentWidth;

			if (leftPressed || rightPressed)
			{
				if ((leftPressed && !leftDisabled) || (rightPressed && !rightDisabled))
					Game.Sound.PlayNotification(world.Map.Rules, null, "Sounds", ClickSound, null);
				else
					Game.Sound.PlayNotification(world.Map.Rules, null, "Sounds", ClickDisabledSound, null);
			}

			// Check production tabs
			var offsetloc = mi.Location - new int2(leftButtonRect.Right - 1 + (int)listOffset, leftButtonRect.Y);
			if (offsetloc.X > 0 && offsetloc.X < contentWidth)
			{
				CurrentQueue = Groups[queueGroup].Tabs[offsetloc.X / (TabWidth - 1)].Queue;
				Game.Sound.PlayNotification(world.Map.Rules, null, "Sounds", ClickSound, null);
			}

			return true;
		}

		public override bool HandleKeyPress(KeyInput e)
		{
			if (e.Event != KeyInputEvent.Down)
				return false;

			return false;
		}
	}
}
