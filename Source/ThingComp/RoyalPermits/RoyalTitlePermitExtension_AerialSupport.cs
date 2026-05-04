using Verse;

namespace DMS_Legion.RoyalPermits
{
    /// <summary>
    /// 皇权支援 Def 的 ModExtension：在 Def 中直接填写空中支援类型的 defName 以关联。
    /// 仅此一种关联方式，不采用其他关联。
    /// </summary>
    public class RoyalTitlePermitExtension_AerialSupport : DefModExtension
    {
        /// <summary>
        /// 空中支援类型 defName（AerialSupportTypeDef.defName），在皇权支援的 XML 中直接填写。
        /// </summary>
        public string aerialSupportTypeDefName = "";
    }
}
