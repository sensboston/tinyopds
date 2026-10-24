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

# 6 files: 5 books (one of them is a duplicate) and an invalid one, 4 books are saved
DONE="invalid: 1, total: 6, in DB: 4"
for i in $(seq 1 120); do
    docker logs "$NAME" 2>&1 | grep -q "$DONE" && break
    sleep 2
done
docker logs "$NAME" 2>&1 | grep -q "$DONE" || { echo "FAILED: the scan didn't finish as expected"; exit 1; }
echo "Scan finished"

search() { curl -fsS "http://127.0.0.1:$PORT/opds/search?searchTerm=$1"; }

# "каштанка"
search "%D0%BA%D0%B0%D1%88%D1%82%D0%B0%D0%BD%D0%BA%D0%B0" | grep -q "<title>Каштанка</title>" \
    || { echo "FAILED: book not found by search"; exit 1; }

# "шинель": one copy, the newer one from the archive
SHINEL="$(search "%D1%88%D0%B8%D0%BD%D0%B5%D0%BB%D1%8C")"
[ "$(echo "$SHINEL" | grep -c "<title>Шинель</title>")" = 1 ] || { echo "FAILED: duplicate is not removed"; exit 1; }
echo "$SHINEL" | grep -q "11111111-0000-0000-0000-000000000005" || { echo "FAILED: wrong copy of the duplicate is kept"; exit 1; }
echo "Search and duplicates OK"

BOOK="http://127.0.0.1:$PORT/opds/download/11111111-0000-0000-0000-000000000003"
curl -fsS -o "$WORK/book.fb2" "$BOOK/fb2"
[ -s "$WORK/book.fb2" ] || { echo "FAILED: FB2 download is empty"; exit 1; }
curl -fsS -o "$WORK/book.epub" "$BOOK/epub"
[ "$(head -c 2 "$WORK/book.epub")" = "PK" ] || { echo "FAILED: EPUB conversion"; exit 1; }
echo "Downloads OK"

curl -fsS "http://127.0.0.1:$PORT/" | grep -q 'lang="ru"' || { echo "FAILED: web page"; exit 1; }
echo "Web page OK"

for f in books.sqlite TinyOPDS.config; do
    [ -f "$WORK/data/$f" ] || { echo "FAILED: $f is not in the data folder"; exit 1; }
    [ "$(stat -c %u "$WORK/data/$f")" = "$(id -u)" ] || { echo "FAILED: $f doesn't belong to PUID"; exit 1; }
done
echo "Data folder OK"

docker stop -t 30 "$NAME" > /dev/null
echo "Stopped with exit code $(docker inspect -f '{{.State.ExitCode}}' "$NAME")"

echo "Smoke test passed"
