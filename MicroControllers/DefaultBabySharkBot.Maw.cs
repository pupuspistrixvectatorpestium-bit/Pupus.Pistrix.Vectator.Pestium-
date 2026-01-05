using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Reflection;
using SC2APIProtocol;
using Sharky;
using Sharky.DefaultBot;
using Debug = System.Diagnostics.Debug;

namespace Maw.DefaultBabySharkBot
{
    public class DefaultBabySharkBot
    {
        // preserve the GameConnection so we can construct a DefaultSharkyBot on conversion
        public GameConnection GameConnection { get; }

        public SharkyOptions SharkyOptions { get; set; }
        public FrameToTimeConverter FrameToTimeConverter { get; set; }
        public List<IManager> Managers { get; set; }

        public DebugManager DebugManager { get; set; }
        public ReportingManager ReportingManager { get; set; }

        public UnitDataManager UnitDataManager { get; set; }
        public MapManager MapManager { get; set; }
        public UnitManager UnitManager { get; set; }
        public EnemyRaceManager EnemyRaceManager { get; set; }
        public BaseManager BaseManager { get; set; }
        public TargetingManager TargetingManager { get; set; }
        public MacroManager MacroManager { get; set; }
        public NexusManager NexusManager { get; set; }
        public OrbitalManager OrbitalManager { get; set; }
        public RallyPointManager RallyPointManager { get; set; }
        public SupplyDepotManager SupplyDepotManager { get; set; }
        public ShieldBatteryManager ShieldBatteryManager { get; set; }
        public PhotonCannonManager PhotonCannonManager { get; set; }
        public ChatManager ChatManager { get; set; }
        public MicroManager MicroManager { get; set; }
        public EnemyStrategyManager EnemyStrategyManager { get; set; }
        public BuildManager BuildManager { get; set; }
        public CameraManager CameraManager { get; set; }
        public AttackDataManager AttackDataManager { get; set; }

        public VespeneGasBuilder VespeneGasBuilder { get; set; }
        public UnitBuilder UnitBuilder { get; set; }
        public UpgradeResearcher UpgradeResearcher { get; set; }
        public SupplyBuilder SupplyBuilder { get; set; }
        public ProductionBuilder ProductionBuilder { get; set; }
        public TechBuilder TechBuilder { get; set; }
        public AddOnBuilder AddOnBuilder { get; set; }
        public BuildingMorpher BuildingMorpher { get; set; }
        public UnfinishedBuildingCompleter UnfinishedBuildingCompleter { get; set; }
        public CollisionCalculator CollisionCalculator { get; set; }
        public UpgradeDataService UpgradeDataService { get; set; }
        public BuildingDataService BuildingDataService { get; set; }
        public TrainingDataService TrainingDataService { get; set; }
        public AddOnDataService AddOnDataService { get; set; }
        public MorphDataService MorphDataService { get; set; }
        public MapDataService MapDataService { get; set; }
        public ChokePointService ChokePointService { get; set; }
        public ChokePointsService ChokePointsService { get; set; }
        public TargetPriorityService TargetPriorityService { get; set; }
        public BuildingService BuildingService { get; set; }
        public WallService WallService { get; set; }
        public TerranWallService TerranWallService { get; set; }
        public ProtossWallService ProtossWallService { get; set; }
        public BuildPylonService BuildPylonService { get; set; }
        public BuildDefenseService BuildDefenseService { get; set; }
        public BuildProxyService BuildProxyService { get; set; }
        public BuildAddOnSwapService BuildAddOnSwapService { get; set; }
        public ChatDataService ChatDataService { get; set; }
        public EnemyNameService EnemyNameService { get; set; }
        public EnemyPlayerService EnemyPlayerService { get; set; }
        public DefenseService DefenseService { get; set; }
        public EnemyAggressivityService EnemyAggressivityService { get; set; }
        public BuildMatcher BuildMatcher { get; set; }
        public RecordService RecordService { get; set; }
        public IBuildDecisionService BuildDecisionService { get; set; }
        public ProxyLocationService ProxyLocationService { get; set; }
        public RequirementService RequirementService { get; set; }
        public UnitCountService UnitCountService { get; set; }
        public DamageService DamageService { get; set; }
        public TargetingService TargetingService { get; set; }
        public AttackPathingService AttackPathingService { get; set; }
        public HarassPathingService HarassPathingService { get; set; }
        public ChatService ChatService { get; set; }
        public TagService TagService { get; set; }
        public DebugService DebugService { get; set; }
        public VersionService VersionService { get; set; }
        public UnitDataService UnitDataService { get; set; }
        public BuildingCancelService BuildingCancelService { get; set; }
        public BuildingRequestCancellingService BuildingRequestCancellingService { get; set; }
        public UpgradeRequestCancellingService UpgradeRequestCancellingService { get; set; }
        public UnitRequestCancellingService UnitRequestCancellingService { get; set; }
        public AreaService AreaService { get; set; }
        public WallDataService WallDataService { get; set; }
        public BaseToBasePathingService BaseToBasePathingService { get; set; }
        public SimCityService SimCityService { get; set; }
        public WorkerBuilderService WorkerBuilderService { get; set; }
        public CreepTumorPlacementFinder CreepTumorPlacementFinder { get; set; }

        public ActiveUnitData ActiveUnitData { get; set; }
        public MapData MapData { get; set; }
        public BuildOptions BuildOptions { get; set; }
        public MacroSetup MacroSetup { get; set; }
        public IBuildingPlacement ProtossBuildingPlacement { get; set; }
        public IBuildingPlacement WallOffPlacement { get; set; }
        public IBuildingPlacement TerranBuildingPlacement { get; set; }
        public IBuildingPlacement ProtossDefensiveGridPlacement { get; set; }
        public IBuildingPlacement ProtossProxyGridPlacement { get; set; }
        public IBuildingPlacement GatewayCannonPlacement { get; set; }
        public IBuildingPlacement ProtectNexusPylonPlacement { get; set; }
        public IBuildingPlacement ProtectNexusCannonPlacement { get; set; }
        public IBuildingPlacement ProtectNexusBatteryPlacement { get; set; }
        public IBuildingPlacement MissileTurretPlacement { get; set; }
        public IBuildingPlacement ZergBuildingPlacement { get; set; }
        public IBuildingPlacement ZergGridPlacement { get; set; }
        public IBuildingPlacement BuildingPlacement { get; set; }
        public StasisWardPlacement StasisWardPlacement { get; set; }
        public IBuildingBuilder BuildingBuilder { get; set; }
        public TerranSupplyDepotGridPlacement TerranSupplyDepotGridPlacement { get; set; }
        public TerranProductionGridPlacement TerranProductionGridPlacement { get; set; }
        public TerranTechGridPlacement TerranTechGridPlacement { get; set; }
        public ProtossPylonGridPlacement ProtossPylonGridPlacement { get; set; }
        public ProtossProductionGridPlacement ProtossProductionGridPlacement { get; set; }
        public ResourceCenterLocator ResourceCenterLocator { get; set; }
        public AttackData AttackData { get; set; }
        public IBuildingPlacement WarpInPlacement { get; set; }
        public IProducerSelector DefaultProducerSelector { get; set; }
        public IProducerSelector ZergProducerSelector { get; set; }
        public MacroData MacroData { get; set; }
        public Morpher Morpher { get; set; }
        public HttpClient HttpClient { get; set; }
        public ChatHistory ChatHistory { get; set; }
        public IPathFinder SharkyPathFinder { get; set; }
        public IPathFinder SharkySimplePathFinder { get; set; }
        public IPathFinder SharkyNearPathFinder { get; set; }
        public IPathFinder SharkyAdvancedPathFinder { get; set; }
        public IPathFinder NoPathFinder { get; set; }
        public IPathFinder SharkyWorkerScoutPathFinder { get; set; }
        public EnemyStrategyHistory EnemyStrategyHistory { get; set; }
        public ICounterTransitioner EmptyCounterTransitioner { get; set; }
        public MacroBalancer MacroBalancer { get; set; }
        public Dictionary<Race, BuildChoices> BuildChoices { get; set; }

        public IIndividualMicroController IndividualMicroController { get; set; }
        public MicroData MicroData { get; set; }
        public IMicroController MicroController { get; set; }
        public MicroTaskData MicroTaskData { get; set; }
        public ChronoData ChronoData { get; set; }
        public TargetingData TargetingData { get; set; }
        public BaseData BaseData { get; set; }
        public ActiveChatData ActiveChatData { get; set; }
        public EnemyData EnemyData { get; set; }
        public PerformanceData PerformanceData { get; set; }
        public SharkyUnitData SharkyUnitData { get; set; }
        public MineralWalker MineralWalker { get; set; }
        public UnitTypeBuildClassifications UnitTypeBuildClassifications { get; set; }

        public DefaultBabySharkBot(GameConnection gameConnection)
        {
            // store connection immediately
            GameConnection = gameConnection;

            // Construct the canonical default bot inside the Sharky assembly (this creates internal instances safely)
            var canonical = new DefaultSharkyBot(gameConnection);

            // Copy public properties from canonical to this instance using reflection.
            // This avoids directly referencing internal types from this assembly.
            var srcProps = typeof(DefaultSharkyBot).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var destType = this.GetType();
            foreach (var p in srcProps)
            {
                var destProp = destType.GetProperty(p.Name, BindingFlags.Public | BindingFlags.Instance);
                if (destProp != null && destProp.CanWrite)
                {
                    destProp.SetValue(this, p.GetValue(canonical));
                }
            }

            // Now apply your custom deltas — for example, replace MineralWalker with your new implementation
            MineralWalker = new MineralWalker(BaseData, SharkyUnitData, ActiveUnitData, MapDataService);

            // Request an immediate drone via MacroData so Production pipeline will build it ASAP.
            // This runs before managers' OnStart/OnFrame, so it's safe to set up early.
            try
            {
                if (MacroData != null)
                {
                    // Ensure DesiredUnitCounts dictionary exists
                    if (MacroData.DesiredUnitCounts == null)
                    {
                        MacroData.DesiredUnitCounts = new Dictionary<UnitTypes, int>();
                    }

                    // Determine desired count: at least 1 drone (or current + 1)
                    var desired = 1;
                    if (UnitCountService != null)
                    {
                        try
                        {
                            var current = UnitCountService.Count(UnitTypes.ZERG_DRONE);
                            desired = Math.Max(desired, current + 1);
                        }
                        catch
                        {
                            // fall back to 1 if counting fails
                            desired = 1;
                        }
                    }

                    MacroData.DesiredUnitCounts[UnitTypes.ZERG_DRONE] = desired;
                    Debug.WriteLine($"[DefaultBabySharkBot] Requested immediate Drone: desired={desired}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DefaultBabySharkBot] Failed to request immediate Drone: {ex.Message}");
            }

            // Any additional customizations belong here (small, incremental changes only).
        }

        // implicit conversion: create a DefaultSharkyBot and copy references so Sharky APIs accepting DefaultSharkyBot work
        public static implicit operator DefaultSharkyBot(DefaultBabySharkBot b)
        {
            if (b == null) return null;

            // construct the canonical default bot (heavy but safe)
            var dsb = new DefaultSharkyBot(b.GameConnection);

            // copy references for all public properties so callers see the same objects
            dsb.SharkyOptions = b.SharkyOptions;
            dsb.FrameToTimeConverter = b.FrameToTimeConverter;
            dsb.Managers = b.Managers;

            dsb.DebugManager = b.DebugManager;
            dsb.ReportingManager = b.ReportingManager;

            dsb.UnitDataManager = b.UnitDataManager;
            dsb.MapManager = b.MapManager;
            dsb.UnitManager = b.UnitManager;
            dsb.EnemyRaceManager = b.EnemyRaceManager;
            dsb.BaseManager = b.BaseManager;
            dsb.TargetingManager = b.TargetingManager;
            dsb.MacroManager = b.MacroManager;
            dsb.NexusManager = b.NexusManager;
            dsb.OrbitalManager = b.OrbitalManager;
            dsb.RallyPointManager = b.RallyPointManager;
            dsb.SupplyDepotManager = b.SupplyDepotManager;
            dsb.ShieldBatteryManager = b.ShieldBatteryManager;
            dsb.PhotonCannonManager = b.PhotonCannonManager;
            dsb.ChatManager = b.ChatManager;
            dsb.MicroManager = b.MicroManager;
            dsb.EnemyStrategyManager = b.EnemyStrategyManager;
            dsb.BuildManager = b.BuildManager;
            dsb.CameraManager = b.CameraManager;
            dsb.AttackDataManager = b.AttackDataManager;

            dsb.VespeneGasBuilder = b.VespeneGasBuilder;
            dsb.UnitBuilder = b.UnitBuilder;
            dsb.UpgradeResearcher = b.UpgradeResearcher;
            dsb.SupplyBuilder = b.SupplyBuilder;
            dsb.ProductionBuilder = b.ProductionBuilder;
            dsb.TechBuilder = b.TechBuilder;
            dsb.AddOnBuilder = b.AddOnBuilder;
            dsb.BuildingMorpher = b.BuildingMorpher;
            dsb.UnfinishedBuildingCompleter = b.UnfinishedBuildingCompleter;
            dsb.CollisionCalculator = b.CollisionCalculator;
            dsb.UpgradeDataService = b.UpgradeDataService;
            dsb.BuildingDataService = b.BuildingDataService;
            dsb.TrainingDataService = b.TrainingDataService;
            dsb.AddOnDataService = b.AddOnDataService;
            dsb.MorphDataService = b.MorphDataService;
            dsb.MapDataService = b.MapDataService;
            dsb.ChokePointService = b.ChokePointService;
            dsb.ChokePointsService = b.ChokePointsService;
            dsb.TargetPriorityService = b.TargetPriorityService;
            dsb.BuildingService = b.BuildingService;
            dsb.WallService = b.WallService;
            dsb.TerranWallService = b.TerranWallService;
            dsb.ProtossWallService = b.ProtossWallService;
            dsb.BuildPylonService = b.BuildPylonService;
            dsb.BuildDefenseService = b.BuildDefenseService;
            dsb.BuildProxyService = b.BuildProxyService;
            dsb.BuildAddOnSwapService = b.BuildAddOnSwapService;
            dsb.ChatDataService = b.ChatDataService;
            dsb.EnemyNameService = b.EnemyNameService;
            dsb.EnemyPlayerService = b.EnemyPlayerService;
            dsb.DefenseService = b.DefenseService;
            dsb.EnemyAggressivityService = b.EnemyAggressivityService;
            dsb.BuildMatcher = b.BuildMatcher;
            dsb.RecordService = b.RecordService;
            dsb.BuildDecisionService = b.BuildDecisionService;
            dsb.ProxyLocationService = b.ProxyLocationService;
            dsb.RequirementService = b.RequirementService;
            dsb.UnitCountService = b.UnitCountService;
            dsb.DamageService = b.DamageService;
            dsb.TargetingService = b.TargetingService;
            dsb.AttackPathingService = b.AttackPathingService;
            dsb.HarassPathingService = b.HarassPathingService;
            dsb.ChatService = b.ChatService;
            dsb.TagService = b.TagService;
            dsb.DebugService = b.DebugService;
            dsb.VersionService = b.VersionService;
            dsb.UnitDataService = b.UnitDataService;
            dsb.BuildingCancelService = b.BuildingCancelService;
            dsb.BuildingRequestCancellingService = b.BuildingRequestCancellingService;
            dsb.UpgradeRequestCancellingService = b.UpgradeRequestCancellingService;
            dsb.UnitRequestCancellingService = b.UnitRequestCancellingService;
            dsb.AreaService = b.AreaService;
            dsb.WallDataService = b.WallDataService;
            dsb.BaseToBasePathingService = b.BaseToBasePathingService;
            dsb.SimCityService = b.SimCityService;
            dsb.WorkerBuilderService = b.WorkerBuilderService;
            dsb.CreepTumorPlacementFinder = b.CreepTumorPlacementFinder;

            dsb.ActiveUnitData = b.ActiveUnitData;
            dsb.MapData = b.MapData;
            dsb.BuildOptions = b.BuildOptions;
            dsb.MacroSetup = b.MacroSetup;
            dsb.ProtossBuildingPlacement = b.ProtossBuildingPlacement;
            dsb.WallOffPlacement = b.WallOffPlacement;
            dsb.TerranBuildingPlacement = b.TerranBuildingPlacement;
            dsb.ProtossDefensiveGridPlacement = b.ProtossDefensiveGridPlacement;
            dsb.ProtossProxyGridPlacement = b.ProtossProxyGridPlacement;
            dsb.GatewayCannonPlacement = b.GatewayCannonPlacement;
            dsb.ProtectNexusPylonPlacement = b.ProtectNexusPylonPlacement;
            dsb.ProtectNexusCannonPlacement = b.ProtectNexusCannonPlacement;
            dsb.ProtectNexusBatteryPlacement = b.ProtectNexusBatteryPlacement;
            dsb.MissileTurretPlacement = b.MissileTurretPlacement;
            dsb.ZergBuildingPlacement = b.ZergBuildingPlacement;
            dsb.ZergGridPlacement = b.ZergGridPlacement;
            dsb.BuildingPlacement = b.BuildingPlacement;
            dsb.StasisWardPlacement = b.StasisWardPlacement;
            dsb.BuildingBuilder = b.BuildingBuilder;
            dsb.TerranSupplyDepotGridPlacement = b.TerranSupplyDepotGridPlacement;
            dsb.TerranProductionGridPlacement = b.TerranProductionGridPlacement;
            dsb.TerranTechGridPlacement = b.TerranTechGridPlacement;
            dsb.ProtossPylonGridPlacement = b.ProtossPylonGridPlacement;
            dsb.ProtossProductionGridPlacement = b.ProtossProductionGridPlacement;
            dsb.ResourceCenterLocator = b.ResourceCenterLocator;
            dsb.AttackData = b.AttackData;
            dsb.WarpInPlacement = b.WarpInPlacement;
            dsb.DefaultProducerSelector = b.DefaultProducerSelector;
            dsb.ZergProducerSelector = b.ZergProducerSelector;
            dsb.MacroData = b.MacroData;
            dsb.Morpher = b.Morpher;
            dsb.HttpClient = b.HttpClient;
            dsb.ChatHistory = b.ChatHistory;
            dsb.SharkyPathFinder = b.SharkyPathFinder;
            dsb.SharkySimplePathFinder = b.SharkySimplePathFinder;
            dsb.SharkyNearPathFinder = b.SharkyNearPathFinder;
            dsb.SharkyAdvancedPathFinder = b.SharkyAdvancedPathFinder;
            dsb.NoPathFinder = b.NoPathFinder;
            dsb.SharkyWorkerScoutPathFinder = b.SharkyWorkerScoutPathFinder;
            dsb.EnemyStrategyHistory = b.EnemyStrategyHistory;
            dsb.EmptyCounterTransitioner = b.EmptyCounterTransitioner;
            dsb.MacroBalancer = b.MacroBalancer;
            dsb.BuildChoices = b.BuildChoices;

            dsb.IndividualMicroController = b.IndividualMicroController;
            dsb.MicroData = b.MicroData;
            dsb.MicroController = b.MicroController;
            dsb.MicroTaskData = b.MicroTaskData;
            dsb.ChronoData = b.ChronoData;
            dsb.TargetingData = b.TargetingData;
            dsb.BaseData = b.BaseData;
            dsb.ActiveChatData = b.ActiveChatData;
            dsb.EnemyData = b.EnemyData;
            dsb.PerformanceData = b.PerformanceData;
            dsb.SharkyUnitData = b.SharkyUnitData;
            dsb.MineralWalker = b.MineralWalker;
            dsb.UnitTypeBuildClassifications = b.UnitTypeBuildClassifications;

            return dsb;
        }

        public SharkyBot CreateBot(List<IManager> managers, DebugService debugService)
        {
            return new SharkyBot(managers, debugService, FrameToTimeConverter, SharkyOptions, PerformanceData, ChatService, TagService);
        }

        public SharkyBot CreateBot()
        {
            return new SharkyBot(Managers, DebugService, FrameToTimeConverter, SharkyOptions, PerformanceData, ChatService, TagService);
        }
    }
}
