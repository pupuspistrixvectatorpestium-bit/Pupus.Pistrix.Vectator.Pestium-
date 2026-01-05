using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SC2APIProtocol;
using Sharky;
using Sharky.DefaultBot;
using Sharky.Managers;

namespace Maw.Managers
{
    // Maw-friendly extension of the upstream BaseManager.
    // Adds mineral-patch classification while delegating main behavior to the base class.
    public class BaseManagerMaw : BaseManager
    {
        // Local references to services/data we need (base class fields are private so we must hold our own).
        readonly BaseData botBaseData;
        readonly SharkyUnitData botSharkyUnitData;
        readonly ActiveUnitData botActiveUnitData;
        readonly MapDataService botMapDataService;

        public Dictionary<BaseLocation, (List<Unit> NearMinerals, List<Unit> FarMinerals)> MineralPatchClassification { get; private set; }

        // Construct using the same dependencies as the original BaseManager
        public BaseManagerMaw(DefaultSharkyBot bot)
            : base(bot.SharkyUnitData, bot.ActiveUnitData, bot.SharkyPathFinder, bot.UnitCountService, bot.BaseData, bot.MapDataService)
        {
            // store local references for subclass logic (base stores its own private copies)
            botBaseData = bot.BaseData;
            botSharkyUnitData = bot.SharkyUnitData;
            botActiveUnitData = bot.ActiveUnitData;
            botMapDataService = bot.MapDataService;

            MineralPatchClassification = new Dictionary<BaseLocation, (List<Unit>, List<Unit>)>();
        }

        // Keep upstream initialization, then refresh classification
        public override void OnStart(ResponseGameInfo gameInfo, ResponseData data, ResponsePing pingResponse, ResponseObservation observation, uint playerId, string opponentId)
        {
            base.OnStart(gameInfo, data, pingResponse, observation, playerId, opponentId);
            RefreshMineralClassification();
        }

        // Call base OnFrame, then ensure classification stays up-to-date
        public override IEnumerable<SC2APIProtocol.Action> OnFrame(ResponseObservation observation)
        {
            var actions = base.OnFrame(observation);
            RefreshMineralClassification();
            return actions;
        }

        // Public helpers
        public List<Unit> GetNearMineralPatches(BaseLocation baseLocation)
        {
            return MineralPatchClassification.TryGetValue(baseLocation, out var lists) ? lists.NearMinerals : new List<Unit>();
        }

        public List<Unit> GetFarMineralPatches(BaseLocation baseLocation)
        {
            return MineralPatchClassification.TryGetValue(baseLocation, out var lists) ? lists.FarMinerals : new List<Unit>();
        }

        // Recompute classification for all known bases.
        // Uses BaseLocation.MiddleMineralLocation when available; otherwise computes a sensible center.
        void RefreshMineralClassification()
        {
            try
            {
                MineralPatchClassification.Clear();
                if (botBaseData?.BaseLocations == null) return;

                const float nearDistanceSquared = 36f; // 6 units squared

                foreach (var baseLocation in botBaseData.BaseLocations)
                {
                    if (baseLocation == null || baseLocation.MineralFields == null || !baseLocation.MineralFields.Any())
                    {
                        MineralPatchClassification[baseLocation] = (new List<Unit>(), new List<Unit>());
                        continue;
                    }

                    Vector2 middle;
                    if (baseLocation.MiddleMineralLocation != null)
                    {
                        middle = new Vector2(baseLocation.MiddleMineralLocation.X, baseLocation.MiddleMineralLocation.Y);
                    }
                    else
                    {
                        // fallback: average mineral positions
                        var vectors = baseLocation.MineralFields.Select(m => new Vector2(m.Pos.X, m.Pos.Y)).ToList();
                        var avg = new Vector2(vectors.Average(v => v.X), vectors.Average(v => v.Y));
                        middle = avg;
                    }

                    var near = baseLocation.MineralFields.Where(m => Vector2.DistanceSquared(new Vector2(m.Pos.X, m.Pos.Y), middle) <= nearDistanceSquared).ToList();
                    var far = baseLocation.MineralFields.Except(near).ToList();

                    MineralPatchClassification[baseLocation] = (near, far);
                }
            }
            catch (Exception)
            {
                // Keep manager flow stable; avoid throwing during classification.
            }
        }
    }
}