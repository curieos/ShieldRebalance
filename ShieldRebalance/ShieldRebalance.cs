using BepInEx;
using BepInEx.Logging;
using R2API;
using R2API.AssetPlus;
using R2API.Utils;

namespace ShieldRebalance
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInPlugin(ModGuid, ModName, ModVer)]
    public class ShieldRebalance : BaseUnityPlugin
    {
        private const string ModVer = "0.1.2";
        private const string ModName = "ShieldRebalance";
        public const string ModGuid = "com.CurieOS.ShieldRebalance";

        internal new static ManualLogSource Logger; // allow access to the logger across the plugin classes

        public void Awake()
        {
            Logger = base.Logger;

            Hooks.Init();
        }
    }
}