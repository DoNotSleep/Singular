﻿using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;

using Styx;
using Styx.Combat.CombatRoutine;

using TreeSharp;
using Styx.Logic.Combat;

namespace Singular.ClassSpecific.Warrior
{
    public class Fury
    {
        private static string[] _slows;
        [Spec(TalentSpec.FuryWarrior)]
        [Behavior(BehaviorType.Combat)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.All)]
        public static Composite CreateFuryCombat()
        {
            _slows = new[] { "Hamstring", "Piercing Howl", "Crippling Poison", "Hand of Freedom", "Infected Wounds" };
            return new PrioritySelector(
                // Ensure Target
                Safers.EnsureTarget(),
                // Face Target
                Movement.CreateFaceTargetBehavior(),
                // LOS check
                Movement.CreateMoveToLosBehavior(),
                // Auto Attack
                Common.CreateAutoAttack(false),

                // Low level support
                new Decorator(
                    ret => StyxWoW.Me.Level < 30,
                    new PrioritySelector(
                        Spell.Cast("Victory Rush"),
                        Spell.Cast("Execute"),
                        Spell.Buff("Rend"),
                        Spell.Cast("Overpower"),
                        Spell.Cast("Bloodthirst"),
                        //rage dump
                        Spell.Cast("Thunder Clap", ret => StyxWoW.Me.RagePercent > 50 && Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) > 3),
                        Spell.Cast("Heroic Strike", ret => StyxWoW.Me.RagePercent > 60),
                        Movement.CreateMoveToTargetBehavior(true, 5f))),
                //30-50 support
                Spell.BuffSelf("Berserker Stance", ret => StyxWoW.Me.Level > 30 && StyxWoW.Me.Level < 50),

                // Intercept
                Spell.Cast("Intercept", ret => StyxWoW.Me.CurrentTarget.Distance > 10),
                //Heroic Leap
                Spell.CastOnGround("Heroic Leap", ret => StyxWoW.Me.CurrentTarget.Location, ret => StyxWoW.Me.CurrentTarget.Distance > 9 && !StyxWoW.Me.CurrentTarget.HasAura("Intercept", 1)),
                
                // ranged slow
                Spell.Buff("Piercing Howl", ret => StyxWoW.Me.CurrentTarget.Distance < 10 && StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows)),
                // melee slow
                Spell.Buff("Hamstring", ret => StyxWoW.Me.CurrentTarget.IsPlayer && !StyxWoW.Me.CurrentTarget.HasAnyAura(_slows)),     
           
                //Interupts
                Spell.Cast("Pummel", ret => StyxWoW.Me.CurrentTarget.IsCasting),

                //Heal up in mele
                Spell.Cast("Victory Rush", ret => StyxWoW.Me.HealthPercent < 80),
                Spell.Cast("Heroic Throw", ret => StyxWoW.Me.CurrentTarget.Distance > 15),

                // engineering gloves --- Still Bugged
                // Item.UseEquippedItem((uint)WoWInventorySlot.Hands),

                // AOE
                new Decorator(
                    ret => Clusters.GetClusterCount(StyxWoW.Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 6f) > 3,
                    new PrioritySelector(
                        Spell.BuffSelf("Recklessness"),
                        Spell.BuffSelf("Death Wish"),
                        Spell.Cast("Whirlwind"),
                        Spell.Cast("Cleave"),
                        Spell.Cast("Raging Blow"),
                        Spell.Cast("Blood Thirst"))),

                //Rotation under 20%
                Spell.Buff("Colossus Smash"),
                Spell.Cast("Execute"),
                //Rotation over 20%
                Spell.Cast("Heroic Strike", ret => StyxWoW.Me.HasAura("Incite", 1) || StyxWoW.Me.RagePercent > 60),
                Spell.Cast("Raging Blow"),
                Spell.Buff("Bloodthirst"),
                Spell.Cast("Slam", ret => StyxWoW.Me.HasAura("Bloodsurge")),
                //Move to Melee
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }

        [Spec(TalentSpec.FuryWarrior)]
        [Behavior(BehaviorType.Pull)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.All)]
        public static Composite CreateFuryPull()
        {
            return new PrioritySelector(
                // Ensure Target
                Safers.EnsureTarget(),
                //face target
                Movement.CreateFaceTargetBehavior(),
                // LOS check
                Movement.CreateMoveToLosBehavior(),
                // Auto Attack
                Common.CreateAutoAttack(false),

                //Dismount
                new Decorator(ret => StyxWoW.Me.Mounted,
                    new Action(o => Styx.Logic.Mount.Dismount())),

                // buff up
                Spell.Cast("Battle Shout", ret => StyxWoW.Me.RagePercent < 20),

                //Shoot flying targets
                new Decorator(
                    ret => StyxWoW.Me.CurrentTarget.IsFlying,
                    new PrioritySelector(
                        Spell.WaitForCast(),
                        Spell.Cast("Heroic Throw"),
                        Spell.Cast("Shoot"),
                        Spell.Cast("Throw")
                    )),

                //low level support
                new Decorator(
                    ret => StyxWoW.Me.Level < 50,
                    new PrioritySelector(
                        Spell.BuffSelf("Battle Stance"),
                        Spell.Cast("Charge", ret => StyxWoW.Me.CurrentTarget.Distance > 10 && StyxWoW.Me.CurrentTarget.Distance <= 25),
                        Spell.Cast("Heroic Throw", ret => !StyxWoW.Me.CurrentTarget.HasAura("Charge Stun")),
                        Movement.CreateMoveToTargetBehavior(true, 5f))),
                
                // Heroic fury
                Spell.Cast("Heroic Fury", ret => SpellManager.Spells["Intercept"].Cooldown),

                //Intercept
                Spell.Cast("Intercept", ret => StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25),
                //Heroic Leap
                Spell.CastOnGround("Heroic Leap", ret => StyxWoW.Me.CurrentTarget.Location, ret => StyxWoW.Me.CurrentTarget.Distance > 9 && !StyxWoW.Me.CurrentTarget.HasAura( "Intercept", 1)),

                // Move to Melee
                Movement.CreateMoveToTargetBehavior(true, 5f)
                );
        }

        [Spec(TalentSpec.FuryWarrior)]
        [Behavior(BehaviorType.CombatBuffs)]
        [Class(WoWClass.Warrior)]
        [Priority(500)]
        [Context(WoWContext.All)]
        public static Composite CreateFuryCombatBuffs()
        {
            return new PrioritySelector(
                //Heal
                Spell.Buff("Enraged Regeneration", ret => StyxWoW.Me.HealthPercent < 60),
                //Recklessness if low on hp or have Deathwish up or as gank protection
                Spell.BuffSelf("Recklessness"),
                // Heroic Fury
                Spell.BuffSelf("Heroic Fury", ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Rooted)),
                // Fear Remover
                Spell.BuffSelf("Berserker Rage", ret => StyxWoW.Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing, WoWSpellMechanic.Sapped, WoWSpellMechanic.Incapacitated, WoWSpellMechanic.Horrified)),
                //Deathwish, for both grinding and gank protection
                Spell.BuffSelf("Death Wish"),
                //Berserker rage to stay enraged
                Spell.BuffSelf("Berserker Rage", ret => !StyxWoW.Me.HasAnyAura("Enrage", "Berserker Rage", "Death Wish")),
                //Battleshout Check
                Spell.BuffSelf("Battle Shout", ret => StyxWoW.Me.RagePercent < 20)
                );
        }
    }
}
