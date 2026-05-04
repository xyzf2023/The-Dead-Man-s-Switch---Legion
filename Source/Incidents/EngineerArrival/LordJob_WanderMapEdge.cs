// ============================================================================
// 文件：LordJob_WanderMapEdge.cs
// 说明：游荡机兵 LordJob，使机械体在地图边缘游荡
// ============================================================================

using Verse;
using Verse.AI.Group;

namespace DMS_Legion.Incidents.EngineerArrival
{
    /// <summary>
    /// 简单的 LordJob，将 pawn 置于 Lord 中并赋予地图边缘游荡 Duty
    /// </summary>
    public class LordJob_WanderMapEdge : LordJob
    {
        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            LordToil toil = new LordToil_WanderMapEdge();
            stateGraph.AddToil(toil);
            stateGraph.StartingToil = toil;
            return stateGraph;
        }
    }
}
