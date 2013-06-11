﻿using System.Linq;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Rest = Singular.Helpers.Rest;
using System.Drawing;
using System;

namespace Singular.ClassSpecific.Paladin
{
    public class Protection
    {

        #region Properties & Fields

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PaladinSettings PaladinSettings { get { return SingularSettings.Instance.Paladin(); } }

        private static int _aoeCount;

        #endregion

        [Behavior(BehaviorType.Rest, WoWClass.Paladin, WoWSpec.PaladinProtection)]
        public static Composite CreateProtectionRest()
        {
            return new PrioritySelector(
                // Rest up damnit! Do this first, so we make sure we're fully rested.
                Rest.CreateDefaultRestBehaviour( "Flash of Light", "Redemption")
                );
        }


        [Behavior(BehaviorType.Pull, WoWClass.Paladin, WoWSpec.PaladinProtection)]
        public static Composite CreatePaladinProtectionPull()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        Helpers.Common.CreateAutoAttack(true),
                        Spell.BuffSelf("Sacred Shield"),
                        Spell.Cast("Judgment"),
                        Spell.Cast("Avenger's Shield", ret => Spell.UseAOE ),
                        Spell.Cast("Reckoning", ret => !Me.CurrentTarget.IsPlayer)
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true)
                );
        }

        [Behavior(BehaviorType.CombatBuffs, WoWClass.Paladin, WoWSpec.PaladinProtection)]
        public static Composite CreatePaladinProtectionCombatBuffs()
        {
            return new PrioritySelector(
                Spell.BuffSelf("Devotion Aura", req => Me.Silenced),

                // Seal twisting. If our mana gets stupid low, just throw on insight to get some mana back quickly, then put our main seal back on.
                // This is Seal of Truth once we get it, Righteousness when we dont.
                Common.CreatePaladinSealBehavior(),

                new Decorator(
                    req => !Unit.IsTrivial(Me.CurrentTarget),
                    new PrioritySelector(

                        // Defensive
                        Spell.BuffSelf("Sacred Shield"),

                        Spell.BuffSelf("Hand of Freedom",
                            ret => Me.HasAuraWithMechanic(WoWSpellMechanic.Dazed,
                                                                    WoWSpellMechanic.Disoriented,
                                                                    WoWSpellMechanic.Frozen,
                                                                    WoWSpellMechanic.Incapacitated,
                                                                    WoWSpellMechanic.Rooted,
                                                                    WoWSpellMechanic.Slowed,
                                                                    WoWSpellMechanic.Snared)),

                        Spell.BuffSelf("Divine Shield",
                            ret => Me.CurrentMap.IsBattleground && Me.HealthPercent <= 20 && !Me.HasAura("Forbearance")),

                        Spell.BuffSelf( "Lay on Hands",
                            ret => Me.HealthPercent <= PaladinSettings.LayOnHandsHealth && !Me.HasAura("Forbearance")),

                        Spell.BuffSelf("Avenging Wrath", 
                            ret => (Me.GotTarget && Me.CurrentTarget.IsWithinMeleeRange && (Me.CurrentTarget.TimeToDeath() > 25) || _aoeCount > 1)),

                        Spell.BuffSelf(
                            "Guardian of Ancient Kings",
                            ret => Me.HealthPercent <= PaladinSettings.GoAKHealth),

                        Spell.BuffSelf(
                            "Ardent Defender",
                            ret => Me.HealthPercent <= PaladinSettings.ArdentDefenderHealth),

                        Spell.BuffSelf(
                            "Divine Protection",
                            ret => Me.HealthPercent <= PaladinSettings.DivineProtectionHealthProt),

                    // Symbiosis
                        Spell.BuffSelf(
                            "Barkskin",
                            ret => Me.HealthPercent <= PaladinSettings.DivineProtectionHealthProt
                                && !Me.HasAura("Divine Protection")
                                && Spell.GetSpellCooldown("Divine Protection", 6).TotalSeconds > 0),

                        Common.CreateWordOfGloryBehavior( on => Me )
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Paladin, WoWSpec.PaladinProtection)]
        public static Composite CreateProtectionCombat()
        {
            return new PrioritySelector(
                Helpers.Common.EnsureReadyToAttackFromMelee(),
                Spell.WaitForCastOrChannel(),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        new Action( r => {
                            // Paladin AOE count should be those near paladin (consecrate, holy wrath) and those near target (avenger's shield)
                            _aoeCount = TankManager.Instance.TargetList.Count(u => u.SpellDistance() < 10 || u.Location.Distance(Me.CurrentTarget.Location) < 10);
                            return RunStatus.Failure;
                            }),

                        CreateProtDiagnosticOutputBehavior(),

                        Helpers.Common.CreateAutoAttack(true),
                        Helpers.Common.CreateInterruptBehavior(),

                        // Taunts - if reckoning on cooldown, throw some damage at them
                        new Decorator(
                            ret => SingularSettings.Instance.EnableTaunting
                                && TankManager.Instance.NeedToTaunt.Any()
                                && TankManager.Instance.NeedToTaunt.FirstOrDefault().InLineOfSpellSight,
                            new Throttle(TimeSpan.FromMilliseconds(1500),
                                new PrioritySelector(
                                    Spell.Cast("Reckoning", ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault()),
                                    Spell.Cast("Avenger's Shield", ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault(), req => Spell.UseAOE ),
                                    Spell.Cast("Judgment", ctx => TankManager.Instance.NeedToTaunt.FirstOrDefault())
                                    )
                                )
                            ),

                        // Soloing move - open with stun to reduce incoming damage (best to take Fist of Justice talent if doing this
                        Spell.Cast("Hammer of Justice", ret => PaladinSettings.StunMobsWhileSolo && SingularRoutine.CurrentWoWContext == WoWContext.Normal),

                        //Multi target
                        new Decorator(
                            ret => _aoeCount >= 4 && Spell.UseAOE,
                            new PrioritySelector(
                                Spell.Cast("Shield of the Righteous", ret => Me.CurrentHolyPower >= 3 || Me.ActiveAuras.ContainsKey("Divine Purpose")),
                                Spell.Cast("Judgment", ret => Common.HasTalent(PaladinTalents.SanctifiedWrath) && Me.HasAura("Avenging Wrath")),
                                Spell.Cast("Hammer of the Righteous", ret => Unit.NearbyUnfriendlyUnits.Any(u => Me.SpellDistance(u) < 8 && !u.HasAura("Weakened Blows"))),
                                Spell.Cast("Judgment"),
                                Spell.Cast("Avenger's Shield"),
                                Spell.Cast("Consecration", ret => !Me.IsMoving),
                        /// level 90 talents
                                Spell.Cast("Holy Prism", on => Me),           // target enemy for Single target
                                Spell.CastOnGround("Light's Hammer", loc => Me.CurrentTarget.Location, ret => Me.CurrentTarget != null, false),       // no mana cost
                                Spell.Cast("Execution Sentence", 
                                    on => Unit.NearbyUnfriendlyUnits.FirstOrDefault( u => u.HealthPercent < 20 && Me.IsSafelyFacing(u) && u.InLineOfSpellSight )),               // no mana cost
                        /// end of talents
                                Spell.Cast("Holy Wrath"),
                                Movement.CreateMoveToMeleeBehavior(true)
                                )
                            ),

                        //Single target
                        Spell.Cast("Shield of the Righteous", ret => Me.CurrentHolyPower >= 3 || Me.ActiveAuras.ContainsKey("Divine Purpose")),
                        Spell.Cast("Hammer of the Righteous", ret => Spell.UseAOE && !Me.CurrentTarget.ActiveAuras.ContainsKey("Weakened Blows")),
                        Spell.Cast("Judgment", ret => SpellManager.HasSpell("Sanctified Wrath") && Me.HasAura("Avenging Wrath")),
                        Spell.Cast("Crusader Strike"),
                        Spell.Cast("Judgment"),
                        Spell.Cast("Avenger's Shield", ret => Spell.UseAOE),
                        Spell.Cast("Consecration", ret => Spell.UseAOE && !Me.IsMoving),
                        Spell.Cast("Hammer of Wrath", ret => Me.CurrentTarget.HealthPercent < 20),
                        /// level 90 talent if avail
                        Spell.Cast("Holy Prism", ret => Spell.UseAOE),           // target enemy for Single target
                        Spell.CastOnGround("Light's Hammer", loc => Me.CurrentTarget.Location, ret => Spell.UseAOE && Me.GotTarget, false ),       // no mana cost
                        Spell.Cast("Execution Sentence", ret => Me.CurrentTarget.HealthPercent < 20),               // no mana cost
                        /// back to normal
                        Spell.Cast("Holy Wrath")
                        )
                    ),

                Movement.CreateMoveToMeleeBehavior(true));
        }

        private static Composite CreateProtDiagnosticOutputBehavior()
        {
            if (!SingularSettings.Debug)
                return new Action(ret => { return RunStatus.Failure; });

            return new Sequence(
                new ThrottlePasses(1, 1,
                    new Action(ret =>
                    {
                        string sMsg;
                        sMsg = string.Format(".... h={0:F1}%, m={1:F1}%, moving={2}, hpower={3}, grandcru={4}, divpurp={5}, sacshld={6}, mobs={7}",
                            Me.HealthPercent,
                            Me.ManaPercent,
                            Me.IsMoving,
                            Me.CurrentHolyPower,
                            (long)Me.GetAuraTimeLeft("Grand Crusader").TotalMilliseconds,
                            (long)Me.GetAuraTimeLeft("Divine Purpose").TotalMilliseconds,
                            (long)Me.GetAuraTimeLeft("Sacred Shield").TotalMilliseconds, 

                            _aoeCount
                            );

                        WoWUnit target = Me.CurrentTarget;
                        if (target != null)
                        {
                            sMsg += string.Format(
                                ", {0}, {1:F1}%, {2:F1} yds, threat={3}% loss={4}",
                                target.SafeName(),
                                target.HealthPercent,
                                target.Distance,
                                (int) target.ThreatInfo.RawPercent,
                                target.InLineOfSpellSight
                                );
                        }

                        Logger.WriteDebug(Color.LightYellow, sMsg);
                        return RunStatus.Failure;
                    })
                    )
                );
        }
    }
}
