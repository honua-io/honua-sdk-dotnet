#!/usr/bin/env bash
# Lossless ArcGIS service/layer import certification (honua-sdk-dotnet#341).
#
# Boots the digest-pinned honua-server candidate as a live ArcGIS REST source,
# publishes fixture/source-sites.v1.sql through the supported admin API, restores
# the requested Honua.Sdk.GeoServices version from nuget.org only, and runs the
# installed-package consumer. A second run with a deliberately corrupted oracle
# must fail, proving the cells detect loss rather than recognition.
#
# Usage: WORK_DIR=/path/on/real/disk OUT_DIR=evidence/<pin> ./run.sh
#
# Environment:
#   WORK_DIR             scratch directory (key ring, package cache, Caddy files); required
#   OUT_DIR              receipt/wire output directory; required
#   SDK_PACKAGE_VERSION  published Honua.Sdk.GeoServices version (default 1.7.0)
#   SERVER_IMAGE         digest-pinned candidate image
#   SERVER_SOURCE_SHA    source commit of SERVER_IMAGE
#   PREFIX               container/network name prefix (default c341)
#   SUBNET_PREFIX        /24 prefix for the private network (default 172.31.241)
#   PORT_PREFIX          two-digit host port prefix (default 28)
#   LOCAL_PACKAGE_DIR    optional folder of locally packed Honua.Sdk.* nupkgs; certifies
#                        those bytes instead of nuget.org (never a published-bytes receipt)
#   KEEP_STACK=1         leave the containers running afterwards
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORK_DIR="${WORK_DIR:?set WORK_DIR to a scratch directory on real disk}"
OUT_DIR="${OUT_DIR:?set OUT_DIR to the evidence output directory}"
SDK_PACKAGE_VERSION="${SDK_PACKAGE_VERSION:-1.7.0}"
SERVER_IMAGE="${SERVER_IMAGE:-ghcr.io/honua-io/honua-server@sha256:29974ee7b722e3ae15c3b891024e5e70800f412188aeccf5ec3d32d9dac675c1}"
SERVER_SOURCE_SHA="${SERVER_SOURCE_SHA:-548b7a5263da5a3f2381eb43f232687cdf92b0bf}"
P="${PREFIX:-c341}"
SUBNET="${SUBNET_PREFIX:-172.31.241}"
PP="${PORT_PREFIX:-28}"
NET="${P}-net"
CADDY_IP="${SUBNET}.10"
SERVICE=cert341_source
ADMIN_KEY="Cert341-$(openssl rand -hex 12)!A"
BASIC_USER=basic-source
BASIC_PASSWORD="$(openssl rand -hex 16)"

mkdir -p "$WORK_DIR" "$OUT_DIR/wire"
WORK_DIR="$(cd "$WORK_DIR" && pwd)"
OUT_DIR="$(cd "$OUT_DIR" && pwd)"

log() { printf '[%s] %s\n' "$(date -u +%H:%M:%S)" "$*"; }

teardown() {
  if [ "${KEEP_STACK:-0}" != 1 ]; then
    docker rm -f "$P-honua" "$P-tls" "$P-pg" "$P-redis" >/dev/null 2>&1 || true
    docker network rm "$NET" >/dev/null 2>&1 || true
    docker volume rm "$P-caddy-data" >/dev/null 2>&1 || true
  fi
}
trap teardown EXIT

admin() {
  local method="$1" path="$2" body="${3:-}"
  curl -sS --fail-with-body -X "$method" -H "X-API-Key: $ADMIN_KEY" -H 'Content-Type: application/json' \
    ${body:+-d "$body"} "http://localhost:${PP}341/api/v1/admin$path"
}

log "key ring and network"
mkdir -p "$WORK_DIR/keyring"
openssl req -x509 -newkey rsa:2048 -nodes -keyout "$WORK_DIR/keyring/key.pem" -out "$WORK_DIR/keyring/cert.pem" \
  -days 2 -subj "/CN=$P-keyring" >/dev/null 2>&1
openssl pkcs12 -export -inkey "$WORK_DIR/keyring/key.pem" -in "$WORK_DIR/keyring/cert.pem" \
  -out "$WORK_DIR/keyring/keyring.pfx" -passout pass:
chmod 0644 "$WORK_DIR/keyring/keyring.pfx"
teardown
docker network create --subnet "${SUBNET}.0/24" "$NET" >/dev/null

log "postgres and redis"
docker run -d --name "$P-pg" --network "$NET" -e POSTGRES_USER=honua_user -e POSTGRES_PASSWORD=honua_password \
  -e POSTGRES_DB=honua postgis/postgis:16-3.4 >/dev/null
docker run -d --name "$P-redis" --network "$NET" redis:7-alpine >/dev/null
for _ in $(seq 1 90); do
  docker logs "$P-pg" 2>&1 | grep -q 'PostgreSQL init process complete' \
    && docker exec "$P-pg" pg_isready -U honua_user -d honua >/dev/null 2>&1 && break
  sleep 2
done
sleep 3
docker exec "$P-pg" psql -q -v ON_ERROR_STOP=1 -U honua_user -d honua \
  -c 'CREATE EXTENSION IF NOT EXISTS postgis; CREATE EXTENSION IF NOT EXISTS postgis_raster;'

log "candidate $SERVER_IMAGE"
docker run -d --name "$P-honua" --network "$NET" -p "127.0.0.1:${PP}341:8080" \
  -e ConnectionStrings__DefaultConnection="Host=$P-pg;Database=honua;Username=honua_user;Password=honua_password" \
  -e ConnectionStrings__Redis="$P-redis:6379" \
  -e HONUA_ADMIN_PASSWORD="$ADMIN_KEY" \
  -e HostValidation__AllowedHosts__0=localhost -e HostValidation__AllowedHosts__1="$P-honua" \
  -e Security__ConnectionEncryption__MasterKey="$(openssl rand -hex 32)" \
  -e Security__ConnectionEncryption__Salt="$(openssl rand -base64 16)" \
  -e Kestrel__Endpoints__Http__Url="http://+:8080" \
  -e ForwardedHeaders__Enabled=true -e ForwardedHeaders__ForwardLimit=2 -e ForwardedHeaders__KnownProxies__0="$CADDY_IP" \
  -e Operations__SecretChannel__KeyRingCertificatePath=/keyring/keyring.pfx \
  -v "$WORK_DIR/keyring/keyring.pfx:/keyring/keyring.pfx:ro" \
  "$SERVER_IMAGE" >/dev/null

# Source variants in front of the same candidate:
#   :8443 TLS (generateToken requires HTTPS; token and bearer cells)
#   :8444 HTTP Basic-protected source
#   :8446 source that ignores resultOffset
#   :8447 throttled source (429 + Retry-After)
basic_hash="$(docker run --rm caddy:2 caddy hash-password --plaintext "$BASIC_PASSWORD" | tail -1)"
cat > "$WORK_DIR/Caddyfile" <<EOF
{
  admin off
  auto_https disable_redirects
}
https://localhost:8443 {
  tls internal
  reverse_proxy $P-honua:8080
}
http://:8444 {
  basic_auth {
    $BASIC_USER $basic_hash
  }
  request_header -Authorization
  reverse_proxy $P-honua:8080 {
    header_up X-API-Key "$ADMIN_KEY"
  }
}
http://:8446 {
  uri query -resultOffset
  reverse_proxy $P-honua:8080
}
http://:8447 {
  header Retry-After "1"
  header Content-Type "application/json"
  respond \`{"error":{"code":429,"message":"Too Many Requests","details":[]}}\` 429
}
EOF
docker run -d --name "$P-tls" --network "$NET" --ip "$CADDY_IP" \
  -p "127.0.0.1:${PP}443:8443" -p "127.0.0.1:${PP}444:8444" -p "127.0.0.1:${PP}446:8446" -p "127.0.0.1:${PP}447:8447" \
  -v "$WORK_DIR/Caddyfile:/etc/caddy/Caddyfile:ro" -v "$P-caddy-data:/data" caddy:2 >/dev/null

ready=""
for _ in $(seq 1 60); do
  ready="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://localhost:${PP}341/healthz/ready" || true)"
  [ "$ready" = 200 ] && break
  if [ "$(docker inspect -f '{{.State.Running}}' "$P-honua")" != true ]; then
    docker logs --tail 60 "$P-honua"
    exit 1
  fi
  sleep 3
done
[ "$ready" = 200 ] || { docker logs --tail 60 "$P-honua"; exit 1; }
image_id="$(docker inspect -f '{{.Image}}' "$P-honua")"
log "ready; running image id $image_id"

log "seed and publish fixture"
docker exec -i "$P-pg" psql -q -v ON_ERROR_STOP=1 -U honua_user -d honua < "$here/fixture/source-sites.v1.sql"
connection_id="$(admin POST /connections/ '{"name":"cert341-source","host":"'"$P"'-pg","port":5432,"databaseName":"honua","username":"honua_user","password":"honua_password","sslRequired":false,"sslMode":"Disable"}' | jq -er '.data.connectionId')"
layer_id="$(admin POST "/connections/$connection_id/layers/" '{"schema":"cert341","table":"source_sites","layerName":"source_sites","geometryColumn":"geom","geometryType":"Point","srid":4326,"primaryKey":"objectid","fields":["objectid","site_guid","name","note","inspected_at","install_date","amount","status_code","big_counter"],"serviceName":"'"$SERVICE"'"}' | jq -er '.data.layerId')"
expected_layer="$(jq -r .layerId "$here/fixture/expected.v1.json")"
[ "$layer_id" = "$expected_layer" ] || { echo "published layer $layer_id, oracle expects $expected_layer" >&2; exit 1; }
admin PUT "/connections/$connection_id/layers/$layer_id/enabled?serviceName=$SERVICE" '{"enabled":true}' >/dev/null
admin PUT "/metadata/layers/$layer_id/fields" '{"fields":[{"name":"status_code","alias":"Status","domain":{"name":"SiteStatus","type":"codedValue","codedValues":[{"code":1,"name":"Active"},{"code":2,"name":"Retired"},{"code":3,"name":"Planned"}]}}]}' >/dev/null
admin PUT "/services/$SERVICE/layers/$layer_id/metadata" '{"attribution":"honua-sdk-dotnet#341 fixture","timeInfo":{"startTimeField":"inspected_at","endTimeField":"install_date"}}' >/dev/null

log "capture source wire"
wire() { curl -sS --fail-with-body -H "X-API-Key: $ADMIN_KEY" "http://localhost:${PP}341/rest/services/$SERVICE/FeatureServer$1" | jq -S . > "$OUT_DIR/wire/$2"; }
wire '?f=json' service.json
wire "/$layer_id?f=json" layer.json
wire "/$layer_id/query?objectIds=1,2,3,101,122&outFields=*&orderByFields=objectid&f=json" query-default.json
wire "/$layer_id/query?objectIds=1,2,3,101,122&outFields=*&orderByFields=objectid&returnZ=true&returnM=true&f=json" query-return-zm.json

log "mint portal token over TLS"
docker exec "$P-tls" cat /data/caddy/pki/authorities/local/root.crt > "$WORK_DIR/caddy-root.crt"
portal_token=""
for _ in $(seq 1 20); do
  portal_token="$(curl -s --cacert "$WORK_DIR/caddy-root.crt" -X POST --data-urlencode username=admin \
    --data-urlencode "password=$ADMIN_KEY" -d client=requestip -d f=json \
    "https://localhost:${PP}443/sharing/rest/generateToken" | jq -r '.token // empty' || true)"
  [ -n "$portal_token" ] && break
  sleep 2
done
[ -n "$portal_token" ] || { echo "generateToken did not issue a token" >&2; exit 1; }

packages="$WORK_DIR/packages-$SDK_PACKAGE_VERSION"
rm -rf "$packages"
consumer="$here/consumer"
nuget_config="$consumer/NuGet.config"
if [ -n "${LOCAL_PACKAGE_DIR:-}" ]; then
  log "restore Honua.Sdk.GeoServices $SDK_PACKAGE_VERSION from local packages $LOCAL_PACKAGE_DIR (NOT published bytes)"
  package_source="local:$LOCAL_PACKAGE_DIR"
  published_hash="$(openssl dgst -sha512 -binary "$LOCAL_PACKAGE_DIR/Honua.Sdk.GeoServices.$SDK_PACKAGE_VERSION.nupkg" | base64 -w0)"
  nuget_config="$WORK_DIR/NuGet.local.config"
  cat > "$nuget_config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$LOCAL_PACKAGE_DIR" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="local"><package pattern="Honua.Sdk.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
EOF
else
  log "restore Honua.Sdk.GeoServices $SDK_PACKAGE_VERSION from nuget.org"
  package_source="https://api.nuget.org/v3/index.json"
  catalog="$(curl -sS --compressed "https://api.nuget.org/v3/registration5-gz-semver2/honua.sdk.geoservices/$SDK_PACKAGE_VERSION.json" | jq -er .catalogEntry)"
  published_hash="$(curl -sS "$catalog" | jq -er .packageHash)"
fi
# Per-run intermediate output keeps concurrent runs (and package versions) from sharing obj/.
intermediate=(-p:BaseIntermediateOutputPath="$WORK_DIR/obj/" -p:MSBuildProjectExtensionsPath="$WORK_DIR/obj/")
NUGET_PACKAGES="$packages" dotnet restore "$consumer/SourceImportCertification.csproj" \
  --configfile "$nuget_config" -p:HonuaSdkPackageVersion="$SDK_PACKAGE_VERSION" "${intermediate[@]}"
NUGET_PACKAGES="$packages" dotnet build "$consumer/SourceImportCertification.csproj" -c Release --no-restore \
  -p:HonuaSdkPackageVersion="$SDK_PACKAGE_VERSION" "${intermediate[@]}" -o "$WORK_DIR/consumer-$SDK_PACKAGE_VERSION"

run_consumer() {
  dotnet "$WORK_DIR/consumer-$SDK_PACKAGE_VERSION/SourceImportCertification.dll" \
    --expected "$here/fixture/expected.v1.json" \
    --base-url "http://localhost:${PP}341" \
    --tls-url "https://localhost:${PP}443" \
    --tls-root "$WORK_DIR/caddy-root.crt" \
    --basic-url "http://localhost:${PP}444" \
    --basic-user "$BASIC_USER" \
    --basic-password "$BASIC_PASSWORD" \
    --offset-ignoring-url "http://localhost:${PP}446" \
    --throttled-url "http://localhost:${PP}447" \
    --api-key "$ADMIN_KEY" \
    --portal-token "$portal_token" \
    --packages-root "$packages" \
    --package-version "$SDK_PACKAGE_VERSION" \
    --package-source "$package_source" \
    --expected-nupkg-sha512 "$published_hash" \
    --server-image "$SERVER_IMAGE" \
    --server-source-sha "$SERVER_SOURCE_SHA" \
    "$@"
}

log "certification run"
status=0
run_consumer --receipt "$OUT_DIR/receipt.json" | tee "$OUT_DIR/run.log" || status=$?

log "negative control (corrupted oracle must fail data.int64-precision)"
control=0
run_consumer --corrupt-oracle --receipt "$OUT_DIR/negative-control-receipt.json" > "$OUT_DIR/negative-control.log" || control=$?
control_cell="$(jq -r '.cells[] | select(.id == "data.int64-precision") | .verdict' "$OUT_DIR/negative-control-receipt.json")"
if [ "$control" -eq 0 ] || [ "$control_cell" != fail ]; then
  echo "negative control did not fail (exit $control, data.int64-precision=$control_cell)" >&2
  exit 2
fi
log "negative control failed as required (exit $control)"

jq -n --arg image "$SERVER_IMAGE" --arg imageId "$image_id" --arg sha "$SERVER_SOURCE_SHA" \
  --arg version "$SDK_PACKAGE_VERSION" --arg source "$package_source" --arg hash "$published_hash" --argjson status "$status" \
  '{serverImage: $image, runningImageId: $imageId, serverSourceSha: $sha, sdkPackageVersion: $version, sdkPackageSource: $source, nupkgSha512: $hash, certificationExit: $status}' \
  > "$OUT_DIR/run-identity.json"
exit "$status"
