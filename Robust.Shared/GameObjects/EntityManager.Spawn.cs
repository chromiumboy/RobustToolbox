using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Robust.Shared.Containers;

namespace Robust.Shared.GameObjects;

public partial class EntityManager
{
    // This method will soon be marked as obsolete.
    public EntityUid SpawnEntity(string? protoName, EntityCoordinates coordinates, ComponentRegistry? overrides = null)
        => SpawnAttachedTo(protoName, coordinates, overrides);

    // This method will soon be marked as obsolete.
    public EntityUid SpawnEntity(string? protoName, MapCoordinates coordinates, ComponentRegistry? overrides = null)
        => Spawn(protoName, coordinates, overrides);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityUid[] SpawnEntitiesAttachedTo(EntityCoordinates coordinates, params string?[] protoNames)
    {
        var ents = new EntityUid[protoNames.Length];
        for (var i = 0; i < protoNames.Length; i++)
        {
            ents[i] = SpawnAttachedTo(protoNames[i], coordinates);
        }
        return ents;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityUid[] SpawnEntities(MapCoordinates coordinates, params string?[] protoNames)
    {
        var ents = new EntityUid[protoNames.Length];
        for (var i = 0; i < protoNames.Length; i++)
        {
            ents[i] = Spawn(protoNames[i], coordinates);
        }
        return ents;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityUid[] SpawnEntitiesAttachedTo(EntityCoordinates coordinates, List<string?> protoNames)
    {
        var ents = new EntityUid[protoNames.Count];
        for (var i = 0; i < protoNames.Count; i++)
        {
            ents[i] = SpawnAttachedTo(protoNames[i], coordinates);
        }
        return ents;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityUid[] SpawnEntities(MapCoordinates coordinates, List<string?> protoNames)
    {
        var ents = new EntityUid[protoNames.Count];
        for (var i = 0; i < protoNames.Count; i++)
        {
            ents[i] = Spawn(protoNames[i], coordinates);
        }
        return ents;
    }

    public virtual EntityUid SpawnAttachedTo(string? protoName, EntityCoordinates coordinates, ComponentRegistry? overrides = null)
    {
        if (!coordinates.IsValid(this))
            throw new InvalidOperationException($"Tried to spawn entity {protoName} on invalid coordinates {coordinates}.");

        var entity = CreateEntityUninitialized(protoName, coordinates, overrides);
        InitializeAndStartEntity(entity, coordinates.GetMapId(this));
        return entity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityUid Spawn(string? protoName = null, ComponentRegistry? overrides = null)
        => Spawn(protoName, MapCoordinates.Nullspace, overrides);

    public virtual EntityUid Spawn(string? protoName, MapCoordinates coordinates, ComponentRegistry? overrides = null)
    {
        var entity = CreateEntityUninitialized(protoName, coordinates, overrides);
        InitializeAndStartEntity(entity, coordinates.MapId);
        return entity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityUid SpawnAtPosition(string? protoName, EntityCoordinates coordinates, ComponentRegistry? overrides = null)
        => Spawn(protoName, coordinates.ToMap(this, _xforms), overrides);

    public bool TrySpawnNextTo(
        string? protoName,
        EntityUid target,
        [NotNullWhen(true)] out EntityUid? uid,
        TransformComponent? xform = null,
        ComponentRegistry? overrides = null)
    {
        uid = null;
        if (!TransformQuery.Resolve(target, ref xform))
            return false;

        if (!xform.ParentUid.IsValid())
            return false;

        if (!MetaQuery.TryGetComponent(target, out var meta))
            return false;

        if ((meta.Flags & MetaDataFlags.InContainer) == 0)
        {
            uid = SpawnAttachedTo(protoName, xform.Coordinates, overrides);
            return true;
        }

        if (!TryGetComponent(xform.ParentUid, out ContainerManagerComponent? containerComp))
            return false;

        foreach (var container in containerComp.Containers.Values)
        {
            if (!container.Contains(target))
                continue;

            uid = Spawn(protoName, overrides);
            if (container.Insert(uid.Value, this))
                return true;

            DeleteEntity(uid.Value);
            uid = null;
            return false;
        }

        return false;
    }

    public bool TrySpawnInContainer(
        string? protoName,
        EntityUid containerUid,
        string containerId,
        [NotNullWhen(true)] out EntityUid? uid,
        ContainerManagerComponent? containerComp = null,
        ComponentRegistry? overrides = null)
    {
        uid = null;
        if (containerComp == null && !TryGetComponent(containerUid, out containerComp))
            return false;

        if (!containerComp.Containers.TryGetValue(containerId, out var container))
            return false;

        uid = Spawn(protoName, overrides);

        if (container.Insert(uid.Value, this))
            return true;

        DeleteEntity(uid.Value);
        uid = null;
        return false;
    }

    public EntityUid SpawnNextToOrDrop(string? protoName, EntityUid target, TransformComponent? xform = null, ComponentRegistry? overrides = null)
    {
        xform ??= TransformQuery.GetComponent(target);
        if (!xform.ParentUid.IsValid())
            return Spawn(protoName);

        var uid = Spawn(protoName, overrides);
        _xforms.DropNextTo(uid, target);
        return uid;
    }

    public EntityUid SpawnInContainerOrDrop(
        string? protoName,
        EntityUid containerUid,
        string containerId,
        TransformComponent? xform = null,
        ContainerManagerComponent? containerComp = null,
        ComponentRegistry? overrides = null)
    {
        var uid = Spawn(protoName, overrides);

        if ((containerComp == null && !TryGetComponent(containerUid, out containerComp))
             || !containerComp.Containers.TryGetValue(containerId, out var container)
             || !container.Insert(uid, this))
        {

            xform ??= TransformQuery.GetComponent(containerUid);
            if (xform.ParentUid.IsValid())
                _xforms.DropNextTo(uid, (containerUid, xform));
        }

        return uid;
    }
}
