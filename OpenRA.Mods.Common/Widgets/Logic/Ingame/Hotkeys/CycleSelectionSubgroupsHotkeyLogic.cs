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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Lint;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic.Ingame
{
	[ChromeLogicArgsHotkeys("CycleSelectionSubgroupsKey")]
	public class CycleSelectionSubgroupsHotkeyLogic : SingleHotkeyBaseLogic
	{
		readonly ISelection selection;
		readonly World world;

		[ObjectCreator.UseCtor]
		public CycleSelectionSubgroupsHotkeyLogic(Widget widget, ModData modData, WorldRenderer worldRenderer, World world, Dictionary<string, MiniYaml> logicArgs)
			: base(widget, modData, "CycleSelectionSubgroupsKey", "WORLD_KEYHANDLER", logicArgs)
		{
			selection = world.Selection;
			this.world = world;
		}

		protected override bool OnHotkeyActivated(KeyInput e)
		{
			var selectedActors = selection.Actors.Concat(selection.BackgroundActors)
				.Where(a => a.Owner == world.LocalPlayer && a.IsInWorld && !a.IsDead);

			if (!selectedActors.Any())
				return true;

			var foregroundActors = selectedActors
				.Where(a => selection.Contains(a));

			var classes = selectedActors
				.Select(a => a.Trait<Selectable>().Class)
				.Distinct()
				.OrderBy(c => c)
				.ToList();

			var foregroundClasses = foregroundActors
				.Select(a => a.Trait<Selectable>().Class)
				.Distinct()
				.OrderBy(c => c)
				.ToList();

			var currentIndex = classes.FindIndex(c => c == foregroundClasses.FirstOrDefault());
			var nextIndex = foregroundClasses.Count() == 1 && classes.Count() > 1 ? currentIndex + 1 : currentIndex;
			var nextClass = classes.ElementAtOrDefault(nextIndex);

			if (nextClass == null)
				nextClass = classes.First();

			// move to background
			foreach (var removed in selectedActors.Where(a => a.Trait<Selectable>().Class != nextClass).ToList())
				selection.Remove(removed, true);

			// promote to foreground
			foreach (var added in selectedActors.Where(a => a.Trait<Selectable>().Class == nextClass).ToList())
				selection.Add(added);

			return true;
		}
	}
}
