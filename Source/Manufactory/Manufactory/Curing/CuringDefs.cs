namespace Manufactory.Curing
{
    public static class CuringDefs
    {
        public const string WetConcreteTerrainDefName = "Manufactory_WetConcrete";
        public const string CuredConcreteTerrainDefName = "Concrete";
        public const string WetConcreteWallDefName = "Manufactory_WetConcreteWall";
        public const string CuredConcreteWallDefName = "Manufactory_ConcreteWall";

        // Tune this value (ticks) to control how long wet concrete takes to cure.
        public const int CURE_TICKS = 60000;
    }
}
