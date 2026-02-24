## **1.0 **
// Responsibilities:
// - Synchronizes voxel modifications
// - Efficiently batches terrain edits
// - Uses buffered RPCs for late joiners
```

---

### Ownership Model: The "Isolation Ownership" Pattern
```
SCENARIO: Australian, American (Host), British players

┌────────────────────────────────────────────────────────────────────┐
│                        GAME WORLD                                  │
│                                                                    │
│    Australian                         American + British │
│    (Isolated)                              (Grouped)              │
│                                                                    │
│  ┌─────────┐                           ┌─────────┐ ┌─────────┐   │
│  │ Chunk A │                           │ Chunk X │ │ Chunk Y │   │
│  │Owner: AU│ ◄── LAG-FREE              │Owner:SRV│ │Owner:SRV│   │
│  │         │     EDITING               │         │ │         │   │
│  └─────────┘                           └─────────┘ └─────────┘   │
│                                             ▲           ▲         │
│                                             │           │         │
│                                        All edits go through      │
│                                        server (latency)          │
└────────────────────────────────────────────────────────────────────┘

OWNERSHIP RULES:
1. If ONLY ONE player is observing a chunk → That player OWNS it
2. If MULTIPLE players are observing → SERVER owns it
3. Ownership transitions are server-authoritative

## **2.1 - ChunkObserverTracker (Server-side)**
```
- Tracks which players are observing which chunks
- Uses spatial proximity checks
- Triggers ownership evaluation on observer changes
```

## **2.2 - Ownership Handoff Protocol**
```
CLIENT GETS ISOLATED:
1. Server detects single observer
2. Server calls TargetRPC to new owner: "PrepareForOwnership"
3. Client confirms readiness
4. Server transfers ownership via GiveOwnership()
5. New owner can now edit lag-free

CLIENT LOSES ISOLATION:
1. Server detects multiple observers
2. Server calls ObserversRPC: "PrepareForServerOwnership"
3. Current owner flushes pending edits to server
4. Server takes ownership back
5. All edits now go through server
```