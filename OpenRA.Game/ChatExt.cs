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

using OpenRA.Network;
using OpenRA.Primitives;

namespace OpenRA
{
	public class ChatExt
	{
		public static void AddSystemLine(string text)
		{
			AddSystemLine("Battlefield Control", text);
		}

		public static void AddSystemLine(string prefix, string text)
		{
			Game.AddSystemLine(prefix, text, ChatPool.System);
		}

		public static void AddChatLine(string name, Color nameColor, string text)
		{
			Game.AddChatLine(name, nameColor, text, ChatPool.Chat);
		}

		public static void AddTranscribedChatLine(string text)
		{
			if (Game.Settings.Game.ChatPoolFilters.HasFlag(ChatPoolFilters.Transcriptions))
				Game.AddSystemLine("Battlefield Control", text, ChatPool.Transcriptions);
		}

		public static void AddFeedbackChatLine(string text)
		{
			if (Game.Settings.Game.ChatPoolFilters.HasFlag(ChatPoolFilters.Feedback))
				Game.AddSystemLine("Battlefield Control", text, ChatPool.Feedback);
		}

		public static void AddMissionChatLine(string name, Color nameColor, string text)
		{
			Game.AddChatLine(name, nameColor, text, ChatPool.Mission);
		}
	}
}
