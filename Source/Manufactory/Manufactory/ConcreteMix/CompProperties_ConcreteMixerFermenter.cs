using Manufactory.Curing;
using Verse;

namespace Manufactory.ConcreteMix
{
    public class CompProperties_ConcreteMixerFermenter : CompProperties
    {
        public int batchDurationTicks = 2500;
        public int mixPerBatch = 20;
        public int chunkPerBatch = 1;
        public int chemfuelPerBatch = 5;
        public int maxInputChunks = 5;
        public int maxInputChemfuel = 20;
        public int maxMixCapacity = 200;
        public string outputDefName = CuringDefs.ConcreteMixDefName;

        public CompProperties_ConcreteMixerFermenter()
        {
            this.compClass = typeof(CompConcreteMixerFermenter);
        }
    }
}
