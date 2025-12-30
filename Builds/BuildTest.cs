using SC2APIProtocol;
using Sharky.DefaultBot;
using System.Linq;

namespace Sharky.Builds.Zerg
{
    public class BuildTest : ZergSharkyBuild
    {
        private readonly Sharky.Builds.MacroServices.BuildingRequestCancellingService _buildingRequestCancellingService;

        private bool _extractorsRequested;
        private bool _extraDronesQueued;
        private bool _extractorTrickCompleted;

        // minimal additions to enable Step5 call
        private readonly DefaultSharkyBot _defaultSharkyBot;
        private bool _step5Called;

        // Stop guard for >275 minerals
        private bool _stopTriggered;

        public BuildTest(DefaultSharkyBot defaultSharkyBot)
            : base(defaultSharkyBot)
        {
            _buildingRequestCancellingService = defaultSharkyBot.BuildingRequestCancellingService;

            // store bot reference for Step5 helper
            _defaultSharkyBot = defaultSharkyBot;
            _step5Called = false;

            _stopTriggered = false;
        }

        public override void StartBuild(int frame)
        {
            base.StartBuild(frame);

            BuildOptions.StrictWorkerCount = true;
            BuildOptions.StrictGasCount = true;
            BuildOptions.StrictSupplyCount = true;

            MacroData.DesiredGases = 0;
            MacroData.DesiredProductionCounts[UnitTypes.ZERG_HATCHERY] = 1;
            MacroData.DesiredUnitCounts[UnitTypes.ZERG_DRONE] = 22;
            MacroData.DesiredUnitCounts[UnitTypes.ZERG_QUEEN] = 1;
            MacroData.DesiredUnitCounts[UnitTypes.ZERG_OVERLORD ] = 1;

            _extractorsRequested = false;
            _extraDronesQueued = false;
            _extractorTrickCompleted = false;
        }

        public override void OnFrame(ResponseObservation observation)
        {
            base.OnFrame(observation);

            // Early stop: break once when minerals exceed 275 and prevent further Step5 calls
            if (!_stopTriggered && MacroData != null && MacroData.Minerals > 275)
            {
                _stopTriggered = true;
                System.Console.WriteLine($"BuildTest: Stopping at minerals={MacroData.Minerals} (>275)");
                System.Diagnostics.Debugger.Break();
                // return to avoid any build actions this frame
                return;
            }

            if (_extractorTrickCompleted)
            {
                // Call Step5 exactly once after minerals exceed 175
                if (!_step5Called && MacroData != null && MacroData.Minerals > 175)
                {
                    try
                    {
                        var target = Step5.CalculateTopOfRamp(_defaultSharkyBot, BaseData, ActiveUnitData);
                        System.Console.WriteLine($"BuildTest: Step5 target X={target.X}, Y={target.Y}");
                        // Delegate to Maw helper to perform prepositioning; Step5 no longer issues orders
                        Maw.MicroControllers.MineralWalkerMaw.PrepositionAt(_defaultSharkyBot, target, MacroData.Frame);
                    }
                    catch (System.Exception ex)
                    {
                        System.Console.WriteLine($"BuildTest: Step5 call failed: {ex.Message}");
                    }
                    _step5Called = true;
                }

                return;
            }

            var supply = MacroData.FoodUsed;
            var larvaCount = ActiveUnitData.SelfUnits.Values.Count(u => u.Unit.UnitType == (uint)UnitTypes.ZERG_LARVA);
            var minerals = MacroData.Minerals;

            // Step 1: At 14 supply, 2 larva, 250 minerals, request double extractor
            //if (!_extractorsRequested && supply >= 14 && larvaCount >= 2 && minerals >= 250)
            if (!_extractorsRequested && supply >= 14 && larvaCount >= 1 && minerals >= 120)
            {
                MacroData.DesiredGases = 1; // Sharky will assign two drones to build
                _extractorsRequested = true;
            }

            // Step 2: Once 2 extractors in progress, queue 2 extra drones
            var inProgressExtractors = ActiveUnitData.SelfUnits.Values.Count(u =>
                u.Unit.UnitType == (uint)UnitTypes.ZERG_EXTRACTOR &&
                u.Unit.BuildProgress < 1.0f);

            if (_extractorsRequested && !_extraDronesQueued && inProgressExtractors >= 1)
            {
                var currentDroneCount = UnitCountService.Count(UnitTypes.ZERG_DRONE);
                MacroData.DesiredUnitCounts[UnitTypes.ZERG_DRONE] = currentDroneCount + 1;
                _extraDronesQueued = true;
                _extractorsRequested = false;

            }

            // Step 3: When extra drones are queued, cancel extractors
            if (_extraDronesQueued)
            {
                if (inProgressExtractors > 0)
                {
                    // Sharky uses BuildingRequestCancellingService for cancels
                    MacroData.DesiredGases = 0;

                    _buildingRequestCancellingService.RequestCancel(UnitTypes.ZERG_EXTRACTOR, 0);
                }
                else
                {
                    // Trick complete, reset macro intent
                    _extractorTrickCompleted = true;
                    MacroData.DesiredGases = 0;
                    BuildOptions.StrictGasCount = true;
                    MacroData.DesiredProductionCounts[UnitTypes.ZERG_HATCHERY] = 2;

                    // Step 4: Queue up next Overlord
                    MacroData.DesiredUnitCounts[UnitTypes.ZERG_OVERLORD] =
                        UnitCountService.Count(UnitTypes.ZERG_OVERLORD) + 1;
                }
            }
        }

        public override bool Transition(int frame)
        {
            return false;
        }
    }
}