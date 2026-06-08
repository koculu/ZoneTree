# Backups

Backup strategy depends on the WAL mode, maintenance behavior, and whether your application can reconstruct data.

## Basic Rule

Back up the full ZoneTree data directory as a unit. It contains metadata, WAL files, and segment files that together define the database.

Do not copy one piece of the storage directory and assume it is enough.

## Incremental Backup

ZoneTree has WAL-related support for incremental backup scenarios. This is useful when you need to capture new write-ahead-log content between full backups.

If you use incremental backup, test restore end to end. Backup is only real when restore has been verified.

## Maintenance And Backups

Maintenance changes the physical storage shape by moving and merging segments. Coordinate backups so the copied state is consistent with the metadata and segment files.

For strict operational workflows, create an application-level backup window:

* pause writes if needed,
* wait for maintenance if needed,
* copy the database directory,
* resume normal operation.

## Rebuildable Data

Some ZoneTree deployments store indexes, caches, or derived views that can be rebuilt from another source. In those systems, backup may focus on the source of truth instead of the ZoneTree directory.

Use `No WAL` only when that data-loss boundary is intentional.
