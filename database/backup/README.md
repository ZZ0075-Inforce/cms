# database/backup/

Where `CMS.bak` goes. `docker-compose.yml` mounts this directory read-only at `/backup` and the
`sql-init` service restores it into the `CMS` database.

```powershell
copy C:\workspace\bk\CMS.bak database\backup\
```

## Why the backup, and not the SQL scripts

`database/*.sql` **cannot build the database**. They are reference DDL exported from SSMS — zero
INSERTs, and they re-declare overlapping tables across files, so running them fails (see the golden
rules in `CLAUDE.md`). `CMS.bak` is the only source that reconstitutes the database, including the
`SysConfig.appConfig` row without which login returns 500.

## Why it is not in version control

The backup is ~30 MB and changes every time it is retaken; committing it would grow the repo without
bound and be painful to purge later. `database/backup/*.bak` is gitignored — only this README is
tracked.

Backup and restore tooling lives in `C:\workspace\bk\` (`refresh.ps1`, `restore.ps1`, `RESTORE.md`).
See `spec/docker-db.md` for the container workflow.
