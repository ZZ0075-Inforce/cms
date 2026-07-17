#!/bin/bash
# Restores CMS from /backup/CMS.bak once SQL Server is up. Skips when CMS already exists,
# so repeating `docker compose up` never wipes data.
#
# Invoked by the sql-init service in docker-compose.yml — not meant to be run by hand.

set -euo pipefail

SQLCMD='/opt/mssql-tools18/bin/sqlcmd'   # not on PATH; the full path is required
SERVER='sql'                             # the compose service name
BAK='/backup/CMS.bak'
DATA_DIR='/var/opt/mssql/data'

# -C is mandatory: tools18 forces encryption and the container's certificate is self-signed.
q() { "$SQLCMD" -S "$SERVER" -U sa -P "$MSSQL_SA_PASSWORD" -C "$@"; }

if [ ! -f "$BAK" ]; then
    echo "[init] $BAK not found."
    echo "[init] Put the backup at database/backup/CMS.bak — see database/backup/README.md"
    exit 1
fi

echo "[init] Waiting for SQL Server..."
for i in $(seq 1 60); do
    if q -Q 'SELECT 1' >/dev/null 2>&1; then break; fi
    if [ "$i" -eq 60 ]; then echo "[init] SQL Server did not come up in time."; exit 1; fi
    sleep 2
done

if [ "$(q -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name='CMS';" | tr -d '[:space:]')" = "1" ]; then
    echo "[init] CMS already exists — skipping restore."
    echo "[init] To rebuild from the backup: docker compose down -v && docker compose up -d"
    exit 0
fi

# The logical names are not the database name (this backup carries UComWeb / UComWeb_log), so read
# them from the backup instead of hard-coding — hard-coded names break on a different backup.
echo "[init] Reading logical file names from the backup..."
FILELIST=$(q -h -1 -W -s'|' -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK='$BAK';")
DATA_NAME=$(echo "$FILELIST" | awk -F'|' '$3=="D" {print $1; exit}')
LOG_NAME=$(echo "$FILELIST"  | awk -F'|' '$3=="L" {print $1; exit}')

if [ -z "$DATA_NAME" ] || [ -z "$LOG_NAME" ]; then
    echo "[init] Could not parse logical file names from the backup:"
    echo "$FILELIST"
    exit 1
fi
echo "[init] data=$DATA_NAME  log=$LOG_NAME"

echo "[init] Restoring CMS..."
q -b -Q "RESTORE DATABASE CMS FROM DISK='$BAK'
    WITH MOVE '$DATA_NAME' TO '$DATA_DIR/CMS.mdf',
         MOVE '$LOG_NAME'  TO '$DATA_DIR/CMS_log.ldf',
         REPLACE, RECOVERY, STATS=25;"

# The JWT signing key lives in SysConfig.appConfig and is read lazily, so a missing row would not
# surface until the first authenticated request 500s. Check it here instead.
TABLES=$(q -h -1 -W -d CMS -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables;" | tr -d '[:space:]')
APPCFG=$(q -h -1 -W -d CMS -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM SysConfig WHERE configKey='appConfig';" | tr -d '[:space:]')

echo "[init] Restored: $TABLES tables."
if [ "$APPCFG" = "1" ]; then
    echo "[init] SysConfig.appConfig present (JWT signing key source)."
else
    echo "[init] WARNING: SysConfig.appConfig missing — login will return 500."
fi
echo "[init] Connect with: Server=localhost,${SQL_PORT:-1433};Database=CMS;User Id=sa;..."
