using HarmonyLib;

namespace XQuinn
{
    public sealed class HarmonyContainer
    {
        public readonly Harmony Harmony;
        public string ID => Harmony.Id;
        public HarmonyContainer(string id)
        {
            Harmony = new(id);
        }
        public void Patch(bool patch)
        {
            if (patch) Harmony.PatchAll();
            else Harmony.UnpatchAll();
        }
    }
}