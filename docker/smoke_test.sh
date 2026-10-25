#!/usr/bin/env bash
# Smoke test of the Docker image: scans a small test library and checks the OPDS catalog, downloads and the data folder
set -euo pipefail

IMAGE="$1"
NAME=tinyopds-test
PORT=8080
WORK="$(mktemp -d)"

python3 "$(dirname "$0")/make_test_books.py" "$WORK/books"
mkdir "$WORK/data"

cleanup() {
    echo "----- container log -----"
    docker logs "$NAME" 2>&1 || true
    docker rm -f "$NAME" > /dev/null 2>&1 || true
}
trap cleanup EXIT

docker run -d --name "$NAME" -p "$PORT:8080" \
    -v "$WORK/books:/books:ro" -v "$WORK/data:/data" \
    -e PUID="$(id -u)" -e PGID="$(id -g)" -e TINYOPDS_LANGUAGE=ru \
    "$IMAGE"

# Outputs are checked as strings, not with "command | grep -q": grep -q exits on the first match,
# the command may get SIGPIPE, and with pipefail the check fails although the text is there
has() { [[ "$1" == *"$2"* ]]; }

# 6 files: 5 books (one of them is a duplicate) and an invalid one, 4 books are saved
DONE="invalid: 1, total: 6, in DB: 4"
for i in $(seq 1 120); do
    LOG="$(docker logs "$NAME" 2>&1)"
    has "$LOG" "$DONE" && break
    sleep 2
done
has "$LOG" "$DONE" || { echo "FAILED: the scan didn't finish as expected"; exit 1; }
echo "Scan finished"

# The log is read in NAS web interfaces: plain lines only, no terminal control sequences
has "$LOG" $'\e' && { echo "FAILED: terminal control sequences in the log"; exit 1; }
echo "Log OK"

search() { curl -fsS "http://127.0.0.1:$PORT/opds/search?searchTerm=$1"; }

# "каштанка"
has "$(search "%D0%BA%D0%B0%D1%88%D1%82%D0%B0%D0%BD%D0%BA%D0%B0")" "<title>Каштанка</title>" \
    || { echo "FAILED: book not found by search"; exit 1; }

# "шинель": one copy, the newer one from the archive
SHINEL="$(search "%D1%88%D0%B8%D0%BD%D0%B5%D0%BB%D1%8C")"
[ "$(grep -c "<title>Шинель</title>" <<< "$SHINEL")" = 1 ] || { echo "FAILED: duplicate is not removed"; exit 1; }
has "$SHINEL" "11111111-0000-0000-0000-000000000005" || { echo "FAILED: wrong copy of the duplicate is kept"; exit 1; }
echo "Search and duplicates OK"

BOOK="http://127.0.0.1:$PORT/opds/download/11111111-0000-0000-0000-000000000003"
curl -fsS -o "$WORK/book.fb2" "$BOOK/fb2"
[ -s "$WORK/book.fb2" ] || { echo "FAILED: FB2 download is empty"; exit 1; }
curl -fsS -o "$WORK/book.epub" "$BOOK/epub"
[ "$(head -c 2 "$WORK/book.epub")" = "PK" ] || { echo "FAILED: EPUB conversion"; exit 1; }
echo "Downloads OK"

has "$(curl -fsS "http://127.0.0.1:$PORT/")" 'lang="ru"' || { echo "FAILED: web page"; exit 1; }
echo "Web page OK"

for f in books.sqlite TinyOPDS.config; do
    [ -f "$WORK/data/$f" ] || { echo "FAILED: $f is not in the data folder"; exit 1; }
    [ "$(stat -c %u "$WORK/data/$f")" = "$(id -u)" ] || { echo "FAILED: $f doesn't belong to PUID"; exit 1; }
done
echo "Data folder OK"

# A new archive copied while the server runs is added by the folder watcher: it is written with
# a temporary name in two parts with a pause, then renamed, as copying programs do
python3 "$(dirname "$0")/make_test_books.py" new-archive "$WORK/new.zip"
SIZE=$(stat -c %s "$WORK/new.zip")
head -c $((SIZE / 2)) "$WORK/new.zip" > "$WORK/books/copy.tmp"
sleep 5
tail -c +$((SIZE / 2 + 1)) "$WORK/new.zip" >> "$WORK/books/copy.tmp"
mv "$WORK/books/copy.tmp" "$WORK/books/fb2-200001-200001.zip"
# "пудель"
for i in $(seq 1 60); do
    has "$(search "%D0%BF%D1%83%D0%B4%D0%B5%D0%BB%D1%8C")" "<title>Белый пудель</title>" && break
    sleep 2
done
has "$(search "%D0%BF%D1%83%D0%B4%D0%B5%D0%BB%D1%8C")" "<title>Белый пудель</title>" \
    || { echo "FAILED: new archive is not added by the folder watcher"; exit 1; }
echo "Folder watcher OK"

# The scan command doesn't make the container exit (and restart again and again)
docker run -d --name "$NAME-scan" -v "$WORK/books:/books:ro" "$IMAGE" scan > /dev/null
sleep 10
[ "$(docker inspect -f '{{.State.Running}}' "$NAME-scan")" = true ] || { echo "FAILED: container exits on the scan command"; docker logs "$NAME-scan"; exit 1; }
docker rm -f "$NAME-scan" > /dev/null
echo "Scan command OK"

# Encrypted credentials for password protection, as the image description explains
CREDENTIALS="$(docker run --rm "$IMAGE" encred user1 password1 | tail -n 1)"
[[ "$CREDENTIALS" =~ ^[A-Za-z0-9+/=]{16,}$ ]] || { echo "FAILED: encred printed '$CREDENTIALS'"; exit 1; }
echo "Credentials encryption OK"

docker stop -t 30 "$NAME" > /dev/null
echo "Stopped with exit code $(docker inspect -f '{{.State.ExitCode}}' "$NAME")"

echo "Smoke test passed"
