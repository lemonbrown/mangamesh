# MangaMesh Test Coverage Plan

**Current state:** 8 test methods across 5 files (3 unit, 2 integration)
**Target:** Systematic coverage of all critical paths, with a prioritized rollout.

---

## Current Coverage Snapshot

| File | Tests | What's covered |
|------|-------|----------------|
| `ContentProtocolTests.cs` | 2 | Manifest fetch, DHT lookup + retrieval |
| `DhtManifestTests.cs` | 1 | DHT Store/FindValue round-trip |
| `ImportChapterServiceTests.cs` | 3 | Import pipeline (happy path + 2 error cases) |
| `GatewayIntegrationTests.cs` | 2 | E2E gateway fetch, chunk reassembly |
| `ImportChapterTests.cs` | 1 | Full import + tracker announce (real tracker) |

**Controllers tested:** 0 of 13+
**Core service classes tested:** ~3 of 50+

---

## Priority Tiers

### Tier 1 — Critical (security + correctness)

These protect against silent data corruption and authentication bypass.

#### `ManifestSigningServiceTests.cs` (unit)
New file: `tests/MangaMesh.Peer.Tests/Signing/ManifestSigningServiceTests.cs`

| Test | What it verifies |
|------|-----------------|
| `SignManifest_ProducesVerifiableSignature` | Round-trip: sign → verify returns true |
| `VerifySignedManifest_TamperedData_ReturnsFalse` | Bit-flip in manifest body fails verification |
| `VerifySignedManifest_WrongKey_ReturnsFalse` | Different public key rejects valid signature |
| `SerializeCanonical_IsStable` | Same manifest always produces identical bytes (deterministic JSON) |
| `SerializeCanonical_FieldOrderDoesNotMatter` | Field reordering still hashes identically |

#### `KeyPairServiceTests.cs` (unit) — extend existing
File: `tests/MangaMesh.Peer.Tests/Keys/KeyPairServiceTests.cs` (move from root)

| Test | What it verifies |
|------|-----------------|
| `SolveChallenge_ProducesVerifiableSignature` | Challenge bytes signed by private key, verify with public key |
| `SolveChallenge_DifferentChallenges_ProduceDifferentSignatures` | Signatures are challenge-bound |
| `Verify_ValidSignature_ReturnsTrue` | Symmetric with SolveChallenge |
| `Verify_TamperedPayload_ReturnsFalse` | Mutation detection |
| `GenerateKeyPair_ProducesDifferentKeysEachCall` | Entropy randomness |

#### `PublicKeyRegistryTests.cs` (unit)
New file: `tests/MangaMesh.Peer.Tests/Auth/PublicKeyRegistryTests.cs`

| Test | What it verifies |
|------|-----------------|
| `CreateChallenge_StoresChallengeForLaterVerification` | Challenge persisted between calls |
| `VerifyChallenge_ValidSignature_ReturnsTrue` | Full challenge-response round-trip |
| `VerifyChallenge_InvalidSignature_ReturnsFalse` | Rejects bad signatures |
| `VerifyChallenge_ExpiredChallenge_ReturnsFalse` | Time-bound challenges expire |
| `VerifyChallenge_UnknownChallenge_ReturnsFalse` | Unknown ID rejects cleanly |

---

### Tier 2 — Core Services (data integrity)

#### `BlobStoreTests.cs` (unit)
New file: `tests/MangaMesh.Peer.Tests/Storage/BlobStoreTests.cs`

| Test | What it verifies |
|------|-----------------|
| `WriteAsync_ThenReadAsync_RoundTrip` | Bytes in == bytes out |
| `WriteAsync_HashMismatch_Throws` | Content-addressed integrity enforced |
| `ExistsAsync_ExistingBlob_ReturnsTrue` | Presence check |
| `ExistsAsync_MissingBlob_ReturnsFalse` | Absence check |
| `DeleteAsync_RemovesBlob` | Deletion works |
| `WriteAsync_LargeBlob_HandledCorrectly` | Multi-MB write/read |
| `WriteAsync_Concurrent_NoDuplicates` | Concurrent writes same key are idempotent |

#### `ManifestStoreTests.cs` (unit)
New file: `tests/MangaMesh.Peer.Tests/Storage/ManifestStoreTests.cs`

| Test | What it verifies |
|------|-----------------|
| `SaveAsync_ThenLoadAsync_RoundTrip` | Manifest serialized and deserialized correctly |
| `LoadAsync_MissingHash_ReturnsNull` | Null on miss |
| `GetAllHashesAsync_ReturnsAllSaved` | Index is complete |
| `DeleteAsync_RemovesFromIndex` | Deletion reflected in GetAllHashes |
| `SaveAsync_DuplicateHash_OverwritesSafely` | Idempotent upsert |

#### `ChunkIngesterTests.cs` (unit)
New file: `tests/MangaMesh.Peer.Tests/Content/ChunkIngesterTests.cs`

| Test | What it verifies |
|------|-----------------|
| `IngestAsync_SmallFile_SingleChunk` | File below chunk threshold → 1 blob |
| `IngestAsync_LargeFile_MultipleChunks` | File above threshold → N blobs, sizes correct |
| `IngestAsync_ChunksAreContentAddressed` | Same bytes → same hashes |
| `IngestAsync_AllChunksStoredInBlobStore` | Every chunk exists after ingest |
| `IngestAsync_ReturnsCorrectChunkManifest` | Returned chunk list matches stored blobs |

#### `KBucketRoutingTableTests.cs` (unit)
New file: `tests/MangaMesh.Peer.Tests/Dht/KBucketRoutingTableTests.cs`

| Test | What it verifies |
|------|-----------------|
| `AddNode_PopulatesBucket` | Node appears after add |
| `AddNode_FullBucket_EjectsLeastRecent` | LRU eviction on bucket overflow |
| `FindClosest_ReturnsKClosestNodes` | Kademlia distance ordering |
| `FindClosest_EmptyTable_ReturnsEmpty` | Edge case |
| `UpdateNode_RefreshesRecency` | Seen node moves to bucket tail |
| `RemoveNode_PurgesFromBucket` | Deletion works |

#### `YamlBootstrapNodeProviderTests.cs` (unit)
New file: `tests/MangaMesh.Peer.Tests/Dht/YamlBootstrapNodeProviderTests.cs`

| Test | What it verifies |
|------|-----------------|
| `GetBootstrapNodes_ValidYaml_ParsesCorrectly` | Host/port parsed, node IDs decoded |
| `GetBootstrapNodes_MissingFile_ReturnsEmpty` | Graceful fallback |
| `GetBootstrapNodes_MalformedYaml_ThrowsOrReturnsEmpty` | Error handling |

#### `DhtRequestTrackerTests.cs` (unit)
New file: `tests/MangaMesh.Peer.Tests/Dht/DhtRequestTrackerTests.cs`

| Test | What it verifies |
|------|-----------------|
| `Register_ThenComplete_ResolvesTask` | Request lifecycle happy path |
| `Register_Timeout_ThrowsOrCancels` | Timeout propagates to caller |
| `Register_DuplicateId_Throws` | Request ID uniqueness enforced |
| `Cancel_PendingRequest_CancelsTask` | Cancellation propagates |

#### `DhtBootstrapTests.cs` (unit — two real nodes, loopback)
New file: `tests/MangaMesh.Peer.Tests/Dht/DhtBootstrapTests.cs`

Note: existing tests use `BootstrapAsync` only as **setup plumbing** — none assert the bootstrap outcome itself. These tests close that gap.

| Test | What it verifies |
|------|-----------------|
| `BootstrapAsync_SinglePeer_AddsToRoutingTable` | After bootstrap, the bootstrap node appears in the routing table |
| `BootstrapAsync_SinglePeer_PingSent` | PING/PONG exchanged — bootstrap node knows about us too |
| `BootstrapAsync_UnreachablePeer_DoesNotHang` | Unreachable address fails fast, does not block indefinitely |
| `BootstrapAsync_UnreachablePeer_RoutingTableStaysEmpty` | Failed peer not added to routing table |
| `BootstrapAsync_MultiplePeers_AllReachableAdded` | All live peers end up in routing table |
| `BootstrapAsync_MultiplePeers_UnreachableSkipped` | Mix of live/dead — only live peers added |
| `YamlBootstrapNodeProvider_OnStartup_ConnectsToParsedNodes` | Node started with YAML provider bootstraps against those addresses |

---

### Tier 3 — Source Strategies (OCP interfaces)

#### `ZipSourceReaderTests.cs` (unit)
New file: `tests/MangaMesh.Peer.Tests/Chapters/ZipSourceReaderTests.cs`

| Test | What it verifies |
|------|-----------------|
| `ReadImagesAsync_ValidZip_ReturnsAllImages` | All image entries extracted |
| `ReadImagesAsync_ZipWithNonImages_FiltersCorrectly` | Non-image entries excluded |
| `ReadImagesAsync_NestedZip_HandlesDepth` | Nested directory structure in zip |
| `ReadImagesAsync_EmptyZip_ReturnsEmpty` | Edge case |
| `ReadImagesAsync_CorruptZip_Throws` | Corrupt archive handled |

#### `DirectorySourceReaderTests.cs` (unit)
New file: `tests/MangaMesh.Peer.Tests/Chapters/DirectorySourceReaderTests.cs`

| Test | What it verifies |
|------|-----------------|
| `ReadImagesAsync_Directory_ReturnsImageFiles` | All images returned |
| `ReadImagesAsync_Mixed_FiltersNonImages` | Non-images excluded |
| `ReadImagesAsync_SortedByName` | Pages returned in filename order |

#### `DefaultImageFormatProviderTests.cs` (unit)
New file: `tests/MangaMesh.Peer.Tests/Chapters/DefaultImageFormatProviderTests.cs`

| Test | What it verifies |
|------|-----------------|
| `GetContentType_Jpeg_ReturnsCorrectMime` | MIME type detection |
| `GetContentType_Png_ReturnsCorrectMime` | PNG detection |
| `GetContentType_Webp_ReturnsCorrectMime` | WebP detection |
| `GetContentType_Unknown_ReturnsOctetStream` | Fallback MIME |

---

### Tier 4 — REST Controllers (API contract)

Use `Microsoft.AspNetCore.Mvc.Testing` + `WebApplicationFactory<Program>` for each API.

#### `KeysControllerTests.cs`
New file: `tests/MangaMesh.Peer.Tests/Api/KeysControllerTests.cs`

| Test | Endpoint | What it verifies |
|------|----------|-----------------|
| `GenerateKeyPair_Returns200WithKeys` | `POST /keys/generate` | Response shape: publicKey, privateKey fields present |
| `GetKeyPair_ExistingKey_Returns200` | `GET /keys/{id}` | Returns stored key pair |
| `GetKeyPair_Missing_Returns404` | `GET /keys/{id}` | 404 on unknown ID |
| `SolveChallenge_ValidInput_Returns200WithSignature` | `POST /keys/challenge/solve` | Signature field in response |
| `SolveChallenge_BadRequest_Returns400` | `POST /keys/challenge/solve` | Validation rejects malformed body |
| `VerifySignature_ValidSignature_Returns200True` | `POST /keys/verify` | Correct signature returns `true` |
| `VerifySignature_InvalidSignature_Returns200False` | `POST /keys/verify` | Bad signature returns `false` (not error) |

#### `NodeControllerTests.cs`
New file: `tests/MangaMesh.Peer.Tests/Api/NodeControllerTests.cs`

| Test | Endpoint | What it verifies |
|------|----------|-----------------|
| `GetNodeInfo_Returns200WithNodeId` | `GET /node` | NodeId field present |
| `GetConnectedPeers_Returns200` | `GET /node/peers` | Returns array (possibly empty) |

#### `SeriesControllerTests.cs`
New file: `tests/MangaMesh.Peer.Tests/Api/SeriesControllerTests.cs`

| Test | Endpoint | What it verifies |
|------|----------|-----------------|
| `Search_EmptyQuery_Returns200EmptyArray` | `GET /series?q=` | No crash on empty query |
| `Search_MatchingQuery_ReturnsSeries` | `GET /series?q=...` | Seeded series returned |
| `ReadChapter_ValidHash_ReturnsManifest` | `GET /series/chapter/{hash}` | Manifest JSON returned |
| `ReadChapter_UnknownHash_Returns404` | `GET /series/chapter/{hash}` | 404 on miss |

#### `SubscriptionsControllerTests.cs`
New file: `tests/MangaMesh.Peer.Tests/Api/SubscriptionsControllerTests.cs`

| Test | Endpoint | What it verifies |
|------|----------|-----------------|
| `GetSubscriptions_Returns200` | `GET /subscriptions` | Returns array |
| `Subscribe_ValidSeries_Returns201` | `POST /subscriptions` | Created response |
| `Unsubscribe_Returns204` | `DELETE /subscriptions/{id}` | No-content response |

---

### Tier 5 — Integration Tests (new scenarios)

Add to `tests/MangaMesh.IntegrationTests/`.

#### `EndToEndReadFlowTests.cs` ★ explicit requirement
New file: `tests/MangaMesh.IntegrationTests/EndToEndReadFlowTests.cs`

Full flow: Index API redirect → Gateway DHT lookup → Peer content fetch → page reassembly.

**Test topology**

```
[Bootstrap DhtNode]  ←─ bootstrapped by both Peer and Gateway
      ↑ / ↓
[Peer DhtNode]  ← has manifest + page blobs in BlobStore, announced to DHT
      ↑
[Gateway (WebApplicationFactory)]  ← DHT client only, no local content
      ↑
[Index API (WebApplicationFactory)]  ← issues redirects
```

**Setup steps (shared `[TestInitialize]`)**

1. Start a bare `DhtNode` on a loopback port as the bootstrap node
2. Start a peer `DhtNode` + real `BlobStore`; bootstrap peer → bootstrap node
3. On the peer: ingest a synthetic chapter (2 pages, 2 chunks each), store all blobs, `StoreAsync` the manifest hash
4. Start `GatewayApi` via `WebApplicationFactory`; bootstrap its internal DHT node → bootstrap node
5. Start `IndexApi` via `WebApplicationFactory` configured with the gateway base URL

| Test | What it verifies |
|------|-----------------|
| `GetChapterRead_IndexRedirectsToGateway` | `GET /api/series/{s}/chapter/{c}/manifest/{hash}/read` returns 302 with `Location: /gateway/api/read/{hash}` |
| `GetChapterRead_GatewayResolvesManifestViaDht` | Following the redirect, `GET /api/read/{hash}` returns 200 with `SeriesId`, `ChapterId`, `Files[]`, `Nodes[]` |
| `GetChapterRead_FilesListMatchesPeerManifest` | `Files[].Hash` values in the response exactly match the page hashes the peer stored |
| `GetFile_GatewayFetchesPageFromPeer` | `GET /content/file/{pageHash}` returns 200 with the correct reassembled bytes (SHA-256 of response == original image hash) |
| `GetFile_AllPages_ReassembledCorrectly` | Every page in the manifest is retrievable and byte-identical to what the peer stored |
| `GetChapterRead_NoPeerHasContent_Returns404` | With no peer announcing the hash, Gateway returns 404 |
| `GetFile_PeerDropsOffMidRead_Returns404` | Peer goes offline after manifest fetch; page blob request returns 404 gracefully |

**Assertion notes**
- `GetChapterRead_IndexRedirectsToGateway`: assert `response.StatusCode == 302` and `response.Headers.Location.ToString()` starts with `/gateway/api/read/`; do **not** auto-follow the redirect so the redirect itself is observable
- `GetFile_GatewayFetchesPageFromPeer`: compute `SHA256(responseBytes)` and compare to the hash used as `pageHash` in the URL — this proves content-addressed integrity survived the full round-trip

#### `TrackerAuthFlowTests.cs`
| Test | What it verifies |
|------|-----------------|
| `FullChallengeResponseFlow_RegistersKey` | CreateChallenge → SolveChallenge → VerifyChallenge with real tracker |
| `DoubleRegister_SameKey_IsIdempotent` | Duplicate registration doesn't corrupt state |
| `AnnounceManifest_WithValidAuth_Succeeds` | Challenge auth gates manifest announcement |
| `AnnounceManifest_WithInvalidSignature_Returns401` | Bad signature rejected at tracker |

#### `StorageMonitorTests.cs` (integration-ish, real temp dirs)
| Test | What it verifies |
|------|-----------------|
| `StorageUsage_ReflectsActualBlobSize` | DirSize matches bytes written to BlobStore |
| `PruneOldestBlobs_WhenOverQuota_FreesSpace` | Eviction runs when quota exceeded |
| `StorageLimit_Enforced_BlocksNewWrite` | Writes fail when at cap |

---

## Test Infrastructure Additions

### Builders / Fixtures

**`ManifestBuilder.cs`** — `tests/MangaMesh.Peer.Tests/Helpers/ManifestBuilder.cs`
Fluent builder for `SignedManifest` test objects. Avoids repeated boilerplate in signing tests.

**`TempDirectoryFixture.cs`** — `tests/MangaMesh.Peer.Tests/Helpers/TempDirectoryFixture.cs`
MSTest `[TestInitialize]`/`[TestCleanup]` helper that creates a unique temp dir per test and deletes on teardown. Required for BlobStore and ManifestStore tests.

**`InMemoryBlobStore.cs`** — `tests/MangaMesh.Peer.Tests/Helpers/InMemoryBlobStore.cs`
In-memory IBlobStore (dictionary-backed) for tests that need a real store but not disk I/O. Complement to existing `InMemoryKeyStore`.

**`WebAppFactory.cs`** — `tests/MangaMesh.Peer.Tests/Helpers/WebAppFactory.cs`
`WebApplicationFactory<Program>` subclass for ClientApi controller tests, wires real DI with in-memory store overrides.

---

## Rollout Order

```
Week 1  — Tier 1 (ManifestSigning, KeyPair crypto, PublicKeyRegistry)
Week 2  — Tier 2 (BlobStore, ManifestStore, ChunkIngester)
Week 3  — Tier 2 continued (KBucketRoutingTable, DhtRequestTracker, Bootstrap providers)
Week 4  — Tier 3 (source readers, image format provider)
Week 5  — Tier 4 (controller tests, WebApplicationFactory setup)
Week 6  — Tier 5 (new integration tests: tracker auth, storage monitor)
```

---

## Coverage Targets

| Tier | Tests to add | Expected new methods covered |
|------|-------------|------------------------------|
| 1 | ~15 | Crypto core, auth flow |
| 2 | ~37 | Storage, DHT infra |
| 3 | ~12 | OCP strategies |
| 4 | ~20 | All REST endpoints |
| 5 | ~13 | E2E flows |
| **Total** | **~97** | **~80% of current untested surface** |

---

## Test Naming Convention

Follow the pattern already used in `ImportChapterServiceTests.cs`:

```
MethodUnderTest_Condition_ExpectedOutcome
```

Examples:
- `SignManifest_ProducesVerifiableSignature`
- `WriteAsync_HashMismatch_Throws`
- `FindClosest_EmptyTable_ReturnsEmpty`
