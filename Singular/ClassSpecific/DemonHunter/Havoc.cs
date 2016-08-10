﻿using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.CommonBot;
using System;
using System.Collections.Generic;
using System.Drawing;
using Singular.ClassSpecific.DeathKnight;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;

namespace Singular.ClassSpecific.DemonHunter
{
    public class Havoc
    {
        private static LocalPlayer Me => StyxWoW.Me;
	    private static DemonHunterSettings DemonHunterSettings => SingularSettings.Instance.DemonHunter();
        private static uint MaxFury => StyxWoW.Me.GetPowerInfo(WoWPowerType.Fury).Max;
        private static uint CurrentFury => StyxWoW.Me.GetPowerInfo(WoWPowerType.Fury).Current;
        private static uint FuryDeficit => MaxFury - CurrentFury;

        #region Normal Rotation

        [Behavior(BehaviorType.Combat, WoWClass.DemonHunter, WoWSpec.DemonHunterHavoc)]
        public static Composite CreateDemonHunterHavocCombat()
        {
            return new PrioritySelector(

                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    req => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.Heal),
                        SingularRoutine.MoveBehaviorInlineToCombat(BehaviorType.CombatBuffs),

                        Helpers.Common.CreateInterruptBehavior(),

                        Movement.WaitForFacing(),
                        Movement.WaitForLineOfSpellSight(),

                        Common.CreateDemonHunterPullMore(),

                        // Buffs + Mitigation
                        Spell.CastOnGround("Metamorphosis", on => (WoWUnit)on, ret => Me.HealthPercent <= DemonHunterSettings.HavocMetamorphosisHealthPercent),
                        Spell.BuffSelf("Blur", ret => Me.HealthPercent <= DemonHunterSettings.BlurHealthPercent),
                        Spell.BuffSelf("Darkness", ret => Me.HealthPercent <= DemonHunterSettings.HavocDarknessHealthPercent),

                        // AoE Rotation
                        new Decorator(
                            ret => Spell.UseAOE && Unit.NearbyUnfriendlyUnits.Count(u => u.MeleeDistance() < 10) > 1,
                            new PrioritySelector(
                                Spell.Cast("Fel Rush",
                                    ret =>
                                        DemonHunterSettings.DPSWithFelRush &&
                                        (Common.HasTalent(DemonHunterTalents.FelMastery) && Havoc.CurrentFury <= 70) || (Unit.NearbyUnfriendlyUnits.Count() >= 3 && Spell.GetCharges("Fel Rush") > 1)),
                                Spell.Cast("Vengeful Retreat",
                                    ret =>
                                        DemonHunterSettings.UseVengefulRetreat &&
                                        (CurrentFury <= 85 || Common.HasTalent(DemonHunterTalents.Prepared)) && DemonHunterSettings.UseVengefulRetreat),
                                Spell.Cast("Eye Beam"),
                                Spell.Cast("Chaos Strike", ret => Unit.NearbyUnfriendlyUnits.Count() <= 3 && Common.HasTalent(DemonHunterTalents.ChaosCleave)),
                                Spell.Cast("Blade Dance", ret => Unit.NearbyUnfriendlyUnits.Count() >= 4),
                                Spell.Cast("Chaos Strike", ret => CurrentFury >= 70),
                                Spell.Cast("Demon's Bite", ret => !Common.HasTalent(DemonHunterTalents.DemonBlades))
                                )
                        ),

                        // Single Target Rotation
                        Spell.Cast("Vengeful Retreat",
                            ret =>
                                DemonHunterSettings.UseVengefulRetreat &&
                                (CurrentFury <= 85 || Common.HasTalent(DemonHunterTalents.Prepared))),
                        Spell.Cast("Fel Rush",
                            ret =>
                                DemonHunterSettings.DPSWithFelRush &&
                                (Common.HasTalent(DemonHunterTalents.FelMastery) && CurrentFury <= 70) || Spell.GetCharges("Fel Rush") > 1),
                        Spell.Cast("Chaos Strike", ret => CurrentFury >= 70),
                        Spell.Cast("Demon's Bite", ret => !Common.HasTalent(DemonHunterTalents.DemonBlades))

                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        #endregion


    }
}