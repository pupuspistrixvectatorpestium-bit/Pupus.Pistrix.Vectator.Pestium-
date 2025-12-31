using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using SC2APIProtocol;
using Sharky;
using Sharky.DefaultBot;
using Debug = System.Diagnostics.Debug;

namespace Maw.MicroControllers
{
    /// <summary>
    /// Maw-specific helpers for selecting drones for prepositioning.
    /// Kept outside the Sharky namespace to avoid interfering with upstream updates.
    /// </summary>
    public static class MineralWalkerMaw
    {
        public static UnitCommander SelectDroneForPreposition(
            BaseData baseData,
            SharkyUnitData sharkyUnitData,
            ActiveUnitData activeUnitData,
            Point2D target,
            int frame,
            int newThreshold = 50,
            float hatcheryRadius = 4f,
            float workerRadius = 6f)
        {
            if (target == null || baseData == null || sharkyUnitData == null || activeUnitData == null) return null;

            var drones = activeUnitData.Commanders.Values
                .Where(c => c.UnitCalculation?.Unit != null && c.UnitCalculation.Unit.UnitType == (uint)UnitTypes.ZERG_DRONE)
                .ToList();

            if (!drones.Any()) return null;

            var targetV = new Vector2(target.X, target.Y);
            var selfBases = baseData.SelfBases.ToList();

            UnitCommander bestIdle = null;
            float bestIdleDist = float.MaxValue;

            UnitCommander bestNew = null;
            float bestNewDist = float.MaxValue;

            UnitCommander bestAtHatch = null;
            float bestAtHatchDist = float.MaxValue;

            UnitCommander bestReturning = null;
            float bestReturningDist = float.MaxValue;

            foreach (var d in drones)
            {
                var uc = d.UnitCalculation;
                var u = uc.Unit;
                var pos = uc.Position;
                var distToTarget = Vector2.DistanceSquared(pos, targetV);

                var orders = u.Orders;
                // include drone-specific return ability and SMART
                var hasReturnOrder = orders != null && orders.Any(o => o != null && (
                    ((uint)o.AbilityId == (uint)Abilities.HARVEST_RETURN) ||
                    ((uint)o.AbilityId == (uint)Abilities.HARVEST_RETURN_DRONE) ||
                    ((uint)o.AbilityId == (uint)Abilities.SMART)
                ));
                // robust carrying-resource detection using SharkyUnitData (null-safe)
                var carryingResource = u.BuffIds != null && u.BuffIds.Any(b => sharkyUnitData.CarryingResourceBuffs.Contains((Buffs)b));
                var hasAnyOrder = orders != null && orders.Count > 0;

                // idle: no orders, not carrying, not claimed, not building
                // Only the idle branch excludes claimed drones (per your instruction).
                var isIdle = !hasAnyOrder && !carryingResource && !d.Claimed && d.UnitRole != UnitRole.Build;
                if (isIdle)
                {
                    // If an idle drone that fits is found, use it immediately.
                    return d;
                }

                // newly created: seen very recently (frame-based)
                if (frame - d.FrameFirstSeen <= newThreshold)
                {
                    // If a newly created drone fits, use it immediately.
                    return d;
                }

                // at hatchery and already returned cargo: close to any base resource center and not carrying
                var atHatch = selfBases.Any(sb =>
                {
                    if (sb.ResourceCenter == null) return false;
                    var rc = sb.ResourceCenter.Pos;
                    var rcPos = new Vector2(rc.X, rc.Y);
                    return Vector2.DistanceSquared(rcPos, pos) <= hatcheryRadius * hatcheryRadius;
                });

                if (atHatch && !carryingResource)
                {
                    // Only the best at-hatch candidate will be used (store and continue scanning other drones).
                    if (distToTarget < bestAtHatchDist)
                    {
                        bestAtHatchDist = distToTarget;
                        bestAtHatch = d;
                    }
                    continue;
                }

                // For any carrying/returning drones, attempt to return minerals now (don't select them for preposition).
                if (carryingResource || hasReturnOrder)
                {
                    try
                    {
                        // Build the minimal data needed to call ReturnMineralsAndGoBuild for this drone.
                        var selfBase = baseData?.SelfBases?.FirstOrDefault();
                        Vector2 baseVector = new Vector2(0, 0);
                        Point2D dropOffPoint = null;
                        Point2D harvestPoint = null;
                        ulong baseTag = 0;

                        if (selfBase != null)
                        {
                            if (selfBase.ResourceCenter != null)
                            {
                                baseVector = new Vector2(selfBase.ResourceCenter.Pos.X, selfBase.ResourceCenter.Pos.Y);
                                baseTag = selfBase.ResourceCenter.Tag;
                            }

                            var miningInfo = selfBase.MineralMiningInfo?.FirstOrDefault(mi => mi.Workers.Any(w => w.UnitCalculation?.Unit?.Tag == d.UnitCalculation.Unit.Tag))
                                            ?? selfBase.MineralMiningInfo?.OrderBy(mi => Vector2.DistanceSquared(d.UnitCalculation.Position, new Vector2(mi.ResourceUnit.Pos.X, mi.ResourceUnit.Pos.Y))).FirstOrDefault();

                            if (miningInfo != null)
                            {
                                dropOffPoint = miningInfo.DropOffPoint;
                                harvestPoint = miningInfo.HarvestPoint;
                            }
                        }

                        if (dropOffPoint == null) dropOffPoint = new Point2D { X = baseVector.X, Y = baseVector.Y };
                        if (harvestPoint == null) harvestPoint = new Point2D { X = d.UnitCalculation.Position.X, Y = d.UnitCalculation.Position.Y };

                        // Issue return actions (ignore returned actions here, other systems will filter/send them).
                        _ = ReturnMineralsAndGoBuild(d, baseVector, dropOffPoint, harvestPoint, target, frame, baseTag);
                    }
                    catch
                    {
                        // swallow — we don't want a single drone's return attempt to break selection.
                    }
                }
            }

            // Selection priority:
            // 1) bestAtHatch (only this at‑hatch drone gets used)
            // 2) fallback: closest drone to target (claimed or not) — everything else has been instructed to return where possible
            if (bestAtHatch != null) return bestAtHatch;

            return drones
                .OrderBy(c => Vector2.DistanceSquared(c.UnitCalculation.Position, targetV))
                .FirstOrDefault();
        }

        // Mirror MineralMiner.ReturnMinerals behavior but queue MOVE to buildLocation instead of moving back to harvest point.
        // This helper avoids referencing Sharky internal types not available here (e.g. MiningInfo).
        public static List<SC2APIProtocol.Action> ReturnMineralsAndGoBuild(
            UnitCommander worker,
            Vector2 baseVector,
            Point2D dropOffPoint,
            Point2D harvestPoint,
            Point2D buildLocation,
            int frame,
            ulong baseTag)
        {
            if (worker == null) return null;

            var actions = new List<SC2APIProtocol.Action>();
            var workerVector = worker.UnitCalculation.Position;
            var distanceSquared = Vector2.DistanceSquared(baseVector, workerVector);

            // Match MineralMiner.ReturnMinerals semantics but queue move to buildLocation
            if (distanceSquared > 20)
            {
                // too far: use SMART to return/target base
                return worker.Order(frame, Abilities.SMART, null, baseTag);
            }
            else if (distanceSquared < 10)
            {
                // close: return cargo, then queue MOVE to build location
                var returnActions = worker.Order(frame, Abilities.HARVEST_RETURN);
                if (returnActions != null) actions.AddRange(returnActions);

                var queuedMove = worker.Order(frame, Abilities.MOVE, buildLocation, 0, false, true);
                if (queuedMove != null) actions.AddRange(queuedMove);

                return actions.Count > 0 ? actions : null;
            }
            else
            {
                // mid distance: move to dropoff then queue SMART to base
                var moveActions = worker.Order(frame, Abilities.MOVE, dropOffPoint, 0, false);
                if (moveActions != null) actions.AddRange(moveActions);

                var smartActions = worker.Order(frame, Abilities.SMART, null, baseTag, false, true);
                if (smartActions != null) actions.AddRange(smartActions);

                return actions.Count > 0 ? actions : null;
            }
        }

        // Perform the API calls for a selected drone: return cargo if needed, then queue or issue MOVE to movePoint.
        public static List<SC2APIProtocol.Action> PrepositionSelectedDrone(
            UnitCommander drone,
            BaseData baseData,
            ActiveUnitData activeUnitData,
            Point2D movePoint,
            int frame,
            SharkyUnitData sharkyUnitData)
        {
            if (drone == null) return null;

            // detect carrying/return state
            var hasReturnOrder = drone.UnitCalculation.Unit.Orders.Any(o =>
                o != null && (
                    ((uint)o.AbilityId == (uint)Abilities.HARVEST_RETURN) ||
                    ((uint)o.AbilityId == (uint)Abilities.HARVEST_RETURN_DRONE) ||
                    ((uint)o.AbilityId == (uint)Abilities.SMART)
                ));
            var carryingResource = drone.UnitCalculation.Unit.BuffIds != null && drone.UnitCalculation.Unit.BuffIds.Any(b => sharkyUnitData.CarryingResourceBuffs.Contains((Buffs)b));

            // if carrying or returning, reproduce MineralMiner.ReturnMinerals flow but queue MOVE -> buildLocation
            if (carryingResource || hasReturnOrder)
            {
                var selfBase = baseData?.SelfBases?.FirstOrDefault();
                Vector2 baseVector = new Vector2(0, 0);
                Point2D dropOffPoint = null;
                Point2D harvestPoint = null;
                ulong baseTag = 0;

                if (selfBase != null)
                {
                    if (selfBase.ResourceCenter != null)
                    {
                        baseVector = new Vector2(selfBase.ResourceCenter.Pos.X, selfBase.ResourceCenter.Pos.Y);
                        baseTag = selfBase.ResourceCenter.Tag;
                    }

                    var miningInfo = selfBase.MineralMiningInfo?.FirstOrDefault(mi => mi.Workers.Any(w => w.UnitCalculation?.Unit?.Tag == drone.UnitCalculation.Unit.Tag))
                                    ?? selfBase.MineralMiningInfo?.OrderBy(mi => Vector2.DistanceSquared(drone.UnitCalculation.Position, new Vector2(mi.ResourceUnit.Pos.X, mi.ResourceUnit.Pos.Y))).FirstOrDefault();

                    if (miningInfo != null)
                    {
                        dropOffPoint = miningInfo.DropOffPoint;
                        harvestPoint = miningInfo.HarvestPoint;
                    }
                }

                if (dropOffPoint == null) dropOffPoint = new Point2D { X = baseVector.X, Y = baseVector.Y };
                if (harvestPoint == null) harvestPoint = new Point2D { X = drone.UnitCalculation.Position.X, Y = drone.UnitCalculation.Position.Y };

                return ReturnMineralsAndGoBuild(drone, baseVector, dropOffPoint, harvestPoint, movePoint, frame, baseTag);
            }

            // otherwise issue immediate move (claim first)
            try { drone.Claimed = true; } catch { }
            try { drone.UnitRole = UnitRole.PreBuild; } catch { }

            var moveActions = drone.Order(frame, Abilities.MOVE, movePoint, 0, false, false, true);

            if (moveActions == null || !moveActions.Any())
            {
                Debug.WriteLine($"MineralWalkerMaw.PrepositionSelectedDrone: no move actions returned for drone {drone.UnitCalculation.Unit.Tag} (maybe spam protection or identical order)");
            }
            else
            {
                // Log detail of the actions returned for debugging (ability, tags, targetTag, world pos, queue)
                foreach (var a in moveActions)
                {
                    try
                    {
                        var uc = a?.ActionRaw?.UnitCommand;
                        if (uc != null)
                        {
                            var posStr = uc.TargetWorldSpacePos != null ? $"{uc.TargetWorldSpacePos.X:F2},{uc.TargetWorldSpacePos.Y:F02}" : "null";
                            Debug.WriteLine($"MineralWalkerMaw.PrepositionSelectedDrone: action ability={uc.AbilityId}, tags=[{string.Join(",", uc.UnitTags)}], targetTag={uc.TargetUnitTag}, pos={posStr}, queue={uc.QueueCommand}");
                        }
                    }
                    catch { }
                }
            }

            return (moveActions != null && moveActions.Any()) ? moveActions : null;
        }

        // New: accept bot + point and perform selection + preposition actions. Step5 will only call this with the point.
        // Updated: delegate to PrePositionBuilderTask if present (this ensures the action is sent through MicroManager pipeline).
        public static void PrepositionAt(DefaultSharkyBot bot, Point2D movePoint, int frame)
        {
            if (bot == null || movePoint == null) return;

            try
            {
                var baseData = bot.BaseData;
                var sharkyUnitData = bot.SharkyUnitData;
                var activeUnitData = bot.ActiveUnitData;

                if (baseData == null || sharkyUnitData == null || activeUnitData == null)
                {
                    Debug.WriteLine("MineralWalkerMaw.PrepositionAt: missing services on bot");
                    return;
                }

                var selected = SelectDroneForPreposition(baseData, sharkyUnitData, activeUnitData, movePoint, frame);
                if (selected == null)
                {
                    Debug.WriteLine("MineralWalkerMaw.PrepositionAt: no drone selected");
                    return;
                }

                // Delegate to the microtask if it exists so orders are sent via MicroManager
                if (bot.MicroTaskData != null && bot.MicroTaskData.TryGetValue(typeof(Sharky.MicroTasks.Macro.PrePositionBuilderTask).Name, out var mt))
                {
                    if (mt is Sharky.MicroTasks.Macro.PrePositionBuilderTask prePositionTask)
                    {
                        prePositionTask.SendBuilder(movePoint, frame);
                        Debug.WriteLine($"MineralWalkerMaw.PrepositionAt: delegated to PrePositionBuilderTask for drone {selected.UnitCalculation.Unit.Tag}");
                        return;
                    }
                }

                // Fallback: issue actions directly (original behavior)
                var actions = PrepositionSelectedDrone(selected, baseData, activeUnitData, movePoint, frame, sharkyUnitData);
                if (actions != null && actions.Any())
                {
                    Debug.WriteLine($"MineralWalkerMaw.PrepositionAt: issued {actions.Count} actions for drone {selected.UnitCalculation.Unit.Tag}");
                    foreach (var a in actions)
                    {
                        try
                        {
                            var uc = a?.ActionRaw?.UnitCommand;
                            if (uc != null)
                            {
                                var posStr = uc.TargetWorldSpacePos != null ? $"{uc.TargetWorldSpacePos.X:F2},{uc.TargetWorldSpacePos.Y:F2}" : "null";
                                Debug.WriteLine($"MineralWalkerMaw.PrepositionAt: action ability={uc.AbilityId}, tags=[{string.Join(",", uc.UnitTags)}], targetTag={uc.TargetUnitTag}, pos={posStr}, queue={uc.QueueCommand}");
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    Debug.WriteLine("MineralWalkerMaw.PrepositionAt: no actions issued (spam protection or helper chose not to)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MineralWalkerMaw.PrepositionAt exception: {ex.Message}");
            }
        }

        // Updated: prefer microtask path then fallback to issuing actions directly.
        public static ulong PrepositionDrone(DefaultSharkyBot bot, ActiveUnitData activeUnitData, Point2D target, int frame)
        {
            if (bot == null || target == null || activeUnitData == null) return 0;

            try
            {
                var baseData = bot.BaseData;
                var sharkyUnitData = bot.SharkyUnitData;
                var selected = SelectDroneForPreposition(baseData, sharkyUnitData, activeUnitData, target, frame);
                if (selected == null)
                {
                    Debug.WriteLine("MineralWalkerMaw.PrepositionDrone: no drone selected");
                    return 0;
                }

                // Delegate to microtask if present
                if (bot.MicroTaskData != null && bot.MicroTaskData.TryGetValue(typeof(Sharky.MicroTasks.Macro.PrePositionBuilderTask).Name, out var mt))
                {
                    if (mt is Sharky.MicroTasks.Macro.PrePositionBuilderTask prePositionTask)
                    {
                        prePositionTask.SendBuilder(target, frame);
                        Debug.WriteLine($"MineralWalkerMaw.PrepositionDrone: delegated to PrePositionBuilderTask for drone {selected.UnitCalculation.Unit.Tag}");
                        return selected.UnitCalculation.Unit.Tag;
                    }
                }

                // Fallback: issue actions directly (original behavior)
                var actions = PrepositionSelectedDrone(selected, baseData, activeUnitData, target, frame, sharkyUnitData);
                if (actions != null && actions.Any())
                {
                    Debug.WriteLine($"MineralWalkerMaw.PrepositionDrone: issued {actions.Count} actions for drone {selected.UnitCalculation.Unit.Tag}");
                    foreach (var a in actions)
                    {
                        try
                        {
                            var uc = a?.ActionRaw?.UnitCommand;
                            if (uc != null)
                            {
                                var posStr = uc.TargetWorldSpacePos != null ? $"{uc.TargetWorldSpacePos.X:F2},{uc.TargetWorldSpacePos.Y:F2}" : "null";
                                Debug.WriteLine($"MineralWalkerMaw.PrepositionDrone: action ability={uc.AbilityId}, tags=[{string.Join(",", uc.UnitTags)}], targetTag={uc.TargetUnitTag}, pos={posStr}, queue={uc.QueueCommand}");
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    Debug.WriteLine("MineralWalkerMaw.PrepositionDrone: no actions issued (spam protection or helper chose not to)");
                }

                return selected.UnitCalculation.Unit.Tag;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MineralWalkerMaw.PrepositionDrone exception: {ex.Message}");
                return 0;
            }
        }
    }
}