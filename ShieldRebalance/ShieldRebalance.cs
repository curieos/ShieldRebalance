using RoR2;
using BepInEx;
using MonoMod.Cil;
using R2API.Utils;
using UnityEngine;
using Mono.Cecil.Cil;
using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Runtime.CompilerServices;
using UnityEngine.Networking;

namespace ShieldRebalance
{
	[BepInDependency(R2API.R2API.PluginGUID)]
	[BepInPlugin(ModGuid, ModName, ModVer)]
	public class ShieldRebalance : BaseUnityPlugin
	{
		private const string ModVer = "0.3.0";
		private const string ModName = "ShieldRebalance";
		public const string ModGuid = "com.CurieOS.ShieldRebalance";
		internal static BepInEx.Logging.ManualLogSource _logger;

		public class AdditionalShieldInfo : R2API.Networking.Interfaces.ISerializableObject {
			public bool hadShields = false; 

			public void Deserialize(NetworkReader reader) {
				hadShields = reader.ReadBoolean(); 
			}

			public void Serialize(NetworkWriter writer) {
				writer.Write(hadShields); 
			}
		}
		private readonly ConditionalWeakTable<object, AdditionalShieldInfo> shieldInfoAttachments = new ConditionalWeakTable<object, AdditionalShieldInfo>();

		public void Awake()
		{
			_logger = base.Logger;
			
			IL.RoR2.HealthComponent.ServerFixedUpdate += (il) =>
			{
				ILCursor cursor = new ILCursor(il);
				bool ILFound = cursor.TryGotoNext(
					MoveType.After,
					x => x.MatchLdarg(0),
					x => x.MatchLdarg(0),
					x => x.MatchLdfld(typeof(HealthComponent).GetFieldCached("ospTimer")),
					x => x.MatchCall<UnityEngine.Time>("get_fixedDeltaTime"),
					x => x.MatchSub(),
					x => x.MatchStfld(typeof(HealthComponent).GetFieldCached("ospTimer"))
				);
				if (ILFound)
				{
					cursor.Emit(OpCodes.Ldarg_0);
					cursor.EmitDelegate<Action<HealthComponent>>((healthComponent) =>
					{
						if (!healthComponent) return;
						if (!healthComponent.body.hasOneShotProtection) return;
						AdditionalShieldInfo asi = null;
						if(!shieldInfoAttachments.TryGetValue(healthComponent, out asi))
						{
							asi = new AdditionalShieldInfo();
							asi.hadShields = healthComponent.shield > 0;
							shieldInfoAttachments.Add(healthComponent, asi);
						}
						else
						{
							asi.hadShields = healthComponent.shield > 0;
						}
					});
				}
				else
				{
					Logger.Log(LogLevel.Error, "ShieldRebalance: failed to apply IL patch (HealthComponent.ServerFixedUpdate)! Mod will not work.");
					return;
				}
			};

			IL.RoR2.HealthComponent.TakeDamage += (il) =>
			{
				ILCursor cursor = new ILCursor(il);
				bool ILFound = cursor.TryGotoNext(
					MoveType.After,
					x => x.MatchLdarg(0),
					x => x.MatchLdfld(typeof(HealthComponent).GetFieldCached("body")),
					x => x.MatchCallvirt<CharacterBody>("get_hasOneShotProtection"),
					x => x.MatchBrfalse(out _),
					x => x.MatchLdarg(1),
					x => x.MatchLdfld(typeof(DamageInfo).GetFieldCached("damageType")),
					x => x.MatchLdcI4(out _),
					x => x.MatchAnd(),
					x => x.MatchLdcI4(out _),
					x => x.MatchBeq(out _)
				);
				if (ILFound)
				{
					cursor.Emit(OpCodes.Ldarg_0);
					cursor.Emit(OpCodes.Ldloc, 5);
					cursor.Emit(OpCodes.Ldarg_0);
					cursor.Emit(OpCodes.Ldfld, typeof(HealthComponent).GetFieldCached("serverDamageTakenThisUpdate"));
					cursor.EmitDelegate<Func<HealthComponent, float, float, float>>((healthComponent, damage, serverDamageTakenThisUpdate) =>
					{
						if (!healthComponent) return damage;
						AdditionalShieldInfo asi = null;
						if(shieldInfoAttachments.TryGetValue(healthComponent, out asi))
						{
							if (healthComponent.barrier <= 0)
							{
								if (asi.hadShields)
								{
									if (damage >= healthComponent.combinedHealth)
									{
										float healthDamage = Mathf.Max(0f, -((healthComponent.fullHealth * healthComponent.body.oneShotProtectionFraction) - healthComponent.health));
										damage = healthComponent.shield + healthDamage;
										healthComponent.InvokeMethod("TriggerOneShotProtection");
										return damage;
									}
								}
								else {
									float num5 = (healthComponent.fullHealth) * (1f - healthComponent.body.oneShotProtectionFraction);
									float b = Mathf.Max(0f, num5 - serverDamageTakenThisUpdate);
									float originalDamage = damage;
									damage = Mathf.Min(damage, b);
									if (damage != originalDamage)
									{
										healthComponent.InvokeMethod("TriggerOneShotProtection");
									}
									return damage;
								}
							}
						}
						return damage;
					});
					cursor.Emit(OpCodes.Stloc, 5);
				}
				else
				{
					Logger.Log(LogLevel.Error, "ShieldRebalance: failed to apply IL patch (HealthComponent.TakeDamage)! Mod will not work.");
					return;
				}
			};
		}
	}
}