# Background processing plan

The application contracts reserve an in-memory queue for resume processing. A worker can be added later and registered only after retry, cancellation, and restart recovery behavior is defined. The v1.2 entity design does not use a persistent queue table; on startup, pending resume records should be scanned and re-enqueued.
