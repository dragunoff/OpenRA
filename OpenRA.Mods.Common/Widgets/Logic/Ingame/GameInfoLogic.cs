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
using System.Linq;
using OpenRA.Mods.Common.Scripting;
using OpenRA.Mods.Common.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public enum IngameInfoPanel { AutoSelect, Map, Objectives, Debug, Chat, LobbbyOptions }

	class GameInfoLogic : ChromeLogic
	{
		readonly Widget tabContainer;
		readonly ButtonWidget tabTemplate;
		readonly int2 buttonStride;
		readonly List<ButtonWidget> buttons = new List<ButtonWidget>();

		[ObjectCreator.UseCtor]
		public GameInfoLogic(Widget widget, ModData modData, World world, IngameInfoPanel activePanel, Action<bool> hideMenu, Dictionary<string, MiniYaml> logicArgs)
		{
			var lp = world.LocalPlayer;
			var numTabs = 0;

			tabContainer = widget.Get("TAB_CONTAINER");
			tabTemplate = tabContainer.Get<ButtonWidget>("BUTTON_TEMPLATE");
			tabContainer.RemoveChild(tabTemplate);

			if (logicArgs.TryGetValue("ButtonStride", out var buttonStrideNode))
				buttonStride = FieldLoader.GetValue<int2>("ButtonStride", buttonStrideNode.Value);

			widget.IsVisible = () => activePanel != IngameInfoPanel.AutoSelect;

			// Objectives/Stats tab
			var scriptContext = world.WorldActor.TraitOrDefault<LuaScript>();
			var hasError = scriptContext != null && scriptContext.FatalErrorOccurred;
			var iop = world.WorldActor.TraitsImplementing<IObjectivesPanel>().FirstOrDefault();
			var hasObjectivesPanel = hasError || (iop != null && iop.PanelName != null);

			if (hasObjectivesPanel)
			{
				numTabs++;
				var objectivesTabButton = AddTab(string.Concat("BUTTON", numTabs.ToString()), "Objectives");
				objectivesTabButton.IsVisible = () => !hasError;
				objectivesTabButton.OnClick = () => activePanel = IngameInfoPanel.Objectives;
				objectivesTabButton.IsHighlighted = () => activePanel == IngameInfoPanel.Objectives;

				var panel = hasError ? "SCRIPT_ERROR_PANEL" : iop.PanelName;
				var objectivesPanel = widget.Get<ContainerWidget>("OBJECTIVES_PANEL");
				objectivesPanel.IsVisible = () => activePanel == IngameInfoPanel.Objectives;

				Game.LoadWidget(world, panel, objectivesPanel, new WidgetArgs()
				{
					{ "hideMenu", hideMenu }
				});

				if (activePanel == IngameInfoPanel.AutoSelect)
					activePanel = IngameInfoPanel.Objectives;
			}

			// Briefing tab
			var missionData = world.WorldActor.Info.TraitInfoOrDefault<MissionDataInfo>();
			if (missionData != null && !string.IsNullOrEmpty(missionData.Briefing))
			{
				numTabs++;
				var mapTabButton = AddTab(string.Concat("BUTTON", numTabs.ToString()), "Briefing");
				mapTabButton.IsVisible = () => !hasError;
				mapTabButton.OnClick = () => activePanel = IngameInfoPanel.Map;
				mapTabButton.IsHighlighted = () => activePanel == IngameInfoPanel.Map;

				var mapPanel = widget.Get<ContainerWidget>("MAP_PANEL");
				mapPanel.IsVisible = () => activePanel == IngameInfoPanel.Map;

				Game.LoadWidget(world, "MAP_PANEL", mapPanel, new WidgetArgs());

				if (activePanel == IngameInfoPanel.AutoSelect)
					activePanel = IngameInfoPanel.Map;
			}

			// Lobby Options tab
			numTabs++;
			var optionsTabButton = AddTab(string.Concat("BUTTON", numTabs.ToString()), "Options");
			optionsTabButton.IsVisible = () => !hasError;
			optionsTabButton.OnClick = () => activePanel = IngameInfoPanel.LobbbyOptions;
			optionsTabButton.IsHighlighted = () => activePanel == IngameInfoPanel.LobbbyOptions;

			var optionsPanel = widget.Get<ContainerWidget>("LOBBY_OPTIONS_PANEL");
			optionsPanel.IsVisible = () => activePanel == IngameInfoPanel.LobbbyOptions;

			Game.LoadWidget(world, "LOBBY_OPTIONS_PANEL", optionsPanel, new WidgetArgs()
			{
				{ "getMap", (Func<MapPreview>)(() => modData.MapCache[world.Map.Uid]) },
				{ "configurationDisabled", (Func<bool>)(() => true) }
			});

			if (activePanel == IngameInfoPanel.AutoSelect)
				activePanel = IngameInfoPanel.LobbbyOptions;

			// Debug/Cheats tab
			// Can't use DeveloperMode.Enabled because there is a hardcoded hack to *always*
			// enable developer mode for singleplayer games, but we only want to show the button
			// if it has been explicitly enabled
			var def = world.Map.Rules.Actors[SystemActors.Player].TraitInfo<DeveloperModeInfo>().CheckboxEnabled;
			var developerEnabled = world.LobbyInfo.GlobalSettings.OptionOrDefault("cheats", def);
			if (lp != null && developerEnabled)
			{
				numTabs++;
				var debugTabButton = AddTab(string.Concat("BUTTON", numTabs.ToString()), "Debug");
				debugTabButton.IsVisible = () => !hasError;
				debugTabButton.IsDisabled = () => world.IsGameOver;
				debugTabButton.OnClick = () => activePanel = IngameInfoPanel.Debug;
				debugTabButton.IsHighlighted = () => activePanel == IngameInfoPanel.Debug;

				var debugPanelContainer = widget.Get<ContainerWidget>("DEBUG_PANEL");
				debugPanelContainer.IsVisible = () => activePanel == IngameInfoPanel.Debug;

				Game.LoadWidget(world, "DEBUG_PANEL", debugPanelContainer, new WidgetArgs());

				if (activePanel == IngameInfoPanel.AutoSelect)
					activePanel = IngameInfoPanel.Debug;
			}

			if (world.LobbyInfo.NonBotClients.Count() > 1)
			{
				numTabs++;
				var chatPanelContainer = widget.Get<ContainerWidget>("CHAT_PANEL");
				var chatTabButton = AddTab(string.Concat("BUTTON", numTabs.ToString()), "Chat");
				chatTabButton.IsVisible = () => !hasError;
				chatTabButton.IsHighlighted = () => activePanel == IngameInfoPanel.Chat;
				chatTabButton.OnClick = () =>
				{
					activePanel = IngameInfoPanel.Chat;
					chatPanelContainer.Get<TextFieldWidget>("CHAT_TEXTFIELD").TakeKeyboardFocus();
				};

				chatPanelContainer.IsVisible = () => activePanel == IngameInfoPanel.Chat;

				Game.LoadWidget(world, "CHAT_CONTAINER", chatPanelContainer, new WidgetArgs() { { "isMenuChat", true } });

				if (activePanel == IngameInfoPanel.AutoSelect)
					chatTabButton.OnClick();
			}

			var titleText = widget.Get<LabelWidget>("TITLE");

			var mapTitle = world.Map.Title;
			var firstCategory = world.Map.Categories.FirstOrDefault();
			if (firstCategory != null)
				mapTitle = firstCategory + ": " + mapTitle;

			titleText.GetText = () => mapTitle;

			var bg = widget.Get<BackgroundWidget>("BACKGROUND");
		}

		ButtonWidget AddTab(string id, string label)
		{
			var tab = tabTemplate.Clone() as ButtonWidget;
			var lastButton = buttons.LastOrDefault();
			if (lastButton != null)
			{
				tab.Bounds.X = lastButton.Bounds.X + buttonStride.X;
				tab.Bounds.Y = lastButton.Bounds.Y + buttonStride.Y;
			}

			tab.Id = id;
			tab.GetText = () => label;
			tabContainer.AddChild(tab);
			buttons.Add(tab);

			return tab;
		}
	}
}
