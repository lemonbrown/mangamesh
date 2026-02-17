# SOLID + DRY Refactor Plan — MangaMesh

## Context

The codebase works but has accumulated several SOLID and DRY violations that make it hard to test,
extend, and reason about. The violations fall into five clear themes, addressed below in priority
order (highest impact / lowest risk first).

---

## Violation Inventory

### Single Responsibility (SRP)
| Class | Problem |
|---|---|
| `DhtNode` (835 lines) | Manages routing table, dispatches messages, tracks pending requests, runs maintenance loops, handles content requests, and serializes/signs DHT messages — at least 5 distinct responsibilities |
| `ImportChapterService.ImportAsync` | Reads source files (3 branches), detects MIME types, orchestrates chunking, builds manifests, composes titles, signs, performs challenge-response auth, and announces to tracker |
| `ImportChapterService.AnnounceWithRetryAsync` | Auth challenge flow (create → sign → authorize → announce) is an embedded sub-protocol unrelated to chapter importing |
| `KeyPairService` | Key generation, key persistence via store, AND signature verification — generation and verification are separate concerns |
| `ManifestSigningService` | Static class with no DI seam; also mixes canonical serialization logic with cryptographic signing |

### Open/Closed (OCP)
| Class | Problem |
|---|---|
| `ImportChapterService` | `if (Directory.Exists) … else if (ext == ".zip" \|\| ext == ".cbz") … else throw` — adding a new source type (e.g. `.rar`, `.tar`) requires modifying the service |
| `ImportChapterService` | `GetMimeType` and `IsImageFile` are private switch/if chains — adding a new image format requires a code edit |
| `DhtNode` | Message handling driven by type checks rather than registered handlers — new message types require class edits |

### Interface Segregation (ISP)
| Interface | Problem |
|---|---|
| `ITrackerClient` (12 methods) | Spans 5 distinct concerns: peer discovery, node announcement, series registration, manifest publication, and challenge auth. Every consumer (and every mock) must know about all 12 methods |

### Dependency Inversion (DIP)
| Class | Problem |
|---|---|
| `KeyStore` | `AppContext.BaseDirectory + "\\data\\keys\\keys.json"` — hardcoded Windows path, no injectable abstraction |
| `DhtNode` | Bootstrap nodes loaded from `AppDomain.CurrentDomain.BaseDirectory/config/bootstrap_nodes.yml` — hardcoded, no `IBootstrapNodeProvider` |
| `ManifestStore` | `string rootDirectory` passed raw — not via `IOptions<T>` |
| `BlobStore` | `string root` passed raw — not via `IOptions<T>` |
| `ImportChapterService` | Calls `File.OpenRead()` directly, no filesystem abstraction |
| `ManifestSigningService` | Static class — cannot be injected or substituted |

### DRY
| Location | Problem |
|---|---|
| `KeyPairService.SolveChallenge`, `ImportChapterService.AnnounceWithRetryAsync`, `PublicKeyRegistry` | `Console.WriteLine` used for logging throughout — should be `ILogger<T>` |
| `ImportChapterService` | `GetMimeType` / `IsImageFile` — format knowledge in two private static methods that could diverge |
| `InMemoryKeyStore` | Copy-pasted verbatim in `ContentProtocolTests.cs` and `DhtManifestTests.cs` |
| `CreateNode()` factory | Copy-pasted verbatim in `ContentProtocolTests.cs` and `DhtManifestTests.cs` |

---

## Refactoring Steps

---

### Step 1 — DIP: Introduce `IOptions<T>` for all configuration (foundational)

**Why first**: Every subsequent step depends on being able to inject configuration cleanly.
All current hardcoded constants and paths move into typed options classes.

Create options records in `src/MangaMesh.Peer.Core/Configuration/`:

```
BlobStoreOptions     { string RootPath; long MaxStorageBytes = 5_368_709_120 }
ManifestStoreOptions { string RootPath }
KeyStoreOptions      { string KeyFilePath }
DhtOptions           { string BootstrapNodesPath; int MessageTimeoutMs = 2000;
                       int MaxMessageSizeBytes = 10_485_760; int ChunkSizeBytes = 262_144 }
```

**Files to change:**
- `src/MangaMesh.Peer.Core/Blob/BlobStore.cs` — replace `string root` ctor param with `IOptions<BlobStoreOptions>`
- `src/MangaMesh.Peer.Core/Manifests/ManifestStore.cs` — replace `string rootDirectory` with `IOptions<ManifestStoreOptions>`
- `src/MangaMesh.Peer.Core/Keys/KeyStore.cs` — replace hardcoded `AppContext.BaseDirectory + "\\data\\keys\\keys.json"` with `IOptions<KeyStoreOptions>`
- `src/MangaMesh.Peer.Core/Storage/StorageMonitorService.cs` — replace hardcoded 5 GB with `BlobStoreOptions.MaxStorageBytes`
- `src/MangaMesh.Peer.Core/Content/ChunkIngester.cs` — replace hardcoded 256 KB with `DhtOptions.ChunkSizeBytes`
- `src/MangaMesh.Peer.ClientApi/Program.cs`, `src/MangaMesh.Peer.GatewayApi/Program.cs` — wire `Configure<T>` from `appsettings.json`

---

### Step 2 — DRY: Replace `Console.WriteLine` with `ILogger<T>` everywhere

**Why second**: Mechanical, low-risk, improves observability, and unblocks proper log-level control.
Debug noise currently always prints to stdout in production.

**Files to change** (add `ILogger<T>` ctor param, replace every `Console.WriteLine`):
- `src/MangaMesh.Peer.Core/Keys/KeyPairService.cs` — 10+ Console calls (signing debug, self-verify output) → `LogDebug`
- `src/MangaMesh.Peer.Core/Chapters/ImportChapterService.cs` — warning in `AnnounceWithRetryAsync` → `LogWarning`
- `src/MangaMesh.Shared/Services/PublicKeyRegistry.cs` — debug output → `LogDebug`
- `src/MangaMesh.Shared/Services/MangaDexMetadataProvider.cs` — error logging → `LogError`

---

### Step 3 — ISP: Split `ITrackerClient` into focused interfaces

**Why third**: `ITrackerClient` has 12 methods across 5 unrelated concerns. Every consumer
(and every test mock) must know about all 12. Splitting it lets each class declare only
the dependency it actually needs.

**New interfaces** in `src/MangaMesh.Peer.Core/Tracker/`:

```csharp
// Read-only peer discovery
public interface IPeerLocator
{
    Task<List<PeerInfo>> GetPeersForManifestAsync(string manifestHash);
    Task<PeerInfo?> GetPeerAsync(string seriesId, string chapterId, string manifestHash);
}

// Node-level sync with tracker
public interface INodeAnnouncer
{
    Task<bool> PingAsync(string nodeId, string manifestSetHash, int count);
    Task AnnounceAsync(AnnounceRequest request);
    Task<bool> CheckNodeExistsAsync(string nodeId);
}

// Series catalog operations
public interface ISeriesRegistry
{
    Task<(string SeriesId, string Title)> RegisterSeriesAsync(ExternalMetadataSource source, string externalMangaId);
    Task<IEnumerable<SeriesSummaryResponse>> SearchSeriesAsync(string query, string? sort = null, string[]? ids = null);
    Task<TrackerStats> GetStatsAsync();
}

// Manifest publication
public interface IManifestAnnouncer
{
    Task AnnounceManifestAsync(AnnounceManifestRequest announcement, CancellationToken ct = default);
    Task AuthorizeManifestAsync(AuthorizeManifestRequest request);
}

// Challenge-response auth
public interface ITrackerChallengeClient
{
    Task<KeyChallengeResponse> CreateChallengeAsync(string publicKeyBase64);
    Task<KeyVerificationResponse> VerifyChallengeAsync(string publicKeyBase64, string challengeId, string signatureBase64);
}
```

`TrackerClient` implements all five — no behaviour changes, just interface assignment.

Keep `ITrackerClient : IPeerLocator, INodeAnnouncer, ISeriesRegistry, IManifestAnnouncer, ITrackerChallengeClient`
as a convenience aggregator for bootstrapping code that legitimately needs the full object.
All production constructor injections switch to the narrowest interface needed:

| Consumer | Old dependency | New dependency |
|---|---|---|
| `ImportChapterService` | `ITrackerClient` | `ISeriesRegistry`, `IManifestAnnouncer`, `ITrackerChallengeClient` |
| `ReplicationService` | `ITrackerClient` | `INodeAnnouncer` |
| `DhtNode` | `ITrackerClient` | `INodeAnnouncer` |
| Gateway query handlers | `ITrackerClient` | `IPeerLocator` |

---

### Step 4 — SRP + DRY: Extract `ITrackerPublisher` from `ImportChapterService`

`AnnounceWithRetryAsync` inside `ImportChapterService` is a complete sub-protocol:

1. Load identity keys
2. Request a challenge from the tracker
3. Sign the challenge with the private key
4. Authorize the manifest
5. Announce the manifest

This is tracker authentication logic, not chapter-import logic. It will be needed by
any future service that publishes content, so it belongs in its own class.

**New interface + class** in `src/MangaMesh.Peer.Core/Tracker/`:

```csharp
public interface ITrackerPublisher
{
    Task PublishManifestAsync(AnnounceManifestRequest request, CancellationToken ct = default);
}

public class TrackerPublisher : ITrackerPublisher
{
    // Constructor: ITrackerChallengeClient, IManifestAnnouncer, IKeyStore,
    //              IKeyPairService, ILogger<TrackerPublisher>
    // Body: AnnounceWithRetryAsync moved here verbatim, then cleaned up
}
```

`ImportChapterService` after this step:
- **Drops**: `IKeyStore`, `IKeyPairService`, challenge/authorize methods from `ITrackerClient`
- **Gains**: `ITrackerPublisher`
- Constructor goes from 7 parameters to 6

---

### Step 5 — OCP: Introduce `IChapterSourceReader` strategy for import sources

`ImportChapterService.ImportAsync` has a 3-branch `if/else if/else` on source type
(directory / .zip / .cbz / unsupported). Adding `.rar` or `.tar.gz` means editing the service.

**New interface** in `src/MangaMesh.Peer.Core/Chapters/`:

```csharp
public interface IChapterSourceReader
{
    bool CanRead(string sourcePath);
    IAsyncEnumerable<(string name, Stream content)> ReadFilesAsync(string sourcePath, CancellationToken ct);
}
```

**Concrete implementations** (new files):
- `DirectorySourceReader` — matches when `Directory.Exists(path)`
- `ZipSourceReader` — matches `.zip` and `.cbz` extensions

**New interface** for MIME / format detection (also fixes DRY — two private static methods that could diverge):

```csharp
public interface IImageFormatProvider
{
    bool IsSupported(string filename);
    string GetMimeType(string filename);
}

public class DefaultImageFormatProvider : IImageFormatProvider
{
    // Moves the existing switch expressions from ImportChapterService
}
```

`ImportChapterService` after this step:
- Constructor gains `IEnumerable<IChapterSourceReader> sourceReaders` + `IImageFormatProvider imageFormats`
- `ImportAsync` replaces the `if/else if` branch with:
  ```csharp
  var reader = sourceReaders.FirstOrDefault(r => r.CanRead(request.SourcePath))
      ?? throw new NotSupportedException($"No reader supports: {request.SourcePath}");
  await foreach (var (name, stream) in reader.ReadFilesAsync(request.SourcePath, ct)) { ... }
  ```
- The two private static methods (`GetMimeType`, `IsImageFile`) are deleted

Adding a new source format = create a new `IChapterSourceReader` + register it in DI. Zero edits to `ImportChapterService`.

---

### Step 6 — SRP + DIP: Make `ManifestSigningService` injectable

`ManifestSigningService` is currently a static class. It cannot be mocked in tests, cannot be
substituted with a different signing algorithm, and cannot receive injected dependencies in future.

**Changes:**
- Add `IManifestSigningService` interface in `src/MangaMesh.Shared/Services/`
- Convert `ManifestSigningService` to a non-static class implementing it (method signatures unchanged)
- Register: `services.AddSingleton<IManifestSigningService, ManifestSigningService>()`
- Update `ImportChapterService` to inject `IManifestSigningService` (removes the static call)
- `SerializeCanonical` can remain `internal static` as an implementation detail

**Files:**
- `src/MangaMesh.Shared/Services/ManifestSigningService.cs`
- `src/MangaMesh.Shared/Services/IManifestSigningService.cs` (new)

---

### Step 7 — SRP: Decompose `DhtNode`

`DhtNode` is 835 lines with at least 5 responsibilities. Extract three focused collaborators.
Each extraction is a separate commit so integration tests can be run between them.

#### 7a — Extract `IRoutingTable` / `KBucketRoutingTable`

```csharp
public interface IRoutingTable
{
    void AddOrUpdate(RoutingEntry entry);
    IReadOnlyList<RoutingEntry> FindClosest(byte[] targetId, int k = 20);
    IReadOnlyList<RoutingEntry> GetAll();
    int BucketCount { get; }
}

// Moves all KBucket and routing-table management out of DhtNode
public class KBucketRoutingTable : IRoutingTable { ... }
```

#### 7b — Extract `IBootstrapNodeProvider`

```csharp
public interface IBootstrapNodeProvider
{
    Task<IReadOnlyList<RoutingEntry>> GetBootstrapNodesAsync();
}

// Moves YAML-file reading from DhtNode.Start()
public class YamlBootstrapNodeProvider : IBootstrapNodeProvider
{
    public YamlBootstrapNodeProvider(IOptions<DhtOptions> options) { ... }
}

// Used in tests — no filesystem, no YAML
public class StaticBootstrapNodeProvider : IBootstrapNodeProvider
{
    public StaticBootstrapNodeProvider(IEnumerable<RoutingEntry> nodes) { ... }
}
```

#### 7c — Extract `IDhtRequestTracker`

```csharp
public interface IDhtRequestTracker
{
    void Register(Guid requestId, TaskCompletionSource<DhtMessage> tcs);
    bool TryComplete(Guid requestId, DhtMessage message);
    void RegisterContent(Guid requestId, TaskCompletionSource<ContentMessage> tcs);
    bool TryCompleteContent(Guid requestId, ContentMessage message);
}

// Wraps the two ConcurrentDictionary<Guid, TCS> fields currently embedded in DhtNode
public class DhtRequestTracker : IDhtRequestTracker { ... }
```

`DhtNode` after all three extractions:
- **Constructor**: `INodeIdentity, ITransport, IDhtStorage, IRoutingTable, IBootstrapNodeProvider, IDhtRequestTracker, IKeyPairService, IKeyStore, INodeAnnouncer, INodeConnectionInfoProvider, ILogger<DhtNode>`
- **Remaining responsibilities**: message signing/sending, iterative lookup orchestration, maintenance loop scheduling
- **Rough size**: ~300 lines (down from 835)

---

### Step 8 — DRY: Consolidate test helpers

Two test helpers are copy-pasted between `ContentProtocolTests.cs` and `DhtManifestTests.cs`:
- `InMemoryKeyStore : IKeyStore`
- `CreateNode(int port)` factory method

**Create** `tests/MangaMesh.Peer.Tests/Helpers/TestNodeFactory.cs`:
- Single canonical `InMemoryKeyStore`
- Single canonical `CreateNode(int port)` with optional handler registration overloads
- Both existing test classes reference the shared helpers

---

## Implementation Order & Risk

| Step | Principle | Scope | Risk | Payoff |
|---|---|---|---|---|
| 1 — `IOptions<T>` config | DIP | Peer.Core, APIs | Low | Foundation for all other changes |
| 2 — `ILogger<T>` | DRY | Peer.Core, Shared | Very Low | Observability, kills console noise |
| 3 — Split `ITrackerClient` | ISP | Peer.Core | Low | Smaller mocks, clearer dependencies |
| 4 — `ITrackerPublisher` | SRP + DRY | Peer.Core | Low | Reusable auth flow |
| 5 — `IChapterSourceReader` | OCP | Peer.Core | Medium | Extensible import pipeline |
| 6 — Injectable signing service | SRP + DIP | Shared | Low | Mockable in unit tests |
| 7 — Decompose `DhtNode` | SRP | Peer.Core | High | Biggest complexity reduction |
| 8 — Test helper consolidation | DRY | Tests | Very Low | Clean test infrastructure |

Steps 1–6 are independent of each other (after step 1 is complete) and can be done in any order.
Step 7 is the highest risk change — run `dotnet test` after each of the three sub-extractions (7a, 7b, 7c).

---

## New Files Summary

```
src/MangaMesh.Peer.Core/
  Configuration/
    BlobStoreOptions.cs               ← Step 1
    ManifestStoreOptions.cs           ← Step 1
    KeyStoreOptions.cs                ← Step 1
    DhtOptions.cs                     ← Step 1
  Chapters/
    IChapterSourceReader.cs           ← Step 5
    DirectorySourceReader.cs          ← Step 5
    ZipSourceReader.cs                ← Step 5
    IImageFormatProvider.cs           ← Step 5
    DefaultImageFormatProvider.cs     ← Step 5
  Node/
    IRoutingTable.cs                  ← Step 7a
    KBucketRoutingTable.cs            ← Step 7a
    IBootstrapNodeProvider.cs         ← Step 7b
    YamlBootstrapNodeProvider.cs      ← Step 7b
    StaticBootstrapNodeProvider.cs    ← Step 7b
    IDhtRequestTracker.cs             ← Step 7c
    DhtRequestTracker.cs              ← Step 7c
  Tracker/
    IPeerLocator.cs                   ← Step 3
    INodeAnnouncer.cs                 ← Step 3
    ISeriesRegistry.cs                ← Step 3
    IManifestAnnouncer.cs             ← Step 3
    ITrackerChallengeClient.cs        ← Step 3
    ITrackerPublisher.cs              ← Step 4
    TrackerPublisher.cs               ← Step 4

src/MangaMesh.Shared/Services/
    IManifestSigningService.cs        ← Step 6

tests/MangaMesh.Peer.Tests/Helpers/
    TestNodeFactory.cs                ← Step 8
```

**Modified files (no new files):**
- `BlobStore.cs`, `ManifestStore.cs`, `KeyStore.cs`, `StorageMonitorService.cs`, `ChunkIngester.cs` — Step 1
- `KeyPairService.cs`, `ImportChapterService.cs`, `PublicKeyRegistry.cs`, `MangaDexMetadataProvider.cs` — Step 2
- `ITrackerClient.cs`, `TrackerClient.cs` — Step 3
- `ImportChapterService.cs` — Steps 4, 5, 6
- `ManifestSigningService.cs` — Step 6
- `DhtNode.cs` — Step 7
- `ContentProtocolTests.cs`, `DhtManifestTests.cs` — Step 8

---

## Verification

After each step, run:

```bash
dotnet build src/
dotnet test tests/MangaMesh.Peer.Tests/
dotnet test tests/MangaMesh.IntegrationTests/
```

All 7 existing tests must remain green at every step. The integration tests exercise the full
DhtNode + content protocol stack and will catch regressions in Step 7.
