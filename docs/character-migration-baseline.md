# Recorded character reference changes

## 2026-11-01: Standard Person shin equipment mounts

The requested standard Person equipment support adds `left-shin-art` and
`right-shin-art` sockets to Person and its inherited builds. Each mount sits at its
foot/ankle control, points local +Y toward its knee, and has a drawing-layer offset
of -0.01. It uses the same coordinate convention as the existing foot artwork.

The migration reference update adds these two socket frames to the 40 existing
Person-family samples (80 added entries). Existing controls, socket frames,
silhouettes, animation samples, collision results and combat records stay unchanged.
The reference test's splice mode adds only the newly reported socket entries.
Equipment tests separately check both mounts across clips and facings, mirroring,
inventory swaps, replacement visibility and restoration after unequip.
