using RoR2;
using BepInEx;
using MonoMod.Cil;
using R2API.Utils;
using UnityEngine;
using Mono.Cecil.Cil;
using System;
using BepInEx.Configuration;
using System.Runtime.CompilerServices;
using UnityEngine.Networking;

namespace ShieldRebalance
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInPlugin(ModGuid, ModName, ModVer)]
    public class ShieldRebalance : BaseUnityPlugin
    {
        private const string ModVer = "0.1.4";
        private const string ModName = "ShieldRebalance";
        public const string ModGuid = "com.CurieOS.ShieldRebalance";

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
						if (!healthComponent) {
							return;
						}
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
					Debug.LogError("ShieldRebalance: failed to apply IL patch (HealthComponent.ServerFixedUpdate)! Mod will not work.");
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
					cursor.Emit(OpCodes.Ldarg_1);
					cursor.Emit(OpCodes.Ldarg_0);
					cursor.Emit(OpCodes.Ldfld, typeof(HealthComponent).GetFieldCached("serverDamageTakenThisUpdate"));
					cursor.EmitDelegate<Action<HealthComponent, DamageInfo, float>>((healthComponent, damageInfo, serverDamageTakenThisUpdate) =>
					{
						if (!healthComponent) {
							return;
						}
						AdditionalShieldInfo asi = null;
                        if(shieldInfoAttachments.TryGetValue(healthComponent, out asi))
						{
							if (asi.hadShields)
							{
								if (damageInfo.damage > healthComponent.combinedHealth)
								{
									Chat.AddMessage("OSP Triggered off Shields.");
									float healthDamage = Mathf.Max(0f, -((healthComponent.fullHealth * healthComponent.body.oneShotProtectionFraction) - healthComponent.health));
									damageInfo.damage = healthComponent.shield + healthDamage;
								}
							}
						}
					});
				}
				else
				{
					Debug.LogError("ShieldRebalance: failed to apply IL patch (HealthComponent.TakeDamage)! Mod will not work.");
                    return;
				}
			};
        }
    }
}