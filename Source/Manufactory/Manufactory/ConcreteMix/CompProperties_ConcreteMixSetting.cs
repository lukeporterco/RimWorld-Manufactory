using Verse;
using Manufactory.Curing;

namespace Manufactory.ConcreteMix
{
    public class CompProperties_ConcreteMixSetting : CompProperties
    {
        public int setTicks = CuringDefs.DefaultCureTicks;
        public string slagDefName = CuringDefs.ConcreteSlagDefName;
        public string mixerDefName = CuringDefs.ConcreteMixerDefName;

        public CompProperties_ConcreteMixSetting()
        {
            this.compClass = typeof(CompConcreteMixSetting);
        }
    }
}
