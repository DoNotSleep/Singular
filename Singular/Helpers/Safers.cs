﻿using System.Linq;
using CommonBehaviors.Actions;

using Singular.Settings;

using Styx;
using Styx.Logic;
using Styx.Logic.POI;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Singular.Managers;
using Action = TreeSharp.Action;

namespace Singular.Helpers
{
    internal static class Safers
    {
        /// <summary>
        ///  This behavior SHOULD be called at top of the combat behavior. This behavior won't let the rest of the combat behavior to be called
        /// if you don't have a target. Also it will find a proper target, if the current target is dead or you don't have a target and still in combat.
        /// Tank targeting is also dealed in this behavior.
        /// </summary>
        /// <returns></returns>
        public static Composite EnsureTarget()
        {
            return
                new Decorator(
                    ret => !SingularSettings.Instance.DisableAllTargeting,
                    new PrioritySelector(
                        new Decorator(
                            // DisableTankTargeting is a user-setting. NeedTankTargeting is an internal one. Make sure both are turned on.
                            ret => !SingularSettings.Instance.DisableTankTargetSwitching && TankManager.NeedTankTargeting
                                   && TankManager.TargetingTimer.IsFinished && StyxWoW.Me.Combat &&
                                   TankManager.Instance.FirstUnit != null && 
                                   (StyxWoW.Me.CurrentTarget == null || StyxWoW.Me.CurrentTarget != TankManager.Instance.FirstUnit),
                            new Action(
                                ret =>
                                    {
                                        Logger.WriteDebug("Targeting first unit of TankTargeting");
                                        TankManager.Instance.FirstUnit.Target();
                                        StyxWoW.SleepForLagDuration();
                                        TankManager.TargetingTimer.Reset();
                                    })),
                        new Decorator(
                            ret => StyxWoW.Me.CurrentTarget == null || StyxWoW.Me.CurrentTarget.Dead,
                            new PrioritySelector(
                                ctx =>
                                    {
                                        // If we have a RaF leader, then use its target.
                                        var rafLeader = RaFHelper.Leader;
                                        if (rafLeader != null && rafLeader.IsValid && !rafLeader.IsMe && rafLeader.Combat && 
                                            rafLeader.CurrentTarget != null && rafLeader.CurrentTarget.IsAlive && !Blacklist.Contains(rafLeader.CurrentTarget))
                                        {
                                            return rafLeader;
                                        }

                                        // Check bot poi.
                                        if (BotPoi.Current.Type == PoiType.Kill)
                                        {
                                            var unit = BotPoi.Current.AsObject as WoWUnit;

                                            if (unit != null && unit.IsAlive && !unit.IsMe && !Blacklist.Contains(unit))
                                            {
                                                return unit;
                                            }
                                        }

                                        // Does the target list have anything in it? And is the unit in combat?
                                        // Make sure we only check target combat, if we're NOT in a BG. (Inside BGs, all targets are valid!!)
                                        var firstUnit = Targeting.Instance.FirstUnit;
                                        if (firstUnit != null && firstUnit.IsAlive && !firstUnit.IsMe && StyxWoW.Me.Combat &&
                                            !Blacklist.Contains(firstUnit))
                                        {
                                            return firstUnit;
                                        }

                                        // Cache this query, since we'll be using it for 2 checks. No need to re-query it.
                                        var agroMob =
                                            ObjectManager.GetObjectsOfType<WoWUnit>(false, false).
                                                Where( p => !Blacklist.Contains(p) && p.IsHostile && !p.IsOnTransport && !p.Dead &&
                                                            !p.Mounted && p.DistanceSqr <= 70*70 && p.Combat).
                                                OrderBy(u => u.DistanceSqr).
                                                FirstOrDefault();

                                        if (agroMob != null)
                                        {
                                            // Return the closest one to us
                                            return agroMob;
                                        }

                                        // And there's nothing left, so just return null, kthx.
                                        return null;
                                    },
                                // Make sure the target is VALID. If not, then ignore this next part. (Resolves some silly issues!)
                                new Decorator(
                                    ret => ret != null,
                                    new Sequence(
                                        new Action(ret => Logger.Write("Target is invalid. Switching to " + ((WoWUnit)ret).SafeName() + "!")),
                                        new Action(ret => ((WoWUnit)ret).Target()),
                                        new WaitContinue(
                                            2,
                                            ret => StyxWoW.Me.CurrentTarget != null &&
                                                   StyxWoW.Me.CurrentTarget == (WoWUnit)ret,
                                            new ActionAlwaysSucceed()))),
                                // In order to resolve getting "stuck" on a target, we'll clear it if there's nothing viable.
                                new Action(
                                    ret =>
                                        {
                                            StyxWoW.Me.ClearTarget();
                                            // Force a failure, just so we can move down the branch. to the log message
                                            return RunStatus.Failure;
                                        }),
                                new Action(ret => Logger.Write("No viable target! NOT GOOD!")),
                                new ActionAlwaysSucceed()))));
        }
    }
}
