using Debug = System.Diagnostics.Debug;
using SC2APIProtocol;
using Sharky;
using Sharky.DefaultBot;
using Sharky.Builds.BuildingPlacement;
using System.Linq;
using System;
using System.Reflection;
using System.Numerics;
using System.Diagnostics;

namespace Sharky.Builds.Zerg
{
    public class BuildTest : ZergSharkyBuild
    {
        private readonly Sharky.Builds.MacroServices.BuildingRequestCancellingService _buildingRequestCancellingService;

        private bool _extractorsRequested;
        private bool _extraDronesQueued;
        private bool _extractorTrickCompleted;
        private bool _hatcheryWallOffMainStarted;

        // Added flag so the message prints only once when supply == 12
        private bool _startLocationPrinted;

        // Typed WallDataService from the repo (avoid reflection)
        private readonly DefaultSharkyBot _defaultSharkyBot;
        private readonly WallDataService _wallDataService;
        private bool _wallDataSaved;

        // New: ensure we only preposition a drone once after the extractor trick
        private bool _dronePrepositioned;

        // Persist the chosen hatchery target across frames
        private Point2D _hatcheryTarget;

        // One-time breakpoint trigger for debugging when minerals > 275
        private bool _breakpointTriggered;

        // Track which drone we gave the move order to, so we can re-issue until it reaches target
        private ulong _prepositionDroneTag;

        public BuildTest(DefaultSharkyBot defaultSharkyBot)
            : base(defaultSharkyBot)
        {
            _buildingRequestCancellingService = defaultSharkyBot.BuildingRequestCancellingService;

            // store references to bot and WallDataService using existing repo property
            _defaultSharkyBot = defaultSharkyBot;
            _wallDataService = _defaultSharkyBot.WallDataService;
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

            // initialize the one-time flags
            _startLocationPrinted = false;
            _dronePrepositioned = false;
            _hatcheryTarget = null;
            _breakpointTriggered = false;

            _prepositionDroneTag = 0;

            // Generate and save wall data for current map once at the start of the build.
            if (!_wallDataSaved)
            {
                try
                {
                    var mapName = ResolveMapName();
                    if (string.IsNullOrEmpty(mapName))
                    {
                        Debug.WriteLine("BuildTest: Could not resolve map name; using fallback 'map'.");
                        mapName = "map";
                    }

                    if (_wallDataService == null)
                    {
                        Debug.WriteLine("BuildTest: WallDataService not found on DefaultSharkyBot. Skipping generation.");
                    }
                    else
                    {
                        Debug.WriteLine($"BuildTest: Generating wall data for map '{mapName}'...");
                        // call the repository method directly
                        _wallDataService.GetWallData(mapName);
                        Debug.WriteLine($"BuildTest: Wall data generation invoked for '{mapName}'.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"BuildTest: Failed to generate/save wall data: {ex.Message}");
                }
                _wallDataSaved = true;
            }
        }

        string ResolveMapName()
        {
            // Try common locations on DefaultSharkyBot and its BaseData for a map name property.
            try
            {
                // Try DefaultSharkyBot.BaseData.MapName or MapFileName
                var baseDataProp = _defaultSharkyBot.GetType().GetProperty("BaseData", BindingFlags.Public | BindingFlags.Instance);
                if (baseDataProp != null)
                {
                    var baseData = baseDataProp.GetValue(_defaultSharkyBot);
                    if (baseData != null)
                    {
                        var candidates = new[] { "MapName", "MapFileName", "Name" };
                        foreach (var candidate in candidates)
                        {
                            var p = baseData.GetType().GetProperty(candidate, BindingFlags.Public | BindingFlags.Instance);
                            if (p != null && p.PropertyType == typeof(string))
                            {
                                var val = (string)p.GetValue(baseData);
                                if (!string.IsNullOrEmpty(val)) return val;
                            }
                        }
                    }
                }

                // Try DefaultSharkyBot.MapName / MapFileName / CurrentMapName
                var botCandidates = new[] { "MapName", "MapFileName", "CurrentMapName" };
                foreach (var candidate in botCandidates)
                {
                    var p = _defaultSharkyBot.GetType().GetProperty(candidate, BindingFlags.Public | BindingFlags.Instance);
                    if (p != null && p.PropertyType == typeof(string))
                    {
                        var val = (string)p.GetValue(_defaultSharkyBot);
                        if (!string.IsNullOrEmpty(val)) return val;
                    }
                }

                // Try MacroData if exposed
                var macroDataProp = this.GetType().BaseType?.GetProperty("MacroData", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (macroDataProp != null)
                {
                    var macroData = macroDataProp.GetValue(this);
                    if (macroData != null)
                    {
                        var p = macroData.GetType().GetProperty("MapName", BindingFlags.Public | BindingFlags.Instance);
                        if (p != null && p.PropertyType == typeof(string))
                        {
                            var val = (string)p.GetValue(macroData);
                            if (!string.IsNullOrEmpty(val)) return val;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BuildTest.ResolveMapName reflection error: {ex.Message}");
            }

            return null;
        }

        public override void OnFrame(ResponseObservation observation)
        {
            base.OnFrame(observation);

            var frame = (int)observation.Observation.GameLoop;

            // If we already assigned a preposition drone, re-issue the move to the exact _hatcheryTarget until it's within tolerance.
            if (_prepositionDroneTag != 0 && _hatcheryTarget != null)
            {
                try
                {
                    var commander = ActiveUnitData.Commanders.Values.FirstOrDefault(c => c.UnitCalculation?.Unit != null && c.UnitCalculation.Unit.Tag == _prepositionDroneTag);
                    if (commander != null)
                    {
                        var pos = commander.UnitCalculation.Position;
                        var dx = pos.X - _hatcheryTarget.X;
                        var dy = pos.Y - _hatcheryTarget.Y;
                        var distSq = dx * dx + dy * dy;

                        const float arrivedDistanceSq = 1.0f; // within 1 unit
                        if (distSq > arrivedDistanceSq)
                        {
                            var moveActions = commander.Order(frame, Abilities.MOVE, new Point2D { X = _hatcheryTarget.X, Y = _hatcheryTarget.Y });
                            if (moveActions != null && moveActions.Any())
                            {
                                Debug.WriteLine($"BuildTest: Re-issued MOVE to preposition drone (tag={_prepositionDroneTag}) -> X={_hatcheryTarget.X}, Y={_hatcheryTarget.Y}");
                            }
                        }
                        else
                        {
                            // Drone reached target
                            _dronePrepositioned = true;
                            Debug.WriteLine($"BuildTest: Preposition drone (tag={_prepositionDroneTag}) reached target X={_hatcheryTarget.X}, Y={_hatcheryTarget.Y}");
                        }
                    }
                    else
                    {
                        // Assigned drone not found; reset tag so we can pick another
                        _prepositionDroneTag = 0;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"BuildTest: Exception reissuing move to preposition drone: {ex.Message}");
                    // don't block further logic
                }
            }

            // Plain breakpoint: break once when minerals exceed 275 — keep this exact guard so execution stops for inspection
            if (!_breakpointTriggered && MacroData.Minerals > 275)
            {
                _breakpointTriggered = true;
                System.Diagnostics.Debugger.Break();
            }

            // Print start location of main hatchery once when supply == 12
            if (!_startLocationPrinted && MacroData.FoodUsed == 12)
            {
                // Try find a hatchery from self units
                var hatchery = ActiveUnitData.SelfUnits.Values
                    .FirstOrDefault(u => u.Unit.UnitType == (uint)UnitTypes.ZERG_HATCHERY);

                if (hatchery != null)
                {
                    Debug.WriteLine($"Start Hatchery location: X={hatchery.Unit.Pos.X}, Y={hatchery.Unit.Pos.Y}");
                }
                else
                {
                    Debug.WriteLine("Start Hatchery not found when supply == 12.");
                }

                _startLocationPrinted = true;
            }

            // Step 5 Preposition Drone at 175 mineral for Hatchery Wall off
            // Use existing repo procedure to request a preposition (avoid reflection). If that doesn't achieve preposition, fallback to existing UnitCommander move.
            if (_extractorTrickCompleted && !_dronePrepositioned && MacroData.Minerals >= 175)
            {
                try
                {
                    Point2D target = null;

                    var mapName = ResolveMapName() ?? "map";

                    // 1) Try to get a saved wall location from WallDataService result (typed call)
                    if (_wallDataService != null)
                    {
                        try
                        {
                            var wallData = _wallDataService.GetWallData(mapName);
                            if (wallData != null)
                            {
                                foreach (var prop in wallData.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                                {
                                    var val = prop.GetValue(wallData);
                                    if (val == null) continue;

                                    if (val is Point2D p)
                                    {
                                        target = p;
                                        break;
                                    }

                                    var enumerable = val as System.Collections.IEnumerable;
                                    if (enumerable != null)
                                    {
                                        foreach (var item in enumerable)
                                        {
                                            if (item == null) continue;
                                            var itemType = item.GetType();
                                            var xProp = itemType.GetProperty("X");
                                            var yProp = itemType.GetProperty("Y");
                                            if (xProp != null && yProp != null)
                                            {
                                                var x = Convert.ToSingle(xProp.GetValue(item));
                                                var y = Convert.ToSingle(yProp.GetValue(item));
                                                target = new Point2D { X = x, Y = y };
                                                break;
                                            }
                                        }
                                    }
                                    if (target != null) break;
                                }
                            }
                        }
                        catch
                        {
                            // best-effort; ignore failures from WallDataService invocation
                        }
                    }

                    // 2) Fallback heuristic (unchanged)
                    if (target == null)
                    {
                        try
                        {
                            object firstBase = null;
                            var selfBasesProp = BaseData.GetType().GetProperty("SelfBases", BindingFlags.Public | BindingFlags.Instance);
                            if (selfBasesProp != null)
                            {
                                var selfBases = selfBasesProp.GetValue(BaseData) as System.Collections.IEnumerable;
                                if (selfBases != null)
                                {
                                    foreach (var b in selfBases) { firstBase = b; break; }
                                }
                            }

                            if (firstBase == null)
                            {
                                var baseLocsProp = BaseData.GetType().GetProperty("BaseLocations", BindingFlags.Public | BindingFlags.Instance);
                                if (baseLocsProp != null)
                                {
                                    var baseLocs = baseLocsProp.GetValue(BaseData) as System.Collections.IEnumerable;
                                    if (baseLocs != null)
                                    {
                                        foreach (var b in baseLocs) { firstBase = b; break; }
                                    }
                                }
                            }

                            if (firstBase != null)
                            {
                                var locProp = firstBase.GetType().GetProperty("Location", BindingFlags.Public | BindingFlags.Instance);
                                var mineralProp = firstBase.GetType().GetProperty("MineralLineLocation", BindingFlags.Public | BindingFlags.Instance);

                                if (locProp != null)
                                {
                                    var loc = locProp.GetValue(firstBase);
                                    var xProp = loc.GetType().GetProperty("X");
                                    var yProp = loc.GetType().GetProperty("Y");
                                    var baseX = Convert.ToSingle(xProp.GetValue(loc));
                                    var baseY = Convert.ToSingle(yProp.GetValue(loc));

                                    float targetX = baseX;
                                    float targetY = baseY;

                                    if (mineralProp != null)
                                    {
                                        var mineral = mineralProp.GetValue(firstBase);
                                        if (mineral != null)
                                        {
                                            var mX = Convert.ToSingle(mineral.GetType().GetProperty("X").GetValue(mineral));
                                            var mY = Convert.ToSingle(mineral.GetType().GetProperty("Y").GetValue(mineral));

                                            var dirX = baseX - mX;
                                            var dirY = baseY - mY;
                                            var len = (float)Math.Sqrt(dirX * dirX + dirY * dirY);
                                            if (len > 0.001f) { dirX /= len; dirY /= len; }

                                            const float offset = 6.0f;
                                            targetX = baseX + dirX * offset;
                                            targetY = baseY + dirY * offset;
                                        }
                                    }
                                    else
                                    {
                                        targetY = baseY - 6.0f;
                                    }

                                    target = new Point2D { X = targetX, Y = targetY };
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"BuildTest: fallback top-of-ramp heuristic failed: {ex.Message}");
                        }
                    }

                    if (target != null)
                    {
                        Debug.WriteLine($"BuildTest: Preposition target X={target.X}, Y={target.Y} (minerals={MacroData.Minerals})");

                        // Deterministic direct preposition: issue an immediate MOVE to the exact printed point.
                        try
                        {
                            // Record hatchery target
                            _hatcheryTarget = new Point2D { X = target.X, Y = target.Y };

                            // Attempt best-effort notify (doesn't replace direct move)
                            try
                            {
                                if (PrePositionBuilderTask != null)
                                {
                                    PrePositionBuilderTask.SendBuilder(_hatcheryTarget, frame);
                                }
                            }
                            catch { /* ignore */ }

                            // Immediately command a free drone to move to the exact printed point (use a fresh Point2D instance)
                            var movePoint = new Point2D { X = target.X, Y = target.Y };

                            var freeDrone = ActiveUnitData.Commanders.Values
                                .Where(c => c.UnitCalculation?.Unit != null && c.UnitCalculation.Unit.UnitType == (uint)UnitTypes.ZERG_DRONE)
                                .Where(c => !c.Claimed)
                                .OrderBy(c => Vector2.DistanceSquared(c.UnitCalculation.Position, new Vector2(movePoint.X, movePoint.Y)))
                                .FirstOrDefault();

                            if (freeDrone != null)
                            {
                                freeDrone.Claimed = true;
                                try { freeDrone.UnitRole = UnitRole.Hide; } catch { /* best-effort */ }

                                var moveActions = freeDrone.Order(frame, Abilities.MOVE, new Point2D { X = movePoint.X, Y = movePoint.Y });
                                if (moveActions != null && moveActions.Any())
                                {
                                    Debug.WriteLine($"BuildTest: Direct move order issued to drone (tag={freeDrone.UnitCalculation.Unit.Tag}) -> X={movePoint.X}, Y={movePoint.Y}");
                                }
                                else
                                {
                                    Debug.WriteLine("BuildTest: Direct move order returned no actions (still claiming drone to hold it).");
                                }

                                _dronePrepositioned = false; // not yet arrived
                                _prepositionDroneTag = freeDrone.UnitCalculation.Unit.Tag;
                                _hatcheryTarget = movePoint;
                            }
                            else
                            {
                                Debug.WriteLine("BuildTest: No free drone commander found to preposition.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"BuildTest: Exception while prepositioning drone: {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("BuildTest: Could not determine top-of-ramp target for hatchery prepositioning.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"BuildTest: Exception while prepositioning drone: {ex.Message}");
                }
            }

            // Trigger actual hatchery build when prepositioned drone is waiting and minerals >= 275
            if (_dronePrepositioned && !_hatcheryWallOffMainStarted && MacroData.Minerals >= 275)
            {
                try
                {
                    // Request an additional hatchery (increase desired count to 2)
                    MacroData.DesiredProductionCounts[UnitTypes.ZERG_HATCHERY] = 2;
                    _hatcheryWallOffMainStarted = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"BuildTest: Exception while requesting hatchery build: {ex.Message}");
                }
            }

            if (_extractorTrickCompleted)
            {
                if (_hatcheryWallOffMainStarted)
                {
                    return;
                }
                // Start building the hatchery wall-off at main base
                //


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

            // Step 2: Once 1 extractor in progress, queue 1 extra drones
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
                    //MacroData.DesiredProductionCounts[UnitTypes.ZERG_HATCHERY] = 2;

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