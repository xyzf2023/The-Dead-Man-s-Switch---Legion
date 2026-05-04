// 尘世（可穿戴人类装备的机械体）defName 列表，供补丁与渲染判定使用
namespace DMS_Legion
{
    public static class TerraDefNames
    {
        public static readonly string[] DefNames = { "DMSL_Mech_Terra" };

        public static bool IsTerra(Verse.Pawn pawn)
        {
            if (pawn?.def?.defName == null) return false;
            for (int i = 0; i < DefNames.Length; i++)
                if (DefNames[i] == pawn.def.defName) return true;
            return false;
        }
    }
}
