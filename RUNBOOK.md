# RUNBOOK

Все команды ниже выполняются из каталога `deploy/`. Рядом должен лежать заполненный файл `.env`.

## Подготовка

Первичный запуск:

```bash
cd deploy
cp .env.example .env
```

Подготовить каталог для backup на хосте:

```bash
sudo mkdir -p /opt/backups/mssql
sudo chown 10001:0 /opt/backups/mssql
```

## Статус сервисов

```bash
docker compose ps
```

Ожидаемо оба сервиса `app` и `mssql` должны быть в статусе `Up`.

## Логи

Логи приложения:

```bash
docker compose logs -f app
```

Логи SQL Server:

```bash
docker compose logs -f mssql
```

## Проверка HTTP endpoints

```bash
curl -fsS http://127.0.0.1:5000/health
curl -fsS http://127.0.0.1:5000/version
curl -i http://127.0.0.1:5000/db/ping
```

Ожидаемо:

- `/health` возвращает `200 OK` и JSON со `status: "ok"`.
- `/version` возвращает `200 OK` и JSON с версией из `APP_VERSION`.
- `/db/ping` возвращает `200 OK` и JSON со `status: "ok"`. Код `503` означает проблему с подключением к MSSQL.

## Обновление

1. Изменить `APP_IMAGE_TAG` в `.env` на нужный тег.
2. Выполнить:

```bash
docker compose pull app
docker compose up -d app
```

3. Проверить:

```bash
docker compose ps
curl -fsS http://127.0.0.1:5000/version
curl -i http://127.0.0.1:5000/db/ping
```

## Откат

1. Вернуть в `.env` предыдущее значение `APP_IMAGE_TAG`.
2. Выполнить:

```bash
docker compose pull app
docker compose up -d app
```

3. Повторно проверить `docker compose ps`, `/version` и `/db/ping`.

## Backup

Backup-файлы сохраняются на хосте в `/opt/backups/mssql`. Внутри контейнера тот же каталог доступен как `/var/opt/mssql/backup`.

Создать backup:

```bash
DB_NAME=$(grep '^DB_NAME=' .env | cut -d= -f2-)
BACKUP_FILE="${DB_NAME}_$(date +%F_%H-%M-%S).bak"
docker exec is-mssql /bin/bash -lc "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"\$MSSQL_SA_PASSWORD\" -Q \"BACKUP DATABASE [${DB_NAME}] TO DISK = N'/var/opt/mssql/backup/${BACKUP_FILE}' WITH INIT, COMPRESSION\""
ls -lh /opt/backups/mssql
```

## Восстановление

Восстановить backup:

```bash
DB_NAME=$(grep '^DB_NAME=' .env | cut -d= -f2-)
BACKUP_FILE="<backup-file>.bak"
docker exec is-mssql /bin/bash -lc "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"\$MSSQL_SA_PASSWORD\" -Q \"ALTER DATABASE [${DB_NAME}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [${DB_NAME}] FROM DISK = N'/var/opt/mssql/backup/${BACKUP_FILE}' WITH REPLACE; ALTER DATABASE [${DB_NAME}] SET MULTI_USER;\""
```

Проверить восстановление:

```bash
DB_NAME=$(grep '^DB_NAME=' .env | cut -d= -f2-)
docker exec is-mssql /bin/bash -lc "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"\$MSSQL_SA_PASSWORD\" -Q \"SELECT name, state_desc FROM sys.databases WHERE name = N'${DB_NAME}'\""
curl -i http://127.0.0.1:5000/db/ping
```
