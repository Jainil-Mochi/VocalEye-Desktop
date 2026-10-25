# ADR-0002: Backend connection model

- Status: Proposed
- Decision: The desktop client communicates with the existing backend over HTTP and does not access backend databases directly.
- Rationale: Keeps backend ownership and persistence behind the existing API boundary.
- Consequences: API routes, authentication, process lifecycle, and streaming contracts must be confirmed before implementing integration.
