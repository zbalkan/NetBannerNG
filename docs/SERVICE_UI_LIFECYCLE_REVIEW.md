# Service/UI Lifecycle Review

## Scope reviewed
- `src/NetBannerNG.Service/*`
- `src/NetBannerNG.Common/Extensions/ProcessStartInfoExtensions.cs`
- `src/NetBannerNG/Services/AppLifecycleService.cs`
- `src/NetBannerNG/NamedPipeClient.cs`

## Conclusion
The current design generally matches the expected architecture:
- service watchdog in Session 0,
- UI process in user session,
- service-owned lifecycle/restart logic.

## Key observations
1. **Service launches UI in active user session in non-interactive mode** via `RunAsActiveUser()` and `CreateProcessAsUser`, which is correct for avoiding elevated UI context.
2. **Watchdog restarts UI** when no child process is detected (`ServiceHost.MonitorChildProcess`).
3. **UI shuts down when service/pipe disconnects**, allowing service to re-provision the UI.

## Gaps / risks
1. **Session targeting can still be challenging in RDP-heavy environments.**
   Service launch/reconciliation remains tied to active-session discovery and may need additional policy for multi-session precedence.
2. **Observability and least-privilege hardening should be continuously validated.**
   Keep lifecycle and authorization logging/tests aligned with implementation as the service evolves.

## Recommendation
- Keep the production pattern (service-as-watchdog + user-session UI) as-is.
- Tighten pipe ACL to least privilege (e.g., active session user SID + SYSTEM + Administrators as needed).
- Re-enable `Process.Start(psi)` in interactive/debug mode for testability parity.


## RDP / logon / logoff behavior details
- **RDP logon (new remote session):** current implementation resolves the launch token via `WTSGetActiveConsoleSessionId()` (physical console session). If no one is on the physical console, `GetActiveSessionId()` can throw (`0`/`0xFFFFFFFF`) and child launch fails until a console session is active.
- **RDP reconnect to existing session:** if the UI process in that same user session is still running, watchdog sees it and does not relaunch. If it is not running, restart behavior still depends on finding an "active console" token, so reconnect-only scenarios can still fail to relaunch.
- **User logoff:** WPF app handles session ending and shuts down gracefully; the service remains running, keeps watchdog loop alive, and retries relaunch every ~5 seconds when no child process exists.
- **No interactive user logged in:** service keeps retrying child start; expected `RunAsActiveUser()` failures are logged and retried by watchdog throttle.

### Practical implication
The watchdog lifecycle is present, but session targeting is **console-centric** rather than **active interactive session-centric**. For RDP-first environments, use `WTSEnumerateSessions` + active session selection (or trigger launch from service session-change events) instead of only `WTSGetActiveConsoleSessionId()`.

## Prioritized fix roadmap (by impact)

### P0 — Correct session targeting for UI launch (highest impact)
**Problem:** UI launch token is derived from `WTSGetActiveConsoleSessionId()`, which can miss RDP-active users and fail in remote-first environments.

**Fix plan:**
1. Replace console-only selection with active interactive session discovery (`WTSEnumerateSessions` + choose `WTSActive` in preferred order).
2. Add fallback logic when multiple active sessions exist (configurable policy: latest active session, specific user SID, or all active sessions with single-instance gate per session).
3. Wire service `OnSessionChange` handling (logon/logoff/connect/disconnect) to trigger immediate reconcile instead of waiting only on polling.

**Success criteria:**
- RDP logon starts UI in that user’s session without requiring a physical console session.
- Logoff/reconnect transitions preserve exactly one UI instance per active session policy.

### P1 — Harden IPC trust boundary (high impact)
**Problem:** Pipe ACL currently grants `AuthenticatedUserSid` read/write.

**Fix plan:**
1. Restrict server pipe ACL to least privilege: `SYSTEM`, `Administrators` (as required), and the target interactive user/session SID.
2. Add runtime validation of connecting client identity/session before accepting commands.
3. Keep checksum validation, but treat ACL + identity checks as primary authorization controls.

**Success criteria:**
- Non-target local users cannot connect/send to service pipe.
- Authorized UI client can still connect across restart scenarios.

### P2 — Improve lifecycle determinism and observability (medium impact)
**Problem:** Watchdog works, but behavior during transitions can be opaque.

**Fix plan:**
1. Introduce explicit state machine for service reconciliation (`NoUser`, `SessionDetected`, `Launching`, `Running`, `Backoff`).
2. Emit structured event logs for each transition and reason codes for launch failures.
3. Add exponential backoff ceiling/jitter for repeated launch failures (instead of fixed 5s only).

**Success criteria:**
- Operators can diagnose "why UI not launched" from event logs alone.
- Restart storms are reduced under persistent failure conditions.

### P3 — Restore interactive/debug parity (medium-low impact)
**Problem:** Interactive branch has `Process.Start(psi)` commented out, limiting realistic local validation.

**Fix plan:**
1. Re-enable child start in interactive mode behind `--debug` guard.
2. Add explicit warning banner in debug mode that behavior is non-production.
3. Add smoke test checklist for local lifecycle checks.

**Success criteria:**
- Developers can validate watchdog + relaunch path locally without service install.

### P4 — Add automated coverage for session and security scenarios (supporting impact)
**Fix plan:**
1. Unit test session-selection policy logic (single active, multiple active, none active).
2. Integration tests for pipe security policy creation and identity acceptance rules.
3. Regression tests for logout/disconnect and reconnect reconciliation behavior (where test harness allows).

**Success criteria:**
- Core lifecycle/security decisions are regression-protected in CI.


## Clarification: one pipe per session?
No — **as currently implemented, there is a single machine-local named pipe endpoint** (`netbannerng-pipe`) hosted by one service process, using `SingleConnectionPipeServer`.

Implications:
- It is **not** currently modeled as "one pipe instance per Windows session."
- With `SingleConnectionPipeServer`, only one client connection is supported at a time.
- Multi-session behavior (for example, concurrent RDP users) requires explicit orchestration changes (for example, per-session client policy and/or multiplexed server strategy).


## ACL model update: separate pipes per session is preferred
You are right: for strong least-privilege ACLs, a **per-session pipe model** is the cleanest approach.

### Recommended topology
- Keep one privileged service orchestrator, but create a dedicated pipe endpoint per target session, e.g. `netbannerng-pipe-s<SessionId>`.
- Set ACL on each pipe instance to only:
  - `SYSTEM`
  - (optional) `Administrators` for supportability
  - the specific logged-on user SID for that session
- Avoid broad `Authenticated Users` grants for session-scoped control channels.

### Why this helps
- Enforces authorization at OS ACL boundary before message handling.
- Prevents cross-session client attachment by unrelated users.
- Simplifies identity checks because pipe name and ACL both encode intended session scope.

### Implementation roadmap delta
- **P0 extension:** session manager creates/tears down per-session pipe servers on logon/logoff/session-disconnect events.
- **P1 extension:** replace single `SingleConnectionPipeServer<PipeMessage>` with per-session server map keyed by SessionId.
- **P1 extension:** bind each launched UI instance to its own session pipe name via startup args/environment.
