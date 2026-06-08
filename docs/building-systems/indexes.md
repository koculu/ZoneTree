# Indexes

ZoneTree is a natural foundation for indexes because it stores keys in order and supports efficient range scans.

## Primary Index

A primary index can use the entity ID as the key:

```text
entityId -> entity
```

## Secondary Index

A secondary index usually encodes the indexed field into the key and stores the primary ID as part of the key or value.

```text
email:user@example.com -> userId
createdAt:2026-06-08T12:00:00Z:userId -> empty
```

## Composite Keys

Composite key layouts make range scans cheap:

```text
tenantId:status:createdAt:entityId
```

This lets you scan all records for one tenant and status in creation order.

## Updating Indexes

If a primary record and secondary index entries must change together, use transactions or design an idempotent repair/rebuild path.

If the index is rebuildable from source data, it can use faster WAL settings or even no WAL.

## Prefix Scans

ZoneTree does not impose a query language. Prefix scans come from key design plus iterator `Seek`.

Design keys so related records are adjacent.
