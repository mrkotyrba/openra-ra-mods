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
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Tracks how much auto-target damage attacking units have committed to each target, so that",
		"AutoTarget with PreventOverkill can spread fire across targets instead of piling onto one.",
		"Attach to the world actor.")]
	public class AutoTargetOverkillLedgerInfo : TraitInfo<AutoTargetOverkillLedger> { }

	public class AutoTargetOverkillLedger
	{
		readonly Dictionary<uint, int> reservedByTarget = [];
		readonly Dictionary<uint, (uint Target, int Amount)> reservationByAttacker = [];

		// Register (or update) the damage an attacker has committed to a target.
		// Any previous reservation for this attacker is released first.
		public void Reserve(Actor attacker, Actor target, int amount)
		{
			Release(attacker);

			if (target == null || amount <= 0)
				return;

			reservationByAttacker[attacker.ActorID] = (target.ActorID, amount);
			reservedByTarget.TryGetValue(target.ActorID, out var current);
			reservedByTarget[target.ActorID] = current + amount;
		}

		// Drop an attacker's reservation (e.g. when it retargets, is disabled or leaves the world).
		public void Release(Actor attacker)
		{
			if (!reservationByAttacker.TryGetValue(attacker.ActorID, out var reservation))
				return;

			reservationByAttacker.Remove(attacker.ActorID);

			if (reservedByTarget.TryGetValue(reservation.Target, out var current))
			{
				var next = current - reservation.Amount;
				if (next > 0)
					reservedByTarget[reservation.Target] = next;
				else
					reservedByTarget.Remove(reservation.Target);
			}
		}

		// Damage already committed to a target by other attackers (excluding the querying attacker's own share).
		public int CommittedDamage(Actor target, Actor excluding)
		{
			if (!reservedByTarget.TryGetValue(target.ActorID, out var total))
				return 0;

			if (reservationByAttacker.TryGetValue(excluding.ActorID, out var reservation) && reservation.Target == target.ActorID)
				total -= reservation.Amount;

			return total;
		}
	}
}
