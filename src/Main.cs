using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace BetterCombat
{
	[BepInPlugin(GUID, "Better Combat", "1.2.0")]
	public class Main : BaseUnityPlugin
	{
		public const string GUID = "ainsley.streetsofrogue.bettercombat";
		public void Awake()
		{
			var harmony = new Harmony(GUID);
			harmony.PatchAll();
		}

		public class GoalAbility : Goal
		{
			public GoalAbility()
			{
				this.goalName = "Ability";
			}

			public override void Process()
			{
				base.Process();
				this.agent.statusEffects.PressedSpecialAbility();
				base.SetGoalState("Completed");
			}
		}

		[HarmonyPatch(typeof(Combat), "CombatEngage")]
		static class Combat_CombatEngage_patch
		{
			static bool Prefix(Combat __instance, Agent ___agent, GameController ___gc) //pass the __result by ref to alter it.
			{
				//
				Agent combatAgent = ___agent;
				GameController combatGC = ___gc;
				Agent opponent = combatAgent.opponent;
				Relationship relationship = combatAgent.relationships.GetRelationship(opponent);
				Relationship relationship2 = opponent.relationships.GetRelationship(combatAgent);

				List<string> usableItems = new List<string>() {
					"Steroids",
					"ElectroPill",
					"CritterUpper",
					"Giantizer",
					"Cocaine",
					"FriendPhone",
					"ResurrectionShampoo",
					"FirstAidKit",
					"Banana"
				};

				if (combatAgent.statusEffects.hasTrait("Electronic"))
                {
					usableItems.Clear();
					usableItems.Add("ElectroPill");
                }

				List<string> undead = new List<string>()
				{
					"Ghost",
					"Vampire",
					"WerewolfB",
					"Zombie"
				};

				if (relationship == null)
				{
					combatAgent.SetOpponent(null);
					return false;
				} else if (relationship2 == null)
                {
					return false;
				}

				bool isUndead = undead.Contains(opponent.agentRealName);
				bool isEquippedForUndead = (combatAgent.inventory.equippedWeapon.invItemName == "GhostBlaster");

				UseUsefulItems(usableItems, combatAgent, isEquippedForUndead);

				int Panic = 0;

				float opponentDistance = Vector2.Distance(combatAgent.curPosLateUpdate, opponent.curPosLateUpdate);

				if (!ClientHasControl(__instance, relationship, combatAgent))
                {
					return false;
                }

				combatAgent.oma.combatCanSee = CanSeeData(__instance, relationship, combatAgent);

				bool flag = false;
				bool forceAttack = false;
				__instance.SetSawProjectileWeapon(opponent, relationship);
				InvItem invItem = combatAgent.inventory.equippedWeapon;
				__instance.usingSpecialAbilityWeapon = false;

				float healthPercent = combatAgent.health / combatAgent.healthMax;

				if (!combatAgent.oma.combatCanSee)
				{
					GoToLastSawPosition(__instance, relationship, combatAgent, opponent);
				}
				else
				{
					if (healthPercent < 0.5f)
					{
						if (!combatAgent.statusEffects.hasTrait("Electronic"))
						{
							// Feel fear as a human-ish being
							Panic = 20;
						}
					}

					// Smarter attacking
					bool HasCannon = combatAgent.statusEffects.hasSpecialAbility("WaterCannon");
					if (opponent.statusEffects.hasTrait("Electronic") && HasCannon)
					{
						invItem = combatAgent.inventory.equippedSpecialAbility;
						__instance.usingSpecialAbilityWeapon = true;
						
					}

					if (opponent.objectAgent)
					{
						bool flag2 = false;
						if (opponent.hasObjectAgentFire)
						{
							InvItem invItem2 = combatAgent.inventory.FindItem("FireExtinguisher");
							if (invItem2 != null)
							{
								combatAgent.inventory.EquipWeapon(invItem2);
								invItem = combatAgent.inventory.equippedWeapon;
								__instance.usingSpecialAbilityWeapon = false;
							}
							else if (combatAgent.firefighter)
							{
								invItem = combatAgent.inventory.equippedSpecialAbility;
								__instance.usingSpecialAbilityWeapon = true;
							}
						}
						else if (opponent.assignedObjectReal != null)
						{
							if (opponent.assignedObjectReal.ora.onFire)
							{
								InvItem invItem3 = combatAgent.inventory.FindItem("FireExtinguisher");
								if (invItem3 != null)
								{
									combatAgent.inventory.EquipWeapon(invItem3);
									invItem = combatAgent.inventory.equippedWeapon;
									__instance.usingSpecialAbilityWeapon = false;
									flag2 = true;
								}
								else if (combatAgent.firefighter)
								{
									invItem = combatAgent.inventory.equippedSpecialAbility;
									__instance.usingSpecialAbilityWeapon = true;
									flag2 = true;
								}
							}
							if (!flag2 && opponent.assignedObjectReal.bulletsCanPass && combatAgent.inventory.equippedWeapon.weaponCode == weaponType.WeaponProjectile)
							{
								combatAgent.inventory.ChooseWeapon(true);
								invItem = combatAgent.inventory.equippedWeapon;
								__instance.usingSpecialAbilityWeapon = false;
								__instance.choseMeleeForLowObject = true;
							}
						}
					}
					else if (combatAgent.inventory.equippedWeapon.dontSelectNPC)
					{
						combatAgent.inventory.ChooseWeapon();
						invItem = combatAgent.inventory.equippedWeapon;
						__instance.usingSpecialAbilityWeapon = false;
					}
					if (!combatGC.serverPlayer && !combatAgent.clientOutOfControl)
					{
						__instance.clientLastSawPosition = opponent.curPosLateUpdate;
					}
				}
				if (__instance.choseMeleeForLowObject)
				{
					if (!opponent.objectAgent || opponent.hasObjectAgentFire)
					{
						__instance.choseMeleeForLowObject = false;
					}
					else if (opponent.objectAgent && !opponent.assignedObjectReal.bulletsCanPass)
					{
						__instance.choseMeleeForLowObject = false;
					}
					if (!__instance.choseMeleeForLowObject)
					{
						combatAgent.inventory.ChooseWeapon();
					}
				}
				bool flag3 = false;

				if (combatAgent.inventory.equippedSpecialAbility != null && combatAgent.specialAbility == "Joke")
				{
					bool shouldJoke = false;

					foreach (Agent attemptAgent in combatAgent.gc.activeBrainAgentList)
					{
						if (attemptAgent.agentID == combatAgent.agentID)
						{
							continue;
						}
						if (attemptAgent.agentID == opponent.agentID)
						{
							continue;
						}

						if (attemptAgent.relationships.GetRelCode(combatAgent) == relStatus.Hostile)
						{
							continue;
						}

						if (attemptAgent.relationships.GetRelCode(combatAgent) == relStatus.Aligned)
						{
							continue;
						}

						float dist = Vector2.Distance(attemptAgent.curPosition, combatAgent.curPosition);

						if (dist < 6f)
						{
							shouldJoke = true;
							break;
						}
					}

					if (shouldJoke)
					{
						combatAgent.statusEffects.PressedSpecialAbility();
					}
				}

				if (combatAgent.agentName == "Comedian")
				{
					if (combatAgent.inventory.equippedSpecialAbility == null)
					{
						Debug.Log("Gave a comedian his ability!");
						combatAgent.statusEffects.GiveSpecialAbility("Joke");
					}
				}

				Item targetItem = null;

				if (invItem.weaponCode == weaponType.WeaponMelee && combatAgent.oma.combatCanSee && !combatAgent.dead)
				{
					// Parrying
					if (opponent.melee.attackAnimPlaying && combatAgent.gc.percentChance(75) && !combatAgent.melee.attackAnimPlaying)
					{
						forceAttack = true;
					}

					if (combatGC.itemList.Count > 0)
					{
						foreach (Item item in combatGC.itemList)
						{
							bool isUsable = (usableItems.Contains(item.invItem.invItemName));

							float distance = Vector2.Distance(item.curPosition, combatAgent.curPosition);

							if (distance > 3f)
                            {
								continue;
                            }

							if (isUsable)
							{
								targetItem = item;
								break;
							}
						}
					}

					if (combatGC.weaponList.Count > 0 && invItem.invItemName == "Fist" && !combatAgent.ghost && !combatAgent.zombified && combatGC.levelType != "Tutorial" && !combatAgent.dontPickUpWeapons && !combatAgent.killerRobot)
					{
						List<Item> weaponList = combatGC.weaponList;
						int j = 0;
						while (j < weaponList.Count)
						{
							Item item = weaponList[j];

							if (item == null)
							{
								continue;
							}
							float num17 = Math.Abs(Math.Max(item.curPosition.x - combatAgent.curPosition.x, item.curPosition.y - combatAgent.curPosition.y));
							bool itemIsAllowed = item.invItem.weaponCode != weaponType.None || combatAgent.challengedToFight == 0;
							bool itemThrowableAllowed = item.invItem.weaponCode != weaponType.WeaponThrown || opponent.isPlayer == 0 || (opponent.localPlayer && combatGC.serverPlayer);
							bool itemIsClose = num17 < 3f;
							bool itemUsableByMe = combatAgent.inventory.DetermineIfCanUseWeapon(item.invItem) && combatAgent.inventory.hasEmptySlot();
							if (!item.fellInHole && itemIsClose && itemUsableByMe && itemIsAllowed && itemThrowableAllowed && combatAgent.movement.HasLOSObject360(item))
							{
								targetItem = item;
								break;
							}
							else
							{
								j++;
							}
						}
					}

					bool flag4 = false;
					if ((opponent.isPlayer == 0 || opponent.outOfControl) && relationship2.relTypeCode != relStatus.Hostile && !opponent.objectAgent && !flag3 && !opponent.oma.mindControlled)
					{
						combatAgent.SetFinalDestObject(opponent);
						combatAgent.SetFinalDestPosition(opponent.curPosLateUpdate);
						if (combatAgent.pathing != 2)
						{
							combatAgent.pathing = 1;
						}
						else
						{
							relationship.SetBlockedFromPathing();
						}
						__instance.advanceRetreat = 0;
						__instance.strafe = 0;
						__instance.avoidDanger = null;
						__instance.moveAwayFromAgent = null;
						__instance.moveAwayFromInCombatAgent = null;
						__instance.hasAvoidDanger = false;
						__instance.hasMoveAwayFromAgent = false;
						__instance.hasMoveAwayFromInCombatAgent = false;
						flag4 = true;
					}
					bool flag5 = true;
					if (combatGC.levelHasGlassWall)
					{
						if (combatAgent.movement.HasShotLineGlassWall(opponent))
						{
							flag5 = false;
						}
					}
					else
					{
						flag5 = false;
					}
					if (((opponentDistance > __instance.distOffset * 3f && !__instance.inMeleeRange) || flag5) && !flag4 && !flag3)
					{
						combatAgent.SetFinalDestObject(opponent);
						combatAgent.SetFinalDestPosition(opponent.curPosLateUpdate);
						if (combatAgent.pathing != 2)
						{
							combatAgent.pathing = 1;
						}
						else
						{
							relationship.SetBlockedFromPathing();
						}
						__instance.advanceRetreat = 0;
						__instance.strafe = 0;
						__instance.avoidDanger = null;
						__instance.moveAwayFromAgent = null;
						__instance.moveAwayFromInCombatAgent = null;
						__instance.hasAvoidDanger = false;
						__instance.hasMoveAwayFromAgent = false;
						__instance.hasMoveAwayFromInCombatAgent = false;
					}
					else if (((opponentDistance > __instance.distOffset * 4f && __instance.inMeleeRange) || flag5) && !flag4 && !flag3)
					{
						__instance.inMeleeRange = false;
						combatAgent.SetFinalDestObject(opponent);
						combatAgent.SetFinalDestPosition(opponent.curPosLateUpdate);
						if (combatAgent.pathing != 2)
						{
							combatAgent.pathing = 1;
						}
						__instance.advanceRetreat = 0;
						__instance.strafe = 0;
						__instance.avoidDanger = null;
						__instance.moveAwayFromAgent = null;
						__instance.moveAwayFromInCombatAgent = null;
						__instance.hasAvoidDanger = false;
						__instance.hasMoveAwayFromAgent = false;
						__instance.hasMoveAwayFromInCombatAgent = false;
					}
					else if (((!combatAgent.movement.HasLOSCombat(opponent) && opponentDistance > __instance.distOffset) || flag5) && !flag4 && !flag3)
					{
						__instance.inMeleeRange = false;
						combatAgent.SetFinalDestObject(opponent);
						combatAgent.SetFinalDestPosition(opponent.curPosLateUpdate);
						if (combatAgent.pathing != 2)
						{
							combatAgent.pathing = 1;
						}
						else
						{
							relationship.SetBlockedFromPathing();
						}
						__instance.advanceRetreat = 0;
						__instance.strafe = 0;
						__instance.avoidDanger = null;
						__instance.moveAwayFromAgent = null;
						__instance.moveAwayFromInCombatAgent = null;
						__instance.hasAvoidDanger = false;
						__instance.hasMoveAwayFromAgent = false;
						__instance.hasMoveAwayFromInCombatAgent = false;
					}
					else if (!flag3)
					{
						if (!flag4 && !flag3)
						{
							combatAgent.movement.PathStop();
							combatAgent.pathing = 0;
							__instance.inMeleeRange = true;
							combatAgent.movement.RotateToObject(opponent.go);
						}
						__instance.hasAvoidDanger = false;
						if (combatAgent.dangersAvoid.Count > 0)
						{
							Danger danger = null;
							float num3 = 1000000f;
							List<Danger> dangersAvoid = combatAgent.dangersAvoid;
							for (int k = 0; k < dangersAvoid.Count; k++)
							{
								Danger danger2 = dangersAvoid[k];
								if (danger2.avoidInCombat)
								{
									float num4 = Vector2.Distance(danger2.tr.position, combatAgent.curPosLateUpdate);
									if (num4 < num3)
									{
										danger = danger2;
										num3 = num4;
										__instance.hasAvoidDanger = true;
									}
								}
							}
							__instance.avoidDanger = danger;
							if (__instance.hasAvoidDanger && __instance.strafe == 0)
							{
								__instance.strafe = combatGC.Choose<int>(1, 2, Array.Empty<int>());
							}
						}
						else
						{
							__instance.avoidDanger = null;
							__instance.hasAvoidDanger = false;
						}

						Agent agent = null;
						Agent agent2 = null;
						float num5 = 1000000f;
						float num6 = 1000000f;
						bool flag6 = false;
						bool flag7 = false;
						List<Agent> agentList = combatGC.agentList;
						for (int l = 0; l < agentList.Count; l++)
						{
							Agent agent3 = agentList[l];
							if (agent3.brain.active && !agent3.dead && agent3.agentID != combatAgent.agentID && agent3.agentID != opponent.agentID)
							{
								float num7 = Vector2.Distance(agent3.curPosLateUpdate, combatAgent.curPosLateUpdate);
								if (num7 < num5 && num7 < 0.96f && combatAgent.relationships.GetRelCode(agent3) != relStatus.Hostile)
								{
									agent = agent3;
									num5 = num7;
									flag6 = true;
								}
								else if (num7 < num6 && num7 < 2.4f && agent3.mostRecentGoalCode == goalType.Battle)
								{
									agent2 = agent3;
									num6 = num7;
									flag7 = true;
								}
							}
						}
						__instance.hasMoveAwayFromAgent = false;
						__instance.hasMoveAwayFromInCombatAgent = false;
						if (flag6)
						{
							__instance.moveAwayFromAgent = agent;
							__instance.hasMoveAwayFromAgent = true;
						}
						else
						{
							__instance.moveAwayFromAgent = null;
							__instance.hasMoveAwayFromAgent = false;
						}
						if (flag7)
						{
							__instance.moveAwayFromInCombatAgent = agent2;
							__instance.hasMoveAwayFromInCombatAgent = true;
						}
						else
						{
							__instance.moveAwayFromInCombatAgent = null;
							__instance.hasMoveAwayFromInCombatAgent = false;
						}
						if (__instance.hasMoveAwayFromAgent && combatAgent.opponent.mostRecentGoalCode == goalType.Flee && __instance.moveAwayFromAgent.relationships.GetRelCode(combatAgent) == relStatus.Aligned && (__instance.moveAwayFromAgent.isPlayer == 0 || __instance.moveAwayFromAgent.outOfControl))
						{
							__instance.moveAwayFromAgent = null;
							__instance.hasMoveAwayFromAgent = false;
						}
						if (__instance.hasMoveAwayFromInCombatAgent && combatAgent.opponent.mostRecentGoalCode == goalType.Flee && __instance.moveAwayFromInCombatAgent.relationships.GetRelCode(combatAgent) == relStatus.Aligned && (__instance.moveAwayFromInCombatAgent.isPlayer == 0 || __instance.moveAwayFromAgent.outOfControl))
						{
							__instance.moveAwayFromInCombatAgent = null;
							__instance.hasMoveAwayFromInCombatAgent = false;
						}
						if (flag6 && __instance.strafe == 0)
						{
							__instance.strafe = combatGC.Choose<int>(1, 2, Array.Empty<int>());
						}
						if ((opponent.inventory.equippedWeapon.weaponCode == weaponType.WeaponProjectile || opponent.inventory.equippedWeapon.weaponCode == weaponType.WeaponThrown || opponent.objectAgent) && __instance.avoidDanger == null)
						{
							if (opponentDistance < __instance.distOffset)
							{
								__instance.forwardBackChoice = 2;
							}
							else
							{
								__instance.forwardBackChoice = 1;
							}
						}
						else if (opponentDistance < __instance.distOffset)
						{
							__instance.forwardBackChoice = 2;
						}
						else if (opponentDistance >= __instance.distOffset && opponentDistance < __instance.distOffset * 3f && __instance.canChangeDirection)
						{
							if (combatAgent.modMeleeSkill == 0)
							{
								__instance.forwardBackChoice = combatGC.Choose<int>(0, 0, new int[]
								{
							0,
							0,
							1,
							1
								});
							}
							else
							{
								__instance.forwardBackChoice = combatGC.Choose<int>(1, 1, new int[]
								{
							1,
							1,
							2
								});
							}
						}
						else if (opponentDistance >= __instance.distOffset * 3f)
						{
							__instance.forwardBackChoice = 1;
						}
						if (combatAgent.weaponCooldown > 0f && opponent.inventory.equippedWeapon.weaponCode != weaponType.WeaponProjectile && opponent.inventory.equippedWeapon.weaponCode != weaponType.WeaponThrown && !opponent.objectAgent)
						{
							__instance.forwardBackChoice = 2;
						}
						if (!opponent.dead && opponent.mostRecentGoalCode == goalType.Flee)
						{
							__instance.forwardBackChoice = 1;
						}
						if (opponent.objectAgent)
						{
							if (opponentDistance < __instance.distOffset)
							{
								__instance.forwardBackChoice = 0;
							}
							else
							{
								__instance.forwardBackChoice = 1;
							}
						}
						if (__instance.forwardBackChoice == 0)
						{
							__instance.advanceRetreat = 0;
						}
						else if (__instance.forwardBackChoice == 1)
						{
							__instance.advanceRetreat = 1;
						}
						else if (__instance.forwardBackChoice == 2)
						{
							__instance.advanceRetreat = 2;
						}
						if ((opponent.inventory.equippedWeapon.weaponCode == weaponType.WeaponProjectile || opponent.inventory.equippedWeapon.weaponCode == weaponType.WeaponThrown || opponent.objectAgent) && __instance.avoidDanger == null && __instance.moveAwayFromAgent == null && __instance.moveAwayFromInCombatAgent == null)
						{
							__instance.strafe = 0;
						}
						else if (__instance.strafe > 0 && __instance.canChangeDirection)
						{
							GameController gameController = combatGC;
							int a = 1;
							int b = 1;
							int[] array = new int[2];
							array[0] = 1;
							if (gameController.Choose<int>(a, b, array) == 0)
							{
								__instance.strafe = 0;
							}
						}
						else if (__instance.canChangeDirection)
						{
							int num8;
							if (combatAgent.modMeleeSkill == 0)
							{
								num8 = 0;
							}
							else
							{
								num8 = combatGC.Choose<int>(0, 0, new int[]
								{
							0,
							0,
							1
								});
							}
							if (num8 == 1)
							{
								__instance.strafe = combatGC.Choose<int>(1, 2, Array.Empty<int>());
							}
						}
						if (!opponent.dead && opponent.mostRecentGoalCode == goalType.Flee)
						{
							__instance.strafe = 0;
						}
						__instance.CollideCheck();
						float num9 = 0f;
						if (opponent.isPlayer > 0 && !opponent.outOfControl)
						{
							num9 = __instance.distOffset;
						}
						if (opponentDistance < __instance.distOffset * 2f + num9)
						{
							bool flag8 = false;
							bool flag9 = true;
							bool flag10 = false;
							if (opponent.isPlayer == 0 && !opponent.dead && opponent.mostRecentGoalCode == goalType.Flee)
							{
								if (combatAgent.modMeleeSkill == 0)
								{
									flag = combatGC.percentChance(50 + Panic);
								}
								else if (combatAgent.modMeleeSkill == 1)
								{
									flag = combatGC.percentChance(65 + Panic);
								}
								else
								{
									flag = combatGC.percentChance(75 + Panic);
								}
								flag8 = true;
							}
							if ((opponent.inventory.equippedWeapon.weaponCode == weaponType.WeaponProjectile || opponent.inventory.equippedWeapon.weaponCode == weaponType.WeaponThrown || opponent.objectAgent) && !flag8)
							{
								if (__instance.meleeJustHitCloseCooldown <= 0f)
								{
									if (opponent.objectAgent && combatAgent.assignedAgent != null && combatAgent.assignedAgent.assignedObjectReal != null)
									{
										if (combatAgent.assignedAgent.assignedObjectReal.dangerousAttackable)
										{
											combatAgent.SetTraversable("AttackingDangerousObject");
										}
										if (combatAgent.modMeleeSkill == 0)
										{
											flag = combatGC.percentChance(40 + Panic);
										}
										else if (combatAgent.modMeleeSkill == 1)
										{
											flag = combatGC.percentChance(60 + Panic);
										}
										else
										{
											flag = combatGC.percentChance(80 + Panic);
										}
										flag8 = true;
									}
									if (!flag8)
									{
										if (combatAgent.modMeleeSkill == 0)
										{
											flag = combatGC.percentChance(20 + Panic);
										}
										else if (combatAgent.modMeleeSkill == 1)
										{
											flag = combatGC.percentChance(30 + Panic);
										}
										else
										{
											flag = combatGC.percentChance(40 + Panic);
										}
										flag8 = true;
										if (combatAgent.specialAbilityAttack && !combatAgent.zombified && ((flag && combatGC.percentChance(20)) || __instance.specialAttackTime > 0f) && !combatAgent.melee.attackAnimPlaying && (!(combatAgent.agentName == "Athlete") || combatGC.serverPlayer))
										{
											flag = true;
											flag10 = true;
										}
									}
								}
								else
								{
									flag = false;
									__instance.advanceRetreat = 0;
									flag8 = true;
								}
							}
							if (!flag8)
							{
								if (__instance.meleeJustBlockedCooldown <= 0f && __instance.meleeJustHitCooldown <= 0f)
								{
									if (combatAgent.modMeleeSkill == 0)
									{
										flag = combatGC.percentChance(20 + Panic);
									}
									else if (combatAgent.modMeleeSkill == 1)
									{
										flag = combatGC.percentChance(30 + Panic);
									}
									else
									{
										flag = combatGC.percentChance(40 + Panic);
									}
									if (combatAgent.specialAbilityAttack && !combatAgent.zombified && ((flag && combatGC.percentChance(20)) || __instance.specialAttackTime > 0f) && !combatAgent.melee.attackAnimPlaying && (!(combatAgent.agentName == "Athlete") || combatGC.serverPlayer))
									{
										flag = true;
										flag10 = true;
									}
									if (opponent.isPlayer != 0 && !opponent.localPlayer)
									{
										flag9 = false;
									}
								}
								else
								{
									flag = false;
									flag9 = false;
								}
							}
							if (opponent.hasGettingArrestedByAgent || opponent.hasGettingBitByAgent)
							{
								flag = false;
							}
							if (__instance.hasMoveAwayFromAgent && !combatAgent.DontHitAlignedCheck(__instance.moveAwayFromAgent))
							{
								__instance.moveAwayFromAgent = null;
								__instance.hasMoveAwayFromAgent = false;
							}
							if (((flag && combatAgent.stunLocked != 1 && !__instance.moveAwayFromAgent) || combatAgent.justKnocked > 0f || (opponent.isPlayer == 0 && relationship2.relTypeCode != relStatus.Hostile && !opponent.objectAgent && combatAgent.stunLocked != 1)) && flag9 && combatGC.levelType != "Tutorial")
							{
								if (flag10)
								{
									if (__instance.specialAttackTime <= 0f)
									{
										__instance.specialAttackTime = 1f;
										__instance.specialAttackTime2 = 0f;
										combatAgent.movement.RotateToAgentTr(opponent);
										__instance.chargingAngle = __instance.tr.eulerAngles.z;
										combatAgent.statusEffects.PressedSpecialAbility();
									}
								}
								else
								{
									if (__instance.specialAttackTime > 0f)
									{
										combatAgent.statusEffects.ReleasedSpecialAbility();
										__instance.specialAttackTime = 0f;
									}
									__instance.meleeStrength = 0;
									combatAgent.melee.Attack(opponent);
									__instance.AIHold = 0.1f;
								}
							}
							else if (__instance.specialAttackTime > 0f)
							{
								combatAgent.statusEffects.ReleasedSpecialAbility();
								__instance.specialAttackTime = 0f;
							}
						}
					}
				}
				if ((invItem.weaponCode == weaponType.WeaponProjectile || invItem.weaponCode == weaponType.WeaponThrown) && combatAgent.oma.combatCanSee)
				{
					bool flag11 = true;
					if (combatAgent.movement.HasShotLineGlassWall(opponent))
					{
						flag11 = false;
					}
					int num10 = 4;
					int num11 = 6;
					int num12 = 8;
					int num13 = 10;
					if (invItem.shortRangeProjectile)
					{
						num10 = 3;
						num11 = 4;
						num12 = 5;
						num13 = 7;
					}
					if ((opponentDistance > __instance.distOffset * (float)num13 && !combatAgent.oma.combatInGunRange) || flag11)
					{
						combatAgent.SetFinalDestObject(opponent);
						combatAgent.SetFinalDestPosition(opponent.curPosLateUpdate);
						if (combatAgent.pathing != 2)
						{
							combatAgent.pathing = 1;
						}
						else
						{
							relationship.SetBlockedFromPathing();
						}
						__instance.advanceRetreat = 0;
						__instance.strafe = 0;
						__instance.avoidDanger = null;
						__instance.moveAwayFromAgent = null;
						__instance.moveAwayFromInCombatAgent = null;
						__instance.hasAvoidDanger = false;
						__instance.hasMoveAwayFromAgent = false;
						__instance.hasMoveAwayFromInCombatAgent = false;
						if (!invItem.shortRangeProjectile)
						{
							__instance.ShootFar(relationship, opponentDistance);
						}
					}
					else if (opponentDistance > __instance.distOffset * (float)num12 && combatAgent.oma.combatInGunRange)
					{
						combatAgent.oma.combatInGunRange = false;
						combatAgent.SetFinalDestObject(opponent);
						combatAgent.SetFinalDestPosition(opponent.curPosLateUpdate);
						if (combatAgent.pathing != 2)
						{
							combatAgent.pathing = 1;
						}
						__instance.advanceRetreat = 0;
						__instance.strafe = 0;
						__instance.avoidDanger = null;
						__instance.moveAwayFromAgent = null;
						__instance.moveAwayFromInCombatAgent = null;
						__instance.hasAvoidDanger = false;
						__instance.hasMoveAwayFromAgent = false;
						__instance.hasMoveAwayFromInCombatAgent = false;
						if (!invItem.shortRangeProjectile)
						{
							__instance.ShootFar(relationship, opponentDistance);
						}
					}
					else
					{
						combatAgent.movement.PathStop();
						combatAgent.pathing = 0;
						combatAgent.oma.combatInGunRange = true;
						combatAgent.movement.RotateToObject(opponent.go);
						__instance.hasAvoidDanger = false;
						if (combatAgent.dangersAvoid.Count > 0)
						{
							bool flag12 = false;
							Danger danger3 = null;
							float num14 = 1000000f;
							List<Danger> dangersAvoid2 = combatAgent.dangersAvoid;
							for (int m = 0; m < dangersAvoid2.Count; m++)
							{
								Danger danger4 = dangersAvoid2[m];
								if (danger4.avoidInCombat)
								{
									float num15 = Vector2.Distance(danger4.tr.position, combatAgent.curPosLateUpdate);
									if (num15 < num14)
									{
										danger3 = danger4;
										num14 = num15;
										flag12 = true;
									}
								}
							}
							__instance.avoidDanger = danger3;
							if (flag12 && __instance.strafe == 0)
							{
								__instance.strafe = combatGC.Choose<int>(1, 2, Array.Empty<int>());
							}
						}
						else
						{
							__instance.avoidDanger = null;
							__instance.hasAvoidDanger = false;
						}
						if (opponentDistance >= __instance.distOffset * (float)num11)
						{
							__instance.forwardBackChoice = 1;
						}
						else if (opponentDistance >= __instance.distOffset * (float)num10 && opponentDistance < __instance.distOffset * (float)num11 && __instance.canChangeDirection)
						{
							if (combatAgent.modGunSkill == 0)
							{
								__instance.forwardBackChoice = 0;
							}
							else if (combatAgent.modGunSkill == 1)
							{
								__instance.forwardBackChoice = combatGC.Choose<int>(0, 0, new int[]
								{
									2,
									2,
									1
								});
							}
							else
							{
								__instance.forwardBackChoice = combatGC.Choose<int>(2, 2, new int[]
								{
									2,
									1
								});
							}
							if (opponent.objectAgent)
							{
								__instance.forwardBackChoice = 0;
							}
						}
						else if (opponentDistance < __instance.distOffset * (float)num10)
						{
							if (combatAgent.modGunSkill == 0)
							{
								__instance.forwardBackChoice = 0;
							}
							else if (combatAgent.modGunSkill == 1)
							{
								__instance.forwardBackChoice = combatGC.Choose<int>(2, 2, new int[2]);
							}
							else
							{
								__instance.forwardBackChoice = 2;
							}
							if (opponent.objectAgent)
							{
								__instance.forwardBackChoice = 0;
							}
						}
						if (__instance.forwardBackChoice == 0)
						{
							__instance.advanceRetreat = 0;
						}
						else if (__instance.forwardBackChoice == 1)
						{
							__instance.advanceRetreat = 1;
						}
						else if (__instance.forwardBackChoice == 2)
						{
							__instance.advanceRetreat = 2;
						}
						bool flag13 = false;
						if (combatAgent.weaponCooldown <= 0f && __instance.personalCooldown <= 0f && __instance.rapidFireTime <= 0f)
						{
							if (invItem.rapidFire)
							{
								flag = combatGC.percentChance((int)(25f * __instance.gunPercentMod));
							}
							else
							{
								float shootChance = ((25f * __instance.gunPercentMod) + (combatAgent.modGunSkill * 25f));
								if (shootChance > 100f)
								{
									shootChance = 100f;
								}
								flag = combatGC.percentChance((int)shootChance);
							}
							if (opponent.hasGettingArrestedByAgent || opponent.hasGettingBitByAgent)
							{
								flag = false;
							}
							if ((flag || forceAttack) && combatAgent.stunLocked != 1)
							{
								PlayfieldObject playfieldObject = opponent;
								if (opponent.objectAgent && !opponent.hasObjectAgentFire)
								{
									playfieldObject = opponent.assignedObjectReal;
								}
								if (combatAgent.movement.HasShotLine(playfieldObject) || playfieldObject.hasObjectAgentFire)
								{
									if (invItem.rapidFire)
									{
										__instance.DoRapidFire();
									}
									else
									{
										combatAgent.gun.CheckAttack(false);
									}
									__instance.SetPersonalCooldown();
								}
								else
								{
									flag13 = true;
									if (__instance.strafe == 0)
									{
										__instance.strafe = combatGC.Choose<int>(1, 2, Array.Empty<int>());
									}
								}
							}
						}
						if (!flag13)
						{
							if (__instance.strafe > 0 && __instance.canChangeDirection)
							{
								GameController gameController2 = combatGC;
								int a2 = 1;
								int b2 = 1;
								int[] array2 = new int[2];
								array2[0] = 1;
								if (gameController2.Choose<int>(a2, b2, array2) == 0)
								{
									__instance.strafe = 0;
								}
							}
							else if (__instance.canChangeDirection)
							{
								int num16;
								if (combatAgent.modGunSkill == 0)
								{
									num16 = 0;
								}
								else if (combatAgent.modGunSkill == 1)
								{
									num16 = 0;
								}
								else
								{
									num16 = combatGC.Choose<int>(0, 0, new int[]
									{
										0,
										1
									});
								}
								if (num16 == 1)
								{
									__instance.strafe = combatGC.Choose<int>(1, 2, Array.Empty<int>());
								}
							}
							__instance.CollideCheck();
						}
					}
				}

				if (combatGC.itemList.Count > 0)
				{
					if (isUndead && !isEquippedForUndead)
                    {
						foreach (Item item in combatGC.itemList)
						{
							bool isUsable = (item.invItem.invItemName == "GhostBlaster");

							float distance = Vector2.Distance(item.curPosition, combatAgent.curPosition);

							if (distance > 3f)
							{
								continue;
							}

							if (isUsable)
							{
								targetItem = item;
								break;
							}
						}
					}
				}

				if (targetItem)
				{
					flag3 = true;
					combatAgent.SetFinalDestObject(targetItem);
					combatAgent.SetFinalDestPosition(targetItem.curPosition);
					if (combatAgent.pathing != 2)
					{
						combatAgent.pathing = 1;
					}
					__instance.advanceRetreat = 0;
					__instance.strafe = 0;
					__instance.avoidDanger = null;
					__instance.moveAwayFromAgent = null;
					__instance.moveAwayFromInCombatAgent = null;
					__instance.hasAvoidDanger = false;
					__instance.hasMoveAwayFromAgent = false;
					__instance.hasMoveAwayFromInCombatAgent = false;
					if (Vector2.Distance(combatAgent.curPosition, targetItem.curPosition) >= 0.64f)
					{
						goto AfterItems;
					}
					if (combatGC.serverPlayer)
					{
						targetItem.Interact(combatAgent);
						goto AfterItems;
					}
					combatGC.playerAgent.objectMult.CallCmdNPCPickUp(combatAgent.objectNetID, targetItem.objectNetID);
				}

				AfterItems:
				if (!__instance.canChangeDirection)
				{
					__instance.canChangeDirection = true;
					return false;
				}
				__instance.canChangeDirection = false;
				return false; //return false to skip execution of the original.
			}

            private static void GoToLastSawPosition(Combat combat, Relationship relationship, Agent combatAgent, Agent opponent)
            {
				GameController combatGC = combatAgent.gc;
				if (combatGC.serverPlayer || combatAgent.clientOutOfControl)
				{
					combatAgent.SetFinalDestPosition(relationship.lastSawPosition);
				}
				else
				{
					combatAgent.SetFinalDestPosition(combat.clientLastSawPosition);
				}

				combatAgent.SetFinalDestObject(null);

				if (combatAgent.pathing != 2)
				{
					combatAgent.pathing = 1;
				}

				combat.advanceRetreat = 0;
				combat.strafe = 0;
				combat.avoidDanger = null;
				combat.moveAwayFromAgent = null;
				combat.moveAwayFromInCombatAgent = null;
				combat.hasAvoidDanger = false;
				combat.hasMoveAwayFromAgent = false;
				combat.hasMoveAwayFromInCombatAgent = false;

				if (opponent.isPlayer <= 0 || opponent.outOfControl)
				{
					return;
				}

				List<ObjectReal> objectRealList = combatGC.objectRealList;
				for (int i = 0; i < objectRealList.Count; i++)
				{
					ObjectReal objectReal = objectRealList[i];
					if (objectReal.objectName != "CornerCombatHelper" || combatAgent.zombified || (double)Vector2.Distance(objectReal.tr.position, opponent.curPosLateUpdate) >= 1.28 || opponent.invisible)
					{
						continue;
					}

					float num2 = Vector2.Distance(objectReal.tr.position, combatAgent.curPosLateUpdate);
					if ((double)num2 < 0.96)
					{
						combatAgent.pathing = 0;
						combat.advanceRetreat = 2;
						continue;
					}
					
					if (num2 < 1.28f)
					{
						combatAgent.pathing = 0;
					}
				}
			}

            private static bool CanSeeData(Combat combat, Relationship relationship, Agent combatAgent)
            {
				GameController combatGC = combatAgent.gc;
				bool combatCanSee = combatAgent.oma.combatCanSee;

				if (combatGC.serverPlayer || combatAgent.clientOutOfControl)
				{
					if (relationship.HasLOS("") && !combatCanSee)
					{
						return true;
					}
					
					if (combatCanSee)
					{
						return false;
					}

					return combatCanSee;
				}
				
				if (combat.clientCanSee)
				{
					if (!combatCanSee)
					{
						return true;
					}

					return combatCanSee;
				}
				
				if (combatCanSee)
				{
					return false;
				}

				return combatCanSee;
			}

            private static bool ClientHasControl(Combat combat, Relationship relationship, Agent combatAgent)
            {
				GameController combatGC = combatAgent.gc;

				if (combatGC.serverPlayer && combatAgent.objectMultPlayfield.clientHasControl && !combatAgent.clientOutOfControl)
				{
					combatAgent.movement.PathStop();
					combatAgent.pathing = 0;
					combat.inMeleeRange = true;
					if (relationship.HasLOS(""))
					{
						combatAgent.movement.RotateToObject(combatAgent.opponent.go);
					}
					return false;
				}

				return true;
			}

            private static void UseUsefulItems(List<string> usableItems, Agent combatAgent, bool isEquippedForUndead)
            {
				foreach (InvItem savedItem in combatAgent.inventory.InvItemList)
				{
					if (savedItem == null)
					{
						continue;
					}

					bool isUsable = (usableItems.Contains(savedItem.invItemName));

					if (!isEquippedForUndead && savedItem.invItemName == "GhostBlaster" && (combatAgent.inventory.equippedWeapon.invItemCount <= 0))
					{
						isEquippedForUndead = true;
						combatAgent.inventory.equippedWeapon = savedItem;
						continue;
					}

					if (isUsable)
					{
						int cowardness = combatAgent.opponent.relationships.FindThreat(combatAgent, true);
						int useChance = Mathf.Clamp(cowardness - 25, 0, 40);

						float healthPercent = combatAgent.health / combatAgent.healthMax;

						// WEAK
						if (healthPercent < 0.5f)
						{
							if (!combatAgent.statusEffects.hasTrait("Electronic"))
							{
								// PREPARE THYSELF
								useChance += 10;
							}
						}

						if (combatAgent.gc.percentChance(useChance))
                        {
							savedItem.UseItem();
						}

						break;
					}
				}
			}
        }

		[HarmonyPatch(typeof(PlayfieldObject), "FindDamage", new Type[] { typeof(PlayfieldObject), typeof(bool), typeof(bool), typeof(bool) })]
		static class PlayfieldObject_FindDamage_patch
        {
			static bool Prefix(PlayfieldObject damagerObject, bool generic, bool testOnly, bool fromClient, PlayfieldObject __instance, ref int __result)
			{
				PlayfieldObject myself = __instance;
				Agent agent = null;
				ObjectReal objectReal = null;
				bool flag = false;
				bool flag2 = false;
				bool flag3 = false;
				bool flag4 = true;
				if (myself.isAgent && !generic)
				{
					agent = (Agent)myself;
					flag = true;
				}
				else if (myself.isObjectReal && !generic)
				{
					objectReal = (ObjectReal)myself;
					flag3 = true;
				}
				Agent agent2 = null;
				float outputDamage = 0f;
				string a = "";
				bool flag5 = false;
				Item item = null;
				bool flag6 = true;
				bool flag7 = false;
				bool flag8 = false;
				bool flag9 = false;
				if (damagerObject.isAgent)
				{
					agent2 = damagerObject.GetComponent<Agent>();
					flag2 = true;
					if (agent2.statusEffects.hasStatusEffect("Giant"))
					{
						outputDamage = 30f;
					}
					else if (agent2.statusEffects.hasStatusEffect("ElectroTouch"))
					{
						outputDamage = 15f;
						if (flag)
						{
							if (agent.underWater || myself.gc.tileInfo.GetTileData(agent.tr.position).spillWater)
							{
								if (agent.underWater)
								{
									outputDamage *= 3f;
								}
								else
								{
									outputDamage *= 1.5f;
								}
								if (agent2.localPlayer && agent2.isPlayer != 0)
								{
									myself.gc.unlocks.DoUnlockEarly("ElectrocuteInWater", "Extra");
								}
							}
							else if (agent.underWater)
							{
								outputDamage *= 3f;
								if (agent2.localPlayer && agent2.isPlayer != 0)
								{
									myself.gc.unlocks.DoUnlockEarly("ElectrocuteInWater", "Extra");
								}
							}
							if (!agent.dead && !testOnly)
							{
								agent.deathMethod = "ElectroTouch";
								agent.deathKiller = agent2.agentName;
							}
						}
					}
					else if (agent2.chargingForward)
					{
						if (flag)
						{
							if (!agent2.oma.superSpecialAbility && !agent2.statusEffects.hasTrait("ChargeMorePowerful"))
							{
								outputDamage = 10f;
							}
							else
							{
								outputDamage = 20f;
							}
							if (!agent.dead && !testOnly)
							{
								agent.deathMethod = "Charge";
								agent.deathKiller = agent2.agentName;
							}
						}
						else
						{
							outputDamage = 30f;
						}
					}
					else if (agent2 == agent && agent.Tripped())
					{
						outputDamage = 5f;
					}
					else
					{
						outputDamage = 30f;
					}
					if (flag && agent.shrunk && !agent2.shrunk)
					{
						outputDamage = 200f;
						if (!agent.dead && !testOnly)
						{
							agent.deathMethod = "Stomping";
							agent.deathKiller = agent2.agentName;
						}
					}
					a = "TouchDamage";
				}
				else if (damagerObject.isBullet)
				{
					Bullet component = damagerObject.GetComponent<Bullet>();
					agent2 = component.agent;
					if (component.agent != null)
					{
						flag2 = true;
						if (flag && component.agent.objectAgent && component.bulletType == bulletStatus.Fire && agent.knockedByObject != null && agent.bouncy && agent.knockedByObject.playfieldObjectType == "Agent" && agent.lastHitByAgent != null)
						{
							agent2 = agent.lastHitByAgent;
						}
					}
					outputDamage = (float)component.damage;
					a = "Bullet";
					if (component.bulletType == bulletStatus.Fire || component.bulletType == bulletStatus.Fireball)
					{
						a = "Fire";
					}
					if (component.bulletType == bulletStatus.Shotgun && (myself.tickEndObject == null || myself.tickEndObject.bulletType == bulletStatus.Shotgun))
					{
						flag5 = true;
					}
					if (component.bulletType == bulletStatus.GhostBlaster)
					{
						flag7 = true;
					}
					if (flag)
					{
						if (flag2)
						{
							if (!agent2.objectAgent)
							{
								float num2 = (float)agent2.accuracyStatMod;
								num2 += (float)component.moreAccuracy;
								outputDamage *= 0.6f + num2 / 5f;
								float x = agent2.agentSpriteTransform.localScale.x;
								if (x <= 0.65f || x >= 0.67f)
								{
									outputDamage *= x;
								}
								if (!agent.dead && !testOnly)
								{
									agent.deathMethodItem = component.cameFromWeapon;
									agent.deathMethodObject = component.cameFromWeapon;
									agent.deathMethod = component.cameFromWeapon;
									if (!agent2.objectAgent)
									{
										agent.deathKiller = agent2.agentName;
									}
								}
							}
							else if (!agent.dead && !testOnly)
							{
								agent.deathMethodItem = component.cameFromWeapon;
								agent.deathMethodObject = component.cameFromWeapon;
								agent.deathMethod = component.cameFromWeapon;
								agent.deathKiller = "Nature";
							}
						}
						else if (!agent.dead && !testOnly)
						{
							if (component.bulletType == bulletStatus.Water || component.bulletType == bulletStatus.Water2)
							{
								agent.deathMethodItem = component.cameFromWeapon;
								agent.deathMethodObject = component.cameFromWeapon;
								agent.deathMethod = component.cameFromWeapon;
								agent.deathKiller = "Nature";
							}
							else
							{
								agent.deathMethodItem = component.cameFromWeapon;
								agent.deathMethodObject = damagerObject.objectName;
								agent.deathMethod = damagerObject.objectName;
								agent.deathKiller = "Nature";
							}
						}
					}
				}
				else if (damagerObject.isMelee)
				{
					Melee melee = damagerObject.playfieldObjectMelee;
					agent2 = melee.agent;
					flag2 = true;
					InvItem invItem;
					if (melee.invItem.weaponCode != weaponType.WeaponMelee)
					{
						invItem = agent2.inventory.fist;
					}
					else
					{
						invItem = melee.invItem;
					}
					outputDamage = (float)invItem.meleeDamage;
					outputDamage *= 1f + (float)agent2.strengthStatMod / 3f;
					float x2 = agent2.agentSpriteTransform.localScale.x;
					outputDamage *= x2;

					List<string> piercers = new List<string>()
					{
						"Axe",
						"Knife",
						"Sword",
						"Lunge",
						"WerewolfLunge"
					};

					bool isPiercer = piercers.Contains(invItem.invItemName);
					float effectiveBonus = 0f;

					if (myself.isAgent)
                    {
						Agent myAgent = (Agent)myself;
						
						bool robotic = myAgent.statusEffects.hasTrait("Electronic");
						bool isWrench = (invItem.invItemName == "Wrench");

						if (robotic && isWrench)
                        {
							effectiveBonus = 4f;
                        }
					}

					bool isCrowbar = (invItem.invItemName == "Crowbar");
					bool isDoor = (myself.objectName == "Door" || myself.objectName == "DoorLocked" || myself.objectName == "DoorNoEntry");

					if (isCrowbar && isDoor)
                    {

						effectiveBonus = 10f;
						agent2.inventory.DepleteMelee(200);
                    }

					if (effectiveBonus > 0)
                    {
						outputDamage += effectiveBonus;
                    }

					if (!isPiercer)
					{
						float nerf = Mathf.Clamp(2 * (melee.meleeHitbox.objectList.Count - 4), 0, float.PositiveInfinity);
						outputDamage = Mathf.Clamp(outputDamage - nerf, 2, float.PositiveInfinity);
					}

					a = "Melee";
					if (flag2 && flag)
					{
						if (!agent.dead && !testOnly)
						{
							agent.deathMethodItem = invItem.invItemName;
							agent.deathMethodObject = invItem.invItemName;
							agent.deathMethod = invItem.invItemName;
							agent.deathKiller = agent2.agentName;
						}
					}
					else if (flag && !agent.dead && !testOnly)
					{
						agent.deathMethodItem = invItem.invItemName;
						agent.deathMethodObject = invItem.invItemName;
						agent.deathMethod = invItem.invItemName;
						agent.deathKiller = "Nature";
					}
				}
				else if (damagerObject.isExplosion)
				{
					Explosion explosion = damagerObject.playfieldObjectExplosion;
					agent2 = explosion.agent;
					if (explosion.agent != null)
					{
						flag2 = true;
						if (flag)
						{
							bool reason2 = (explosion.sourceObject != null && explosion.sourceObject.isBullet && explosion.sourceObject.playfieldObjectBullet.cameFromWeapon == "Fireworks" && (!agent.movement.HasLOSAgent360(explosion.agent) || Vector2.Distance(agent.curPosition, explosion.agent.curPosition) > explosion.agent.LOSRange / agent.hardToSeeFromDistance));
							bool reason1 = (explosion.realSource != null && explosion.realSource.isItem && (!agent.movement.HasLOSAgent360(explosion.agent) || Vector2.Distance(agent.curPosition, explosion.agent.curPosition) > explosion.agent.LOSRange / agent.hardToSeeFromDistance));
							if (reason1 || reason2)
							{
								flag4 = false;
							}
						}
					}
					outputDamage = (float)explosion.damage;
					a = "Explosion";
					if (flag2 && flag)
					{
						if (!agent.dead && !testOnly)
						{
							agent.deathMethod = "Explosion";
							if (agent2 != agent && !agent2.objectAgent)
							{
								agent.deathKiller = agent2.agentName;
							}
							else
							{
								agent.deathKiller = "Self";
							}
						}
					}
					else if (flag && !agent.dead && !testOnly)
					{
						agent.deathMethod = "Explosion";
						agent.deathKiller = "Nature";
					}
				}
				else if (damagerObject.isFire)
				{
					Fire fire = damagerObject.playfieldObjectFire;
					agent2 = fire.agent;
					if (fire.agent != null)
					{
						flag2 = true;
						if (flag && (!agent.movement.HasLOSAgent360(fire.agent) || Vector2.Distance(agent.curPosition, fire.agent.curPosition) > fire.agent.LOSRange / agent.hardToSeeFromDistance))
						{
							flag4 = false;
						}
					}
					outputDamage = (float)fire.damage;
					a = "Fire";
					if (flag)
					{
						if (flag2)
						{
							if (!agent.dead && !testOnly)
							{
								agent.deathMethod = "Fire";
								if (!agent2.objectAgent)
								{
									agent.deathKiller = agent2.agentName;
								}
							}
						}
						else if (!agent.dead && !testOnly)
						{
							agent.deathMethod = "Fire";
							agent.deathKiller = "Nature";
						}
					}
				}
				else if (damagerObject.isObjectReal)
				{
					ObjectReal objectReal2 = damagerObject.playfieldObjectReal;
					outputDamage = (float)objectReal2.hazardDamage;
					a = "Hazard";
					if (flag && agent.knockedByObject != null && agent.bouncy && agent.knockedByObject.playfieldObjectType == "Agent" && agent.lastHitByAgent != null)
					{
						agent2 = agent.lastHitByAgent;
						flag2 = true;
					}
					if (flag2 && flag)
					{
						if (!agent.dead && !testOnly)
						{
							agent.deathMethodItem = objectReal2.objectName;
							agent.deathMethodObject = objectReal2.objectName;
							agent.deathMethod = objectReal2.objectName;
							if (!agent2.objectAgent)
							{
								agent.deathKiller = agent2.agentName;
							}
						}
					}
					else if (flag)
					{
						if (!agent.dead && !testOnly)
						{
							agent.deathMethodItem = objectReal2.objectName;
							agent.deathMethodObject = objectReal2.objectName;
							agent.deathMethod = objectReal2.objectName;
							agent.deathKiller = "Nature";
						}
					}
					else if (flag3)
					{
						outputDamage = 30f;
					}
				}
				else if (damagerObject.isItem)
				{
					item = damagerObject.playfieldObjectItem;
					if (item.invItem.otherDamage > 0 && item.otherDamageMode)
					{
						if (item.hitCauser != null)
						{
							agent2 = item.hitCauser;
							flag2 = true;
						}
						else if (item.owner != null)
						{
							agent2 = item.owner;
							flag2 = true;
							if (flag && (!agent.movement.HasLOSAgent360(item.owner) || Vector2.Distance(agent.curPosition, item.owner.curPosition) > item.owner.LOSRange / agent.hardToSeeFromDistance))
							{
								flag4 = false;
							}
						}
						outputDamage = (float)item.invItem.otherDamage;
					}
					else if (item.invItem.touchDamage > 0 && myself.playfieldObjectType == "Agent")
					{
						if (item.hitCauser != null)
						{
							agent2 = item.hitCauser;
							flag2 = true;
						}
						else if (item.owner != null)
						{
							agent2 = item.owner;
							flag2 = true;
							if (flag && (!agent.movement.HasLOSAgent360(item.owner) || Vector2.Distance(agent.curPosition, item.owner.curPosition) > item.owner.LOSRange / agent.hardToSeeFromDistance))
							{
								flag4 = false;
							}
						}
						if (item.invItem.touchDamage > 0)
						{
							outputDamage = (float)item.invItem.touchDamage;
						}
						else if (item.invItem.otherDamage > 0)
						{
							outputDamage = (float)item.invItem.otherDamage;
						}
						if (item.thrower != null)
						{
							a = "Throw";
						}
					}
					else
					{
						if (item.thrower != null && item.invItem.throwDamage != 0)
						{
							agent2 = item.thrower;
							flag2 = true;
						}
						outputDamage = (float)item.invItem.throwDamage;
						if (flag2 && item.invItem.invItemName == "TossItem" && (agent2.oma.superSpecialAbility || agent2.statusEffects.hasTrait("GoodThrower")))
						{
							outputDamage *= 2f;
						}
						a = "Throw";
					}
					if (!flag2 && item.thrower != null && item.thrower.statusEffects.hasTrait("KillerThrower"))
					{
						agent2 = item.thrower;
						flag2 = true;
						a = "Throw";
					}
					if (flag2 && flag)
					{
						if (!agent.dead && !testOnly)
						{
							agent.deathMethodItem = item.invItem.invItemName;
							agent.deathMethodObject = item.invItem.invItemName;
							agent.deathMethod = item.invItem.invItemName;
							if (!agent2.objectAgent)
							{
								agent.deathKiller = agent2.agentName;
							}
						}
					}
					else if (flag && !agent.dead && !testOnly)
					{
						agent.deathMethodItem = item.invItem.invItemName;
						agent.deathMethodObject = item.invItem.invItemName;
						agent.deathMethod = item.invItem.invItemName;
						agent.deathKiller = "Nature";
					}
				}
				bool flag10 = false;
				if (flag2)
				{
					if (agent2.isPlayer != 0 && !agent2.localPlayer)
					{
						flag10 = true;
					}
					if (flag && agent.isPlayer != 0 && agent2.isPlayer != 0 && !myself.gc.pvp)
					{
						flag6 = false;
					}
				}
				if (a == "Melee")
				{
					if (agent2.statusEffects.hasTrait("Strength"))
					{
						outputDamage *= 1.5f;
					}
					if (agent2.statusEffects.hasTrait("StrengthSmall"))
					{
						outputDamage *= 1.25f;
					}
					if (agent2.statusEffects.hasTrait("Weak"))
					{
						outputDamage *= 0.5f;
					}
					if (agent2.statusEffects.hasTrait("Withdrawal"))
					{
						outputDamage *= 0.75f;
					}
					if (agent2.melee.specialLunge)
					{
						if (agent2.agentName == "WerewolfB")
						{
							outputDamage *= 1.3f;
						}
						else
						{
							outputDamage *= 2f;
						}
					}
					if (agent2.inventory.equippedWeapon.invItemName == "Fist" || agent2.inventory.equippedWeapon.itemType == "WeaponProjectile")
					{
						if (agent2.statusEffects.hasTrait("StrongFists2"))
						{
							outputDamage *= 1.8f;
						}
						else if (agent2.statusEffects.hasTrait("StrongFists"))
						{
							outputDamage *= 1.4f;
						}
						if (agent2.statusEffects.hasTrait("CantAttack") && myself.isAgent)
						{
							outputDamage = 0f;
							flag8 = true;
						}
						else if (agent2.statusEffects.hasTrait("AttacksOneDamage") && myself.isAgent)
						{
							outputDamage = 1f;
							flag9 = true;
						}
					}
					if (!flag10 && flag6)
					{
						if (agent2.inventory.equippedArmor != null && !testOnly && (agent2.inventory.equippedArmor.armorDepletionType == "MeleeAttack" && flag) && !agent.dead && !agent.mechEmpty && !agent.butlerBot)
						{
							agent2.inventory.DepleteArmor("Normal", Mathf.Clamp((int)(outputDamage / 2f), 0, 12));
						}
						if (agent2.inventory.equippedArmorHead != null && !testOnly && (agent2.inventory.equippedArmorHead.armorDepletionType == "MeleeAttack" && flag) && !agent.dead && !agent.mechEmpty && !agent.butlerBot)
						{
							agent2.inventory.DepleteArmor("Head", Mathf.Clamp((int)(outputDamage / 2f), 0, 12));
						}
					}
					if (flag)
					{
						float num3 = outputDamage / agent2.agentSpriteTransform.localScale.x;
						if (!agent.dead && !testOnly && !flag10 && flag6 && !agent.butlerBot && !agent.mechEmpty)
						{
							agent2.inventory.DepleteMelee(Mathf.Clamp((int)num3, 0, 15), damagerObject.playfieldObjectMelee.invItem);
						}
						if ((agent2.statusEffects.hasTrait("SleepKiller") || agent2.statusEffects.hasTrait("Backstabber")) && agent.sleeping)
						{
							outputDamage = 200f;
							agent.agentHitboxScript.wholeBodyMode = 0;
							agent2.melee.successfullySleepKilled = true;
							if (agent2.statusEffects.hasTrait("Backstabber"))
							{
								agent.statusEffects.CreateBuffText("Backstab", agent.objectNetID);
							}
						}
						else if ((agent2.melee.mustDoBackstab && outputDamage != 200f && !agent.dead) || (agent2.statusEffects.hasTrait("Backstabber") && ((agent.mostRecentGoalCode != goalType.Battle && agent.mostRecentGoalCode != goalType.Flee) || agent.frozen) && !agent.movement.HasLOSObjectBehind(agent2) && !agent.sleeping && outputDamage != 200f && !agent.dead))
						{
							agent.agentHelperTr.localPosition = new Vector3(-0.64f, 0f, 0f);
							if (!myself.gc.tileInfo.IsOverlapping(agent.agentHelperTr.position, "Wall"))
							{
								agent.agentHelperTr.localPosition = Vector3.zero;
								agent.statusEffects.CreateBuffText("Backstab", agent.objectNetID);
								if (agent2.statusEffects.hasStatusEffect("InvisibleLimited") || (agent2.statusEffects.hasStatusEffect("Invisible") && agent2.statusEffects.hasSpecialAbility("InvisibleLimitedItem")))
								{
									outputDamage *= 10f;
									agent2.melee.successfullyBackstabbed = true;
									myself.gc.OwnCheck(agent2, agent.go, "Normal", 0);
								}
								else
								{
									outputDamage *= 2f;
								}
							}
						}
						else if (agent2.statusEffects.hasStatusEffect("InvisibleLimited"))
						{
							bool flag11 = false;
							if (flag && agent.dead)
							{
								flag11 = true;
							}
							if (!flag10 && !flag11 && !agent2.oma.superSpecialAbility && !agent2.statusEffects.hasTrait("FailedAttacksDontEndCamouflage"))
							{
								agent2.statusEffects.RemoveInvisibleLimited();
							}
						}
					}
				}
				else if (a == "Bullet")
				{
					if (flag && !flag7)
					{
						if (agent.statusEffects.hasTrait("ResistBullets"))
						{
							outputDamage /= 1.5f;
						}
						if (agent.statusEffects.hasTrait("ResistBulletsSmall"))
						{
							outputDamage /= 1.2f;
						}
						if (agent.statusEffects.hasTrait("ResistBulletsTrait2"))
						{
							outputDamage /= 2f;
						}
						else if (agent.statusEffects.hasTrait("ResistBulletsTrait"))
						{
							outputDamage /= 1.5f;
						}
					}
				}
				else if (a == "Fire")
				{
					if (flag)
					{
						if (agent.statusEffects.hasTrait("ResistFire"))
						{
							outputDamage /= 1.5f;
						}
						if ((agent.oma.superSpecialAbility && agent.agentName == "Firefighter") || agent.statusEffects.hasTrait("FireproofSkin2"))
						{
							outputDamage = 0f;
							flag8 = true;
						}
						else if (agent.statusEffects.hasTrait("FireproofSkin"))
						{
							outputDamage /= 1.5f;
						}
					}
				}
				else if (a == "Throw")
				{
					if (flag2)
					{
						if (agent2.statusEffects.hasTrait("GoodThrower"))
						{
							outputDamage *= 2f;
						}
						if (flag && agent2.statusEffects.hasTrait("KillerThrower") && item.throwerReal == item.thrower)
						{
							if (agent != item.thrower)
							{
								if (agent.health >= 100f)
								{
									outputDamage = 100f;
								}
								else
								{
									outputDamage = 200f;
								}
							}
							else
							{
								outputDamage = 20f;
							}
						}
					}
				}
				else if (!(a == "Explosion"))
				{
					a = "Hazard";
				}
				if (flag2 && flag && !testOnly)
				{
					if (agent2.statusEffects.hasTrait("BloodyMess"))
					{
						agent.bloodyMessed = true;
					}
					if ((agent2.invisible && !agent2.oma.hidden) || agent2.ghost)
					{
						agent2.gc.spawnerMain.SpawnDanger(agent2, "Targeted", "Spooked", agent);
						relStatus relCode = agent2.relationships.GetRelCode(agent);
						if (relCode != relStatus.Aligned && relCode != relStatus.Loyal)
						{
							List<Agent> agentList = myself.gc.agentList;
							for (int i = 0; i < agentList.Count; i++)
							{
								Agent agent3 = agentList[i];
								if (agent3.employer == agent2)
								{
									relStatus relCode2 = agent3.relationships.GetRelCode(agent);
									if (relCode2 != relStatus.Aligned && relCode2 != relStatus.Loyal)
									{
										agent3.relationships.SetRelHate(agent, 5);
									}
									else if (relCode2 == relStatus.Aligned && agent3.relationships.GetRelCode(agent2) == relStatus.Loyal)
									{
										agent3.relationships.SetRelHate(agent2, 5);
										agent2.agentInteractions.LetGo(agent3, agent2);
									}
								}
							}
						}
					}
				}
				if (flag)
				{
					if (agent.statusEffects.hasTrait("NumbToPain"))
					{
						outputDamage /= 3f;
					}
					if (agent.statusEffects.hasTrait("ResistDamageSmall"))
					{
						outputDamage /= 1.25f;
					}
					if (agent.statusEffects.hasTrait("ResistDamageMed"))
					{
						outputDamage /= 1.5f;
					}
					if (agent.statusEffects.hasTrait("ResistDamageLarge"))
					{
						outputDamage /= 2f;
					}
					if (agent.statusEffects.hasTrait("Giant"))
					{
						outputDamage /= 3f;
					}
					if (agent.statusEffects.hasTrait("Shrunk"))
					{
						outputDamage *= 3f;
					}
					if (agent.statusEffects.hasTrait("Diminutive"))
					{
						outputDamage *= 1.5f;
					}
					if (agent.frozen)
					{
						outputDamage *= 2f;
					}
					if (agent.statusEffects.hasSpecialAbility("ProtectiveShell") && agent.objectMult.chargingSpecialLunge)
					{
						outputDamage /= 8f;
					}
					if (agent.hasEmployer && agent.employer.statusEffects.hasSpecialAbility("ProtectiveShell") && agent.employer.objectMult.chargingSpecialLunge)
					{
						outputDamage /= 8f;
					}
					if (agent.oma.mindControlled && agent.mindControlAgent != null && (agent.mindControlAgent.statusEffects.hasTrait("MindControlledResistDamage") || (agent.mindControlAgent.oma.superSpecialAbility && agent.mindControlAgent.agentName == "Alien")))
					{
						outputDamage /= 1.5f;
					}
					if (flag2 && flag6 && !agent2.dead)
					{
						if (agent2.statusEffects.hasTrait("MoreDamageWhenHealthLow") && agent2.agentID != agent.agentID)
						{
							int num4 = (int)(agent2.healthMax / 4f);
							if (agent2.health <= (float)num4)
							{
								float num5 = agent2.health / (float)num4;
								num5 = (1f - num5) * outputDamage * 1.5f;
								outputDamage += num5;
							}
						}
						else if (agent2.statusEffects.hasTrait("MoreDamageWhenHealthLow2") && agent2.agentID != agent.agentID)
						{
							int num6 = (int)(agent2.healthMax / 2f);
							if (agent2.health <= (float)num6)
							{
								float num7 = agent2.health / (float)num6;
								num7 = (1f - num7) * outputDamage * 1.5f;
								outputDamage += num7;
							}
						}
						if (!testOnly && agent2.agentID != agent.agentID)
						{
							int num8 = agent2.critChance;
							num8 = agent2.DetermineLuck(num8, "CritChance", true);
							if (UnityEngine.Random.Range(0, 100) <= num8 - 1 && (!(myself.gc.levelType == "Tutorial") || !(a == "Explosion")))
							{
								outputDamage *= 2f;
								agent.critted = true;
							}
							if (agent2.statusEffects.hasTrait("ChanceToSlowEnemies2"))
							{
								int myChance = agent2.DetermineLuck(20, "ChanceToSlowEnemies", true);
								if (myself.gc.percentChance(myChance))
								{
									agent.statusEffects.AddStatusEffect("Slow");
								}
							}
							else if (agent2.statusEffects.hasTrait("ChanceToSlowEnemies"))
							{
								int myChance2 = agent2.DetermineLuck(8, "ChanceToSlowEnemies", true);
								if (myself.gc.percentChance(myChance2))
								{
									agent.statusEffects.AddStatusEffect("Slow");
								}
							}
						}
						if (agent2.statusEffects.hasTrait("MoreFollowersCauseMoreDamage") || agent2.statusEffects.hasTrait("MoreFollowersCauseMoreDamage2"))
						{
							float num9 = 1.2f;
							if (agent2.statusEffects.hasTrait("MoreFollowersCauseMoreDamage2"))
							{
								num9 = 1.4f;
							}
							float num10 = outputDamage;
							int num11 = 0;
							for (int j = 0; j < myself.gc.agentList.Count; j++)
							{
								Agent agent4 = myself.gc.agentList[j];
								if (agent4.hasEmployer && agent4.employer == agent2 && Vector2.Distance(agent4.tr.position, agent.tr.position) < 10.24f)
								{
									outputDamage += num10 * num9 - num10;
									num11++;
									if (num11 >= 3 && !myself.gc.challenges.Contains("NoLimits"))
									{
										break;
									}
								}
							}
						}
						if (agent2.oma.mindControlled && agent2.mindControlAgent != null && (agent2.mindControlAgent.statusEffects.hasTrait("MindControlledDamageMore") || (agent2.mindControlAgent.oma.superSpecialAbility && agent2.mindControlAgent.agentName == "Alien")))
						{
							outputDamage *= 1.5f;
						}
					}
					int num12 = 0;
					if (agent.inventory.equippedArmor != null && !testOnly && flag6)
					{
						InvItem equippedArmor = agent.inventory.equippedArmor;
						if (equippedArmor.armorDepletionType == "Everything")
						{
							num12++;
						}
						else if (equippedArmor.armorDepletionType == "Bullet" && a == "Bullet")
						{
							num12++;
						}
						else if (equippedArmor.armorDepletionType == "Fire" && a == "Fire")
						{
							num12++;
						}
						else if (equippedArmor.armorDepletionType == "FireAndEverything")
						{
							num12++;
						}
					}
					if (agent.inventory.equippedArmorHead != null && !testOnly && flag6)
					{
						InvItem equippedArmorHead = agent.inventory.equippedArmorHead;
						if (equippedArmorHead.armorDepletionType == "Everything")
						{
							num12++;
						}
						else if (equippedArmorHead.armorDepletionType == "Bullet" && a == "Bullet")
						{
							num12++;
						}
						else if (equippedArmorHead.armorDepletionType == "Fire" && a == "Fire")
						{
							num12++;
						}
						else if (equippedArmorHead.armorDepletionType == "FireAndEverything")
						{
							num12++;
						}
					}
					if (agent.inventory.equippedArmor != null && !testOnly && flag6)
					{
						InvItem equippedArmor2 = agent.inventory.equippedArmor;
						if (equippedArmor2.armorDepletionType == "Everything")
						{
							agent.inventory.DepleteArmor("Normal", Mathf.Clamp((int)(outputDamage * 2f), 0, 12) / num12);
						}
						else if (equippedArmor2.armorDepletionType == "Bullet" && a == "Bullet")
						{
							agent.inventory.DepleteArmor("Normal", Mathf.Clamp((int)(outputDamage * 2f), 0, 12) / num12);
						}
						else if (equippedArmor2.armorDepletionType == "Fire" && a == "Fire")
						{
							agent.inventory.DepleteArmor("Normal", Mathf.Clamp((int)(outputDamage * 2f), 0, 12) / num12);
						}
						else if (equippedArmor2.armorDepletionType == "FireAndEverything")
						{
							agent.inventory.DepleteArmor("Normal", Mathf.Clamp((int)(outputDamage * 2f), 0, 12) / num12);
						}
					}
					if (agent.inventory.equippedArmorHead != null && !testOnly && flag6)
					{
						InvItem equippedArmorHead2 = agent.inventory.equippedArmorHead;
						if (equippedArmorHead2.armorDepletionType == "Everything")
						{
							agent.inventory.DepleteArmor("Head", Mathf.Clamp((int)(outputDamage * 2f), 0, 12) / num12);
						}
						else if (equippedArmorHead2.armorDepletionType == "Bullet" && a == "Bullet")
						{
							agent.inventory.DepleteArmor("Head", Mathf.Clamp((int)(outputDamage * 2f), 0, 12) / num12);
						}
						else if (equippedArmorHead2.armorDepletionType == "Fire" && a == "Fire")
						{
							agent.inventory.DepleteArmor("Head", Mathf.Clamp((int)(outputDamage * 2f), 0, 12) / num12);
						}
						else if (equippedArmorHead2.armorDepletionType == "FireAndEverything")
						{
							agent.inventory.DepleteArmor("Head", Mathf.Clamp((int)(outputDamage * 2f), 0, 12) / num12);
						}
					}
					if (agent.statusEffects.hasTrait("MoreFollowersLessDamageToPlayer") || agent.statusEffects.hasTrait("MoreFollowersLessDamageToPlayer2"))
					{
						int num13 = 0;
						float num14 = 1.2f;
						if (agent.statusEffects.hasTrait("MoreFollowersLessDamageToPlayer2"))
						{
							num14 = 1.4f;
						}
						for (int k = 0; k < myself.gc.agentList.Count; k++)
						{
							Agent agent5 = myself.gc.agentList[k];
							if (agent5.hasEmployer && agent5.employer == agent && Vector2.Distance(agent5.tr.position, agent.tr.position) < 10.24f)
							{
								outputDamage /= num14;
								num13++;
								if (num13 >= 3 && !myself.gc.challenges.Contains("NoLimits"))
								{
									break;
								}
							}
						}
					}
					if (!testOnly && flag4)
					{
						agent.attackCooldown = 2f;
					}
				}
				if (flag3 && flag2 && (myself.objectName == "Bars" || myself.objectName == "BarbedWire"))
				{
					if (agent2.statusEffects.hasTrait("MeleeDestroysWalls2"))
					{
						outputDamage = 99f;
					}
					else if (agent2.statusEffects.hasTrait("MeleeDestroysWalls") && myself.objectName == "BarbedWire")
					{
						outputDamage = 99f;
					}
				}
				if (outputDamage > 200f)
				{
					outputDamage = 200f;
				}
				int num15 = Mathf.Clamp((int)outputDamage, 1, 1000);
				if ((damagerObject.isItem && a == "Throw" && outputDamage == 0f) || flag8)
				{
					num15 = 0;
				}
				else if (flag9)
				{
					num15 = 1;
				}
				if (flag2 && flag && !testOnly)
				{
					if ((float)num15 < agent.health)
					{
						Relationship relationship = agent.relationships.GetRelationship(agent2);
						relStatus myRel = relStatus.Neutral;
						bool flag12 = false;
						if (relationship != null)
						{
							myRel = relationship.relTypeCode;
							flag12 = relationship.sawBecomeHidden;
						}
						if ((!agent2.invisible || flag12) && flag4)
						{
							if ((agent2.isPlayer <= 0 || agent2.localPlayer || damagerObject.isItem || damagerObject.isExplosion || agent2.statusEffects.hasTrait("CantAttack")) && (!damagerObject.isExplosion || !damagerObject.noAngerOnHit) && !agent.mechEmpty)
							{
								agent.justHitByAgent3 = true;
								agent.relationships.AddRelHate(agent2, Mathf.Clamp(num15, 5, 200));
								agent.justHitByAgent3 = false;
							}
							agent.relationships.PotentialAlignmentCheck(myRel);
						}
					}
					if (flag4)
					{
						agent.SetJustHitByAgent(agent2);
					}
					agent.justHitByAgent2 = agent2;
					agent.lastHitByAgent = agent2;
					if (!agent2.killerRobot && !agent.killerRobot)
					{
						relStatus relCode3 = agent2.relationships.GetRelCode(agent);
						if (damagerObject.isExplosion)
						{
							Explosion explosion2 = (Explosion)damagerObject;
							if (explosion2.explosionType == "Huge" || explosion2.explosionType == "Ridiculous")
							{
								myself.gc.EnforcerAlertAttack(agent2, agent, 10.8f, explosion2.tr.position);
								if (agent2.ownerID != 0 && relCode3 == relStatus.Hostile)
								{
									myself.gc.EnforcerAlertAttack(agent, agent2, 10.8f, explosion2.tr.position);
								}
							}
							else
							{
								myself.gc.EnforcerAlertAttack(agent2, agent, 10.8f, explosion2.tr.position);
								if (agent2.ownerID != 0 && relCode3 == relStatus.Hostile)
								{
									myself.gc.EnforcerAlertAttack(agent, agent2, 10.8f, explosion2.tr.position);
								}
							}
						}
						else
						{
							myself.gc.EnforcerAlertAttack(agent2, agent, 7.4f);
							if (agent2.ownerID != 0 && relCode3 == relStatus.Hostile)
							{
								myself.gc.EnforcerAlertAttack(agent, agent2, 7.4f);
							}
						}
					}
					agent.damagedAmount = num15;
					if (agent.agentName == "Slave")
					{
						__instance.StartCoroutine(agent.agentInteractions.OwnCheckSlaveOwners(agent, agent2));
					}
					if (agent.isPlayer == 0 && !agent.hasEmployer && !agent.zombified && !agent.noEnforcerAlert)
					{
						agent2.oma.hasAttacked = true;
					}
				}
				if (flag3)
				{
					if (flag2)
					{
						if (!testOnly)
						{
							objectReal.lastHitByAgent = agent2;
							objectReal.damagedAmount = num15;
							if (objectReal.useForQuest != null || objectReal.destroyForQuest != null)
							{
								myself.gc.OwnCheck(agent2, objectReal.gameObject, "Normal", 0);
							}
						}
						if (!agent2.objectAgent && agent2.agentSpriteTransform.localScale.x > 1f)
						{
							num15 = 99;
						}
					}
					else if (!testOnly)
					{
						objectReal.lastHitByAgent = null;
						objectReal.damagedAmount = num15;
					}
				}
				if (!flag5 || flag3 || fromClient)
				{
					__result = num15;
					return false;
				}
				myself.tickEndDamage += num15;
				myself.tickEndObject = damagerObject;
				myself.tickEndRotation = damagerObject.tr.rotation;
				if (fromClient)
				{
					myself.tickEndDamageFromClient = true;
				}
				else
				{
					myself.tickEndDamageFromClient = false;
				}
				if (myself.tickEndObject.isBullet)
				{
					myself.tickEndBullet = (Bullet)myself.tickEndObject;
				}
				__result = 9999;
				return false;
			}
		}
	}
}
