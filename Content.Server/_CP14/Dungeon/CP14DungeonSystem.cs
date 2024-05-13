using System.Threading;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Parallax;
using Content.Server.Procedural;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared._CP14.Dungeon;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Salvage.Expeditions;
using Content.Shared.Teleportation.Systems;
using FastAccessors;
using Robust.Server.Audio;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._CP14.Dungeon;

public sealed partial class CP14DungeonSystem : EntitySystem
{
    [Dependency] private readonly LinkedEntitySystem _link = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly DungeonSystem _dungeon = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly AnchorableSystem _anchorable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;



    private readonly JobQueue _dungeonGenQueue = new();
    private readonly List<(CP14SpawnDungeonLevelJob Job, CancellationTokenSource CancelToken)> _dungeonGenJobs = new();
    private const double DungeonGenTime = 0.002;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CP14DungeonEntranceComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<CP14StationDungeonDataComponent, StationPostInitEvent>(OnStationPostInit);
    }

    private void OnStationPostInit(Entity<CP14StationDungeonDataComponent> dungeonData, ref StationPostInitEvent args)
    {
        if (!TryComp<StationDataComponent>(dungeonData, out var stationData)) return;

        SpawnDungeon("TestProceduralLevel");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var currentTime = _timing.CurTime;
        _dungeonGenQueue.Process();

        foreach (var (job, cancelToken) in _dungeonGenJobs.ToArray())
        {
            switch (job.Status)
            {
                case JobStatus.Finished:
                    _dungeonGenJobs.Remove((job, cancelToken));
                    break;
            }
        }
    }

    private void OnActivateInWorld(Entity<CP14DungeonEntranceComponent> entrance, ref ActivateInWorldEvent args)
    {
        //Вообще тут должна быть генерация всего данжа целиком в будущем
        SpawnDungeon("TestProceduralLevel");
    }

    private void SpawnDungeon(ProtoId<CP14DungeonLevelPrototype> levelParams)
    {
        if (_station.GetStations().FirstOrNull() is not { } station)
            return;

        if (!TryComp<CP14StationDungeonDataComponent>(station, out var dunData))
            return;

        var cancelToken = new CancellationTokenSource();
        var job = new CP14SpawnDungeonLevelJob(
            DungeonGenTime,
            _anchorable,
            _atmos,
            _biome,
            EntityManager,
            _logManager,
            _mapManager,
            _map,
            _prototypeManager,
            _dungeon,
            _metaData,
            _random,
            levelParams,
            dunData,
            cancelToken.Token);

        _dungeonGenJobs.Add((job, cancelToken));
        _dungeonGenQueue.EnqueueJob(job);
    }
}
