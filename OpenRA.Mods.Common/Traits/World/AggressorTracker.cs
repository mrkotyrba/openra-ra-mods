#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Records enemy actors that have recently damaged each player's units, so that AutoTarget",
		"with a non-zero AggressorPriorityBonus can prioritise targets that are currently attacking.",
		"Attach to the world actor.")]
	public class AggressorTrackerInfo : TraitInfo
	{
		[Desc("How many ticks an attacker is remembered as a threat after damaging one of the player's units.")]
		public readonly int MemoryDuration = 50;

		public override object Create(ActorInitializer init) { return new AggressorTracker(this); }
	}

	public class AggressorTracker : ITick
	{
		readonly AggressorTrackerInfo info;

		// victim player -> (attacker ActorID -> tick at which the threat expires)
		readonly Dictionary<Player, Dictionary<uint, int>> threatsByPlayer = [];
		int tick;

		public AggressorTracker(AggressorTrackerInfo info)
		{
			this.info = info;
		}

		// Remember that 'attacker' has just damaged one of 'victimOwner's units.
		public void ReportAttack(Player victimOwner, Actor attacker)
		{
			if (victimOwner == null || attacker == null)
				return;

			if (!threatsByPlayer.TryGetValue(victimOwner, out var threats))
				threatsByPlayer[victimOwner] = threats = [];

			threats[attacker.ActorID] = tick + info.MemoryDuration;
		}

		// Has 'candidate' recently attacked one of 'owner's units?
		public bool IsThreatTo(Player owner, Actor candidate)
		{
			if (owner == null || candidate == null)
				return false;

			return threatsByPlayer.TryGetValue(owner, out var threats)
				&& threats.TryGetValue(candidate.ActorID, out var expiry)
				&& expiry > tick;
		}

		void ITick.Tick(Actor self)
		{
			tick++;

			// Periodically drop expired entries so the dictionaries do not grow unbounded.
			if (tick % 32 == 0)
				foreach (var threats in threatsByPlayer.Values)
					foreach (var actorId in threats.Where(kv => kv.Value <= tick).Select(kv => kv.Key).ToList())
						threats.Remove(actorId);
		}
	}
}
