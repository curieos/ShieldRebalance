using R2API;
using RoR2;
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ShieldRebalance
{
	public class PlayerShieldTracker
	{
		public HealthComponent PlayerHealthComponent { get; }
		public bool HadShieldsAtStartOfUpdate { get; set; }

		public PlayerShieldTracker(HealthComponent healthComponent)
		{
			PlayerHealthComponent = healthComponent;
			HadShieldsAtStartOfUpdate = false;
		}
	}

	public class Hooks
	{
		private static List<PlayerShieldTracker> players = new	List<PlayerShieldTracker>(); 

		internal static void Init()
		{
			On.RoR2.HealthComponent.ServerFixedUpdate += (orig, self) =>
			{
				orig(self);
				if (self.alive)
				{
					if (self.body.isPlayerControlled)
					{
						PlayerShieldTracker player = players.Find(x => x.PlayerHealthComponent == self);
						if (player != null)
						{
							player.HadShieldsAtStartOfUpdate = self.shield > 0;
						} 
						else
						{
							player = new PlayerShieldTracker(self);
							players.Add(player);
						}
					}
				}
			};

			On.RoR2.HealthComponent.TakeDamage += (orig, self, damageInfo) =>
			{
				bool triggerOSP = false;
				if (!NetworkServer.active)
				{
					Debug.LogWarning("[Server] function 'System.Void RoR2.HealthComponent::TakeDamage(RoR2.DamageInfo)' called on client");
					return;
				}
				if (!self.alive || self.godMode)
				{
					return;
				}
				float ospTimer = (float)typeof(HealthComponent).GetField("ospTimer", BindingFlags.NonPublic |BindingFlags.GetField | BindingFlags.Instance).GetValue(self);
				if (ospTimer > 0f)
				{
					return;
				}
				if (self.body.hasOneShotProtection && (damageInfo.damageType & DamageType.BypassOneShotProtection) != DamageType.BypassOneShotProtection)
				{
					float damage = damageInfo.damage;
					if (self.barrier <= 0f)
					{
						//float serverDamageTakenThisUpdate = (float)typeof(HealthComponent).GetField("serverDamageTakenThisUpdate", BindingFlags.NonPublic |BindingFlags.GetField | BindingFlags.Instance).GetValue(self);
						PlayerShieldTracker player = players.Find(x => x.PlayerHealthComponent == self);
						if (player != null)
						{
							if (player.HadShieldsAtStartOfUpdate)
							{
								if (damage > self.combinedHealth)
								{
									damage = self.shield + 0f;
									damageInfo.crit = false;
									triggerOSP = true;
									Chat.AddMessage("Trigger OSP? Shields: " + self.shield);
								}
							}
						}
						/* else
						{
							float ospDamageThreshold = (self.fullHealth) * (1f - self.body.oneShotProtectionFraction);
							float ospThresholdLeftThisTick = Mathf.Max(0f, ospDamageThreshold - serverDamageTakenThisUpdate);
							float fullDamage = damage;
							damage = Mathf.Min(damage, ospThresholdLeftThisTick);
							if (damage != fullDamage)
							{
								Chat.AddMessage("OSP Triggered off health");
							}
						} */
					}
					damageInfo.damage = damage;
				}

				orig(self, damageInfo);

				if (triggerOSP)
				{
					typeof(HealthComponent).GetMethod("TriggerOneShotProtection").Invoke(self, null);
				}
			};
		}
	}
}

/*
** if we have shields, prevent a one shot
** if we have greater than 90% health (no shields, prevent a one shot)
** else, let regular OSP handle it
*/
