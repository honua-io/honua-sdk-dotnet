-- honua-sdk-dotnet#341 source-import conformance fixture, version source-import-fixture.v1.
--
-- The live ArcGIS-REST source for the lossless import certification. Every value
-- here is chosen so that a lossy decode is observable, and expected.v1.json is
-- derived from these literals by hand (never from the server or the SDK):
--   * big_counter 9007199254740993 is 2^53 + 1; a double decode yields ...992.
--   * big_counter -9223372036854775807 is long.MinValue + 1.
--   * note '' versus NULL, and name NULL versus '', must stay distinct.
--   * objectid 2 carries a NULL geometry; the other rows carry PointZM with Z/M
--     ordinates that cannot be derived from X/Y.
--   * inspected_at / install_date include pre-1970 instants (negative epoch ms).
--   * objectids are non-contiguous (1-3, 101-122) so offset arithmetic and
--     ID batching are both observable.
-- Changing any literal requires bumping FIXTURE_VERSION and expected.v1.json.

CREATE SCHEMA IF NOT EXISTS cert341;

DROP TABLE IF EXISTS cert341.source_sites;

CREATE TABLE cert341.source_sites (
    objectid     bigint PRIMARY KEY,
    site_guid    uuid,
    name         text,
    note         text,
    inspected_at timestamptz,
    install_date date,
    amount       double precision,
    status_code  smallint,
    big_counter  bigint,
    geom         geometry(PointZM, 4326)
);

INSERT INTO cert341.source_sites VALUES
    (1, '6f1c2a4e-0b7d-4c1e-9a55-3e2f4b6d8c01', 'alpha', '', '2026-01-02T03:04:05.678Z', '2026-01-02',
     1.5, 1, 9007199254740993, ST_SetSRID(ST_MakePoint(-122.41, 37.77, 125.5, 42.0), 4326)),
    (2, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
    (3, 'a0b1c2d3-e4f5-4a6b-8c7d-9e0f1a2b3c4d', '', 'x', '1969-12-31T23:59:59Z', '1900-01-01',
     0.1, 2, -9223372036854775807, ST_SetSRID(ST_MakePoint(-122.40, 37.78, -3.25, 0.5), 4326));

INSERT INTO cert341.source_sites (objectid, name, amount, status_code, big_counter, geom)
SELECT 100 + g,
       'row-' || g,
       g * 0.25,
       (g % 3) + 1,
       4611686018427387904 + g,
       ST_SetSRID(ST_MakePoint(-122.45 + g * 0.001, 37.75 + g * 0.001, g, g * 10), 4326)
FROM generate_series(1, 22) AS g;
