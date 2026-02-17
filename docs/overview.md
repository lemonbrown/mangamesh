# MangaMesh Overview

## Purpose

MangaMesh is a decentralized, peer-to-peer network for distributing and discovering manga. It allows peers to store, share, and retrieve manga chapters without depending on a single central server for content delivery. A lightweight central tracker coordinates peer discovery and manifest announcements, but content itself flows directly between peers.

The primary motivations are:
- **Resilience**: content survives as long as at least one peer hosts it
- **Authenticity**: every chapter is cryptographically signed, so readers can verify who produced it and that it hasn't been tampered with
- **Openness**: any node can join the network, import chapters, and serve content to others

---

## Goals

| Goal | Description |
|---|---|
| Decentralized content delivery | Chapters are stored and served by peers, not a central CDN |
| Cryptographic integrity | Every chapter manifest is signed with Ed25519; the network rejects forged content |
| Efficient discovery | A DHT (Distributed Hash Table) routes requests to peers that hold a given chapter |
| Flexible node types | Peers, gateways, and bootstrap nodes each play a distinct role |
| Low coordination overhead | A "smart sync" ping avoids redundant announcements to the tracker |
| Human-readable access | A public gateway and reader website let non-technical users browse and read manga |

---

## Node Types

```
┌──────────────────────────────────────────────────────────────┐
│                        Public Internet                        │
│                                                              │
│   ┌──────────┐     ┌──────────────┐     ┌────────────────┐  │
│   │  Reader  │────▶│   Gateway    │────▶│   DHT Network  │  │
│   │ (Komorii)│     │  (read-only) │     │  (peers/nodes) │  │
│   └──────────┘     └──────────────┘     └────────────────┘  │
│                           │                      │           │
│                    ┌──────▼──────┐        ┌──────▼──────┐   │
│                    │  Index API  │        │  Bootstrap  │   │
│                    │  (tracker)  │        │    Node     │   │
│                    └─────────────┘        └─────────────┘   │
└──────────────────────────────────────────────────────────────┘
```

| Node | Role |
|---|---|
| **Peer** | Stores and serves chapter content; imports new chapters from local files |
| **Bootstrap Node** | Static, well-known DHT entry point; helps new peers join the network |
| **Gateway** | Public-facing read-only API; forwards discovery requests into the DHT and caches popular content |
| **Index / Tracker** | Central registry of peers and which manifests they host; does not store content |
| **Admin API** | Back-office management over the tracker database |

---

## Key Concepts

### Manifest

A **manifest** is an immutable, signed JSON document that describes a piece of content. There are two levels:

- **PageManifest** — describes a single image file: its MIME type, total size, and an ordered list of SHA-256 chunk hashes
- **ChapterManifest** — describes a full chapter: series ID, chapter number, volume, language, scan group, a list of `ChapterFileEntry` references (each pointing to a page manifest by hash), the publisher's public key, and an Ed25519 signature

The **ManifestHash** is a deterministic SHA-256 computed from the chapter's identifying fields (series, scan group, language, chapter number, file paths). It acts as the canonical key for a chapter across the entire network.

### Blob Store

Binary content (image chunks) is stored as **blobs** keyed by SHA-256 hash. Images are split into 256 KB chunks before storage. Retrieving a page means fetching the PageManifest to get the ordered chunk list, then fetching each blob.

### Identity

Every peer generates an **Ed25519 key pair** on first start. The public key is hashed to derive the peer's `NodeId`. The private key signs DHT messages and chapter manifests, allowing any recipient to verify authenticity.

---

## High-Level Flows

### 1. Importing a Chapter

A peer operator drops image files into a directory and triggers an import via the Peer API or CLI.

```
Source files (directory / archive)
  │
  ▼
ImportChapterService
  │  splits each image into 256 KB chunks
  │  stores chunks in BlobStore (keyed by SHA-256)
  │  builds PageManifest per image
  │  builds ChapterManifest (lists all pages)
  │  signs ChapterManifest with peer's private key
  ▼
Local storage (SQLite + blob files)
  │
  ▼
Announce to Index Tracker
  │  POST /announce with all manifest hashes
  │  Index records which peer hosts which manifests
  ▼
DHT Store
  │  peer advertises manifest in the DHT
  ▼
Chapter is now discoverable and retrievable
```

### 2. Discovering a Chapter

A reader or gateway wants to find who hosts a specific chapter.

```
Client knows ManifestHash (from Index search or direct link)
  │
  ▼
Query Index API: GET /manifest/{hash}/peers
  │  returns list of peers currently hosting this manifest
  │
  ▼  (alternatively, go directly through DHT)
DHT Lookup
  │  find k-closest nodes to the ManifestHash
  │  those nodes return the peers that stored the manifest
  ▼
Client has a list of peer addresses
```

### 3. Retrieving Content

Once a client knows a peer address and a ManifestHash, it fetches content over a direct TCP content protocol.

```
Client connects to Peer (TCP)
  │
  ├─▶ GetManifest(ManifestHash)
  │       ◀─ ManifestData (ChapterManifest JSON)
  │
  └─▶ for each page in ChapterManifest:
        GetManifest(pageHash)
            ◀─ ManifestData (PageManifest JSON)
        for each chunk hash in PageManifest:
          GetBlob(blobHash)
              ◀─ BlobData (raw bytes)
  │
  ▼
Client reassembles image files from ordered chunks
```

### 4. Tracker Sync (Smart Sync)

Peers periodically check whether the tracker's view of their manifest set is up to date, without sending redundant data.

```
Peer computes ManifestSetHash = SHA-256( sorted list of all its manifest hashes )
  │
  ▼
POST /ping  { nodeId, manifestSetHash }
  │
  ├─ 200 OK  → tracker is up to date, nothing to do
  │
  └─ 409 Conflict → hashes differ
       │
       ▼
       POST /announce  { nodeId, all manifest hashes + metadata }
         tracker updates its records
```

### 5. Authorization for Manifest Announcements

Before a peer can announce a specific chapter to the tracker, it must prove ownership of the signing key.

```
Peer  →  POST /api/keys/{publicKey}/challenges
              ◀─ { challengeId, randomBytes }

Peer signs randomBytes with private key

Peer  →  POST /api/keys/{publicKey}/challenges/{id}/verify  { signature }
              ◀─ authorization token

Peer  →  POST /api/announce/manifest  { manifest, token }
              tracker verifies signature, records manifest
```

---

## Protocol Summary

| Layer | Transport | Format | Purpose |
|---|---|---|---|
| DHT | TCP (length-prefixed) | JSON | Node routing, peer discovery, manifest advertisement |
| Content | TCP (length-prefixed) | JSON + raw bytes | Manifest and blob retrieval between peers |
| Tracker | HTTP/REST | JSON | Peer registration, manifest announcement, peer lookup |

All DHT messages include the sender's `NodeId`, a UTC timestamp, a `RequestId`, and an Ed25519 signature. The 10 MB per-message size limit is enforced at the transport layer.

---

## Technology Stack

| Concern | Choice |
|---|---|
| Language / Runtime | C# 12, .NET 10 |
| Web APIs | ASP.NET Core |
| Local storage | SQLite via Entity Framework Core |
| Cryptography | NSec.Cryptography (Ed25519) |
| Serialization | System.Text.Json |
| Frontend | React / TypeScript / Vite |
| Containerization | Docker / docker-compose |

---

## Repository Structure

```
mangamesh/
├── src/
│   ├── MangaMesh.Shared/              # Shared models, DB contexts, crypto helpers
│   ├── MangaMesh.Peer/
│   │   ├── MangaMesh.Peer.Core/       # DHT node, transport, import, blob store
│   │   ├── MangaMesh.Peer.ClientApi/  # REST API for local peer management
│   │   └── MangaMesh.Peer.GatewayApi/ # Public gateway (read-only)
│   ├── MangaMesh.Index.Api/           # Tracker / central index
│   ├── MangaMesh.Index.AdminApi/      # Admin dashboard API
│   └── MangaMesh.SeriesScraper.Cli/   # CLI to scrape chapters from external sources
├── frontend/
│   ├── mangamesh-peer-ui/             # Peer management UI
│   ├── komorii/                       # Public reader website
│   └── index-admin-ui/               # Admin dashboard UI
├── tests/                             # Integration and unit tests
└── docs/                              # Documentation (this file)
```
