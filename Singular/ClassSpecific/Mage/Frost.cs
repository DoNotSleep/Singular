﻿using System.Linq;
using CommonBehaviors.Actions;
using Singular.Dynamics;
using Singular.Helpers;
using Singular.Managers;
using Singular.Settings;
using Styx;

using Styx.CommonBot;
using Styx.Helpers;


using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;

namespace Singular.ClassSpecific.Mage
{
    public class Frost
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MageSettings MageSettings { get { return SingularSettings.Instance.Mage(); } }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.All, 1)]
        public static Composite CreateMageFrostPreCombatbuffs()
        {
            return new Decorator(
                ret => !Spell.IsCasting() && !Spell.IsGlobalCooldown(),
                new PrioritySelector(

                    CreateSummonWaterElemental()

                    )
                );
        }

        #region Normal Rotation
        [Behavior(BehaviorType.Pull, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.Normal)]
        public static Composite CreateMageFrostNormalPull()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Common.CreateStayAwayFromFrozenTargetsBehavior(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Helpers.Common.CreateAutoAttack(true),
                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(
                        CreateSummonWaterElemental(),
                        Spell.Cast("Frostbolt", ret => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost)),
                        Spell.Cast("Frostfire Bolt"),
                        Movement.CreateMoveToTargetBehavior(true, 35f)
                        )
                    )
                );
        }

        [Behavior(BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.Normal)]
        public static Composite CreateMageFrostNormalCombat()
        {
            return new PrioritySelector(
                 Safers.EnsureTarget(),
                 Common.CreateStayAwayFromFrozenTargetsBehavior(),
                 Movement.CreateMoveToLosBehavior(),
                 Movement.CreateFaceTargetBehavior(),
                 Helpers.Common.CreateAutoAttack(true),
                 Spell.WaitForCast(true),

                 new Decorator(
                     ret => !Spell.IsGlobalCooldown(),
                     new PrioritySelector(

                        new Decorator(
                            ret => !Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 15 && !u.IsCrowdControlled()),
                            new PrioritySelector(
                                CastFreeze( on => Me.CurrentTarget ),
                                Spell.BuffSelf(
                                "Frost Nova",
                                    ret => Unit.NearbyUnfriendlyUnits.Any(u => u.Distance <= 11 && !u.HasAura("Frost Nova") && !u.HasAura("Freeze"))
                                    )
                                )
                            ),

                        Spell.Cast("Icy Veins"),

                        new Decorator(ret => Spell.UseAOE && Me.Level >= 25 && Unit.UnfriendlyUnitsNearTarget(10).Count() > 1,
                            new PrioritySelector(
                                Spell.CastOnGround("Flamestrike", loc => Me.CurrentTarget.Location),
                                Spell.Cast("Frozen Orb"),
                                Spell.Cast("Fire Blast", ret => TalentManager.HasGlyph("Fire Blast") && Me.CurrentTarget.HasAnyAura("Frost Bomb", "Living Bomb", "Nether Tempest")),
                                Spell.Cast("Ice Lance", ret => Me.ActiveAuras.ContainsKey("Fingers of Frost")),
                                Spell.Cast("Arcane Explosion", ret => Unit.NearbyUnfriendlyUnits.Count(t => t.Distance <= 10) >= 4),
                                new Decorator(
                                    ret => Unit.UnfriendlyUnitsNearTarget(10).Count() >= 4,
                                    Movement.CreateMoveToTargetBehavior(true, 10f)
                                    )
                                )
                            ),

                        Common.CreateMagePolymorphOnAddBehavior(),

                        Spell.Cast("Frozen Orb", ret => Spell.UseAOE ),
                        Spell.Cast("Frostbolt", ret => !Me.CurrentTarget.HasAura("Frostbolt", 3) || Me.HasAura("Presence of Mind")),
                        CastFreeze(on => Clusters.GetBestUnitForCluster(Unit.UnfriendlyUnitsNearTarget(8), ClusterType.Radius, 8)),
                        Spell.Cast("Frostfire Bolt", ret => Me.HasAura("Brain Freeze")),
                        Spell.Cast("Ice Lance", ret => Me.IsMoving || Me.ActiveAuras.ContainsKey("Fingers of Frost")),
                        Spell.Cast("Frostbolt")
                        )
                    ),

                Movement.CreateMoveToTargetBehavior(true, 35f)
                );
        }

        #endregion

        #region Battleground Rotation

        [Behavior(BehaviorType.Pull|BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.Battlegrounds)]
        public static Composite CreateMageFrostPvPPullAndCombat()
        {
            return new PrioritySelector(
                 Safers.EnsureTarget(),
                 Common.CreateStayAwayFromFrozenTargetsBehavior(),
                 Movement.CreateMoveToLosBehavior(),
                 Movement.CreateFaceTargetBehavior(),
                 Helpers.Common.CreateAutoAttack(true),
                 Spell.WaitForCast(true),

                 new Decorator(
                     ret => !Spell.IsGlobalCooldown(),
                     new PrioritySelector(
                         CreateSummonWaterElemental(),

                         // Defensive stuff
                         Spell.BuffSelf("Blink", ret => MovementManager.IsClassMovementAllowed && (Me.IsStunned() || Me.IsRooted())),
                         Spell.BuffSelf("Mana Shield", ret => Me.HealthPercent <= 75),



                         Spell.BuffSelf("Frost Nova", ret => Unit.NearbyUnfriendlyUnits.Any(u => u.DistanceSqr <= 8 * 8 && !u.HasAura("Freeze") && !u.HasAura("Frost Nova") && !u.Stunned)),

                         Common.CreateUseManaGemBehavior(ret => Me.ManaPercent < 80),
                        // Cooldowns
                         Spell.BuffSelf("Evocation", ret => Me.ManaPercent < 30),
                         Spell.BuffSelf("Mirror Image"),
                         Spell.BuffSelf("Mage Ward", ret => Me.HealthPercent <= 75),
                         Spell.BuffSelf("Icy Veins"),

                         // Rotation
                         Spell.Cast("Frost Bomb", ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3),
                         Spell.Cast("Deep Freeze",
                             ret => Me.ActiveAuras.ContainsKey("Fingers of Frost") ||
                                    Me.CurrentTarget.HasAura("Freeze") ||
                                    Me.CurrentTarget.HasAura("Frost Nova")),

                         new Decorator(
                             ret => Me.ActiveAuras.ContainsKey("Brain Freeze"),
                             new PrioritySelector(
                                 Spell.Cast("Frostfire Bolt"),
                                 Spell.Cast("Fireball")
                                 )),
                         Spell.Cast("Ice Lance",
                             ret => Me.ActiveAuras.ContainsKey("Fingers of Frost") ||
                                    Me.CurrentTarget.HasAura("Freeze") ||
                                    Me.CurrentTarget.HasAura("Frost Nova") ||
                                    Me.IsMoving),
                         Spell.Cast("Frostbolt")
                         )
                    ),

                 Movement.CreateMoveToTargetBehavior(true, 39f)
                 );
        }

        #endregion

        #region Instance Rotation
        [Behavior(BehaviorType.Pull | BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageFrost, WoWContext.Instances)]
        public static Composite CreateMageFrostInstanceCombat()
        {
            return new PrioritySelector(
                Safers.EnsureTarget(),
                Movement.CreateMoveToLosBehavior(),
                Movement.CreateFaceTargetBehavior(),
                Spell.WaitForCast(true),

                new Decorator(
                    ret => !Spell.IsGlobalCooldown(),
                    new PrioritySelector(

                        Spell.Cast("Icy Veins"),

                        new Decorator(ret => Spell.UseAOE && Me.Level >= 25 && Unit.UnfriendlyUnitsNearTarget(10).Count() > 1,
                            new PrioritySelector(
                                Spell.CastOnGround( "Flamestrike", loc => Me.CurrentTarget.Location ),
                                Spell.Cast("Frozen Orb"),
                                Spell.Cast("Fire Blast", ret => TalentManager.HasGlyph("Fire Blast") && Me.CurrentTarget.HasAnyAura("Frost Bomb", "Living Bomb", "Nether Tempest")),
                                Spell.Cast("Ice Lance", ret => Me.ActiveAuras.ContainsKey("Fingers of Frost")),
                                Spell.Cast("Arcane Explosion", ret => Unit.NearbyUnfriendlyUnits.Count( t => t.Distance <= 10 ) >= 4),
                                new Decorator(
                                    ret =>  Unit.UnfriendlyUnitsNearTarget(10).Count() >= 4,
                                    Movement.CreateMoveToTargetBehavior(true, 10f)
                                    )
                                )
                            ),

                        Spell.Cast("Frozen Orb", ret => Spell.UseAOE ),
                        Spell.Cast("Frostbolt", ret => !Me.CurrentTarget.HasAura("Frostbolt", 3) || Me.HasAura("Presence of Mind")),
                        CastFreeze( on => Clusters.GetBestUnitForCluster(Unit.UnfriendlyUnitsNearTarget(8), ClusterType.Radius, 8)),
                        Spell.Cast("Frostfire Bolt", ret => Me.HasAura("Brain Freeze")),
                        Spell.Cast("Ice Lance", ret => Me.IsMoving || Me.ActiveAuras.ContainsKey("Fingers of Frost")),
                        Spell.Cast("Frostbolt")
                        )
                    ),

                Movement.CreateMoveToTargetBehavior(true, 30f)
                );
        }

        #endregion

        public static Composite CreateSummonWaterElemental()
        {
            return new PrioritySelector(
                new Decorator(
                    ret => SingularSettings.Instance.DisablePetUsage && Me.GotAlivePet,
                    new Action( ret => Lua.DoString("PetDismiss()"))
                    ),

                new Decorator(
                    ret => !SingularSettings.Instance.DisablePetUsage
                        && (!Me.GotAlivePet || Me.Pet.Distance > 40)
                        && PetManager.PetTimer.IsFinished
                        && SpellManager.CanCast("Summon Water Elemental"),
                    new Sequence(
                        new Action(ret => PetManager.CallPet("Summon Water Elemental")),
                        Helpers.Common.CreateWaitForLagDuration()
                        )
                    )
                );
        }

        
        /// <summary>
        /// Cast "Freeze" pet ability on a target.  Uses a local store for location to
        /// avoid target position changing during cast preparation and being out of
        /// range after range check
        /// </summary>
        /// <param name="onUnit">target to cast on</param>
        /// <returns></returns>
        private static Composite CastFreeze( UnitSelectionDelegate onUnit )
        {
            return new Sequence(
                new Decorator( 
                    ret => onUnit != null && onUnit(ret) != null, 
                    new Action( ret => _locFreeze = onUnit(ret).Location)
                    ),
                Pet.CreateCastPetActionOnLocation(
                    "Freeze",
                    on => _locFreeze,
                    ret => Me.Pet.ManaPercent >= 12
                        && Me.Pet.Location.Distance(_locFreeze) < 45
                        && !Me.CurrentTarget.HasAura("Frost Nova")
                    )
                );
        }

        static private WoWPoint _locFreeze;
    }
}
