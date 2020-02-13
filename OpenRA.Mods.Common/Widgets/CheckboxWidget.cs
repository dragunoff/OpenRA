#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class CheckboxWidget : ButtonWidget
	{
		public string CheckType = "tick";
		public Func<string> GetCheckType;
		public Func<bool> IsChecked = () => false;
		public int CheckOffset = 2;

		[ObjectCreator.UseCtor]
		public CheckboxWidget(ModData modData)
			: base(modData)
		{
			GetCheckType = () => CheckType;
		}

		protected CheckboxWidget(CheckboxWidget other)
			: base(other)
		{
			CheckType = other.CheckType;
			GetCheckType = other.GetCheckType;
			IsChecked = other.IsChecked;
			CheckOffset = other.CheckOffset;
		}

		public override void Draw()
		{
			var @checked = IsChecked();
			var disabled = IsDisabled();
			var hover = Ui.MouseOverWidget == this;
			var font = Game.Renderer.Fonts[Font];
			var color = GetColor();
			var colordisabled = GetColorDisabled();
			var bgDark = GetContrastColorDark();
			var bgLight = GetContrastColorLight();
			var rect = RenderBounds;
			var text = GetText();
			var textSize = font.Measure(text);
			var check = new Rectangle(rect.Location, new Size(Bounds.Height, Bounds.Height));
			var baseName = @checked ? "checkbox-checked" : "checkbox";
			var state = WidgetUtils.GetStatefulImageName(baseName, disabled, Depressed, hover);

			WidgetUtils.DrawPanel(state, check);

			var topOffset = font.TopOffset;
			var position = new float2(rect.Left + rect.Height * 1.5f, RenderOrigin.Y + (Bounds.Height - textSize.Y - topOffset) / 2);

			if (Contrast)
				font.DrawTextWithContrast(text, position,
					disabled ? colordisabled : color, bgDark, bgLight, 2);
			else
				font.DrawText(text, position,
					disabled ? colordisabled : color);

			var offset = new float2(rect.Left + CheckOffset, rect.Top + CheckOffset);
			var checkmarkCollectionName = "checkmark-" + GetCheckType();
			var checkmarkBaseName = @checked ? "checked" : "unchecked";
			var checkmarkStateName = WidgetUtils.GetStatefulImageName(checkmarkBaseName, disabled, Depressed, hover);
			var checkmarkImage = ChromeProvider.GetImage(checkmarkCollectionName, checkmarkStateName) ?? ChromeProvider.GetImage(checkmarkCollectionName, checkmarkBaseName);

			if (@checked || checkmarkImage != null)
				WidgetUtils.DrawRGBA(checkmarkImage, offset);
		}

		public override Widget Clone() { return new CheckboxWidget(this); }
	}
}
