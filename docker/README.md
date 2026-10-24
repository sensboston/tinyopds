# TinyOPDS

Lightweight OPDS and web server for a home e-book library: FB2 and EPUB files and ZIP archives
(Flibusta and Librusec distributions included), web reader, search, conversion to EPUB and MOBI.
This image runs TinyOPDS CLI on Mono for NAS (Synology, QNAP, etc.) and other Linux hosts, amd64 and arm64.

Project, documentation and Windows version: https://github.com/sensboston/tinyopds

## Quick start

`docker-compose.yml` (Synology Container Manager: Project → Create, QNAP Container Station: Applications → Create):

```yaml
services:
  tinyopds:
    image: sensboston/tinyopds:latest
    container_name: tinyopds
    ports:
      - "8080:8080"
    volumes:
      - /volume1/books:/books:ro            # your book library
      - /volume1/docker/tinyopds:/data      # TinyOPDS config, database and log
    environment:
      - PUID=1000                           # user and group the data folder belongs to
      - PGID=1000
      - TZ=Europe/Moscow
      - TINYOPDS_LANGUAGE=ru
    restart: unless-stopped
```

Or the same with `docker run`:

```
docker run -d --name tinyopds -p 8080:8080 \
  -v /volume1/books:/books:ro -v /volume1/docker/tinyopds:/data \
  -e PUID=1000 -e PGID=1000 -e TZ=Europe/Moscow -e TINYOPDS_LANGUAGE=ru \
  --restart unless-stopped sensboston/tinyopds:latest
```

Then open `http://<NAS address>:8080/` in a browser, and add `http://<NAS address>:8080/opds` as an OPDS catalog
to a reader app (KOReader, Moon+ Reader, FBReader, etc.)

## Folders

| Container folder | What is there |
|---|---|
| `/books` | Book library: FB2, EPUB, ZIP archives, subfolders. Read only access is enough |
| `/data` | `TinyOPDS.config`, database `books.sqlite`, log. Keep it to keep the library between updates |

## Settings

| Variable | Default | Meaning |
|---|---|---|
| `PUID`, `PGID` | 1000 | User and group IDs the files in `/data` belong to (Synology: usually 1026 and 100, see `id` in SSH) |
| `TZ` | UTC | Time zone, e.g. `Europe/Moscow` |
| `TINYOPDS_SCAN_ON_START` | `true` | Scan the library on start: the first scan indexes all books, the next ones add only new books |
| `TINYOPDS_<SETTING>` | | Any setting of `TinyOPDS.config`, name in upper case |

Useful settings: `TINYOPDS_LANGUAGE` (`en`, `ru`, `uk`, `de`, `fr`, `es`, `pl`), `TINYOPDS_SERVERNAME` (library name),
`TINYOPDS_ROOTPREFIX` (OPDS catalog path, `opds` by default), `TINYOPDS_DARKTHEMEONWEB`, `TINYOPDS_ITEMSPEROPDSPAGE`,
`TINYOPDS_FILTERBOOKSBYINTERFACELANGUAGE`, `TINYOPDS_SAVELOGTODISK`. Settings can also be changed in `/data/TinyOPDS.config`
while the container is stopped; environment variables take precedence over the file.

Keep the container port 8080: to use another port, change the left part of `ports`, e.g. `"8090:8080"`.

### Password protection

Get the encrypted user list (one or more user/password pairs):

```
docker run --rm sensboston/tinyopds encred user1 password1 user2 password2
```

and set `TINYOPDS_USEHTTPAUTH=true` and `TINYOPDS_CREDENTIALS=<the printed line>`.

## Notes

- **First scan.** A large library (hundreds of thousands of books) takes hours on a NAS; the server works while scanning,
  the progress is shown in the container log every 30 seconds. Restarts only add new books.
- **Database from a PC.** The library can be scanned by TinyOPDS on Windows and `books.sqlite` copied to `/data`,
  if the archives are in the root of the library folder on both computers.
- **Updates.** Pull the new image (Container Manager shows that an update is available) and recreate the container;
  `/data` keeps the library. Some releases ask to rescan the library from scratch, see the release notes.
- **Docker Hub is not reachable?** The image is also published as `ghcr.io/sensboston/tinyopds`.

## Кратко по-русски

OPDS и веб-сервер для домашней библиотеки (FB2, EPUB, ZIP-архивы Флибусты и Либрусека). Папку с книгами подключите в `/books`,
папку для настроек и базы — в `/data`, укажите `PUID`/`PGID` пользователя NAS и `TINYOPDS_LANGUAGE=ru`.
Веб-интерфейс: `http://<адрес NAS>:8080/`, OPDS-каталог для читалок: `http://<адрес NAS>:8080/opds`.
Если Docker Hub недоступен, тот же образ есть в `ghcr.io/sensboston/tinyopds`.
Подробная инструкция для NAS: https://github.com/sensboston/tinyopds
