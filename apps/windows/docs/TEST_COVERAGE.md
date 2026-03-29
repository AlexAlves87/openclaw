# Test Coverage Summary

**571 tests total** (478 shared + 93 tray) — all passing ✅

| Metric | Value |
|--------|-------|
| Total Tests | 571 |
| Passing | 571 (100%) |
| Failing | 0 |
| Framework | xUnit 2.9.3 / .NET 10.0 |

## Test Projects

### OpenClaw.Shared.Tests — 478 tests

#### ModelsTests
- **AgentActivityTests** (~15) — glyph mapping for all ActivityKind values, display text formatting
- **ChannelHealthTests** (~25) — status display for ON/OFF/ERR/LINKED/READY states, case-insensitive matching
- **SessionInfoTests** (~25) — display text, main/sub session prefixes, ShortKey extraction, context summaries
- **SessionInfoContextSummaryTests** (~8) — token window formatting, millions/thousands display
- **SessionInfoRichDisplayTextTests** (~8) — rich display labels, display name fallback
- **SessionInfoAgeTextTests** (~6) — relative time formatting (minutes, hours ago)
- **GatewayUsageInfoTests** (~12) — token counts (999, 15.0K, 2.5M), cost display, empty state
- **GatewayNodeInfoTests** (~10) — display name, node info formatting

#### OpenClawGatewayClientTests (~50)
- Notification classification (health, urgent, calendar, build, email alerts)
- Tool-to-activity mapping (exec, read, write, edit, search, browser, message)
- Path shortening and label truncation
- `ResetUnsupportedMethodFlags` — clearing unsupported flag state

#### ExecApprovalPolicyTests (~20)
- Policy rule evaluation, persistence, pattern matching

#### CapabilityTests (~30)
- **SystemCapabilityTests** — system command handling
- **CanvasCapabilityTests** — canvas command handling
- **ScreenCapabilityTests** — screen command handling
- **CameraCapabilityTests** — camera command handling

#### NodeCapabilitiesTests (~15)
- Base class parsing, `ExecuteAsync` return values, payload handling

#### DeviceIdentityTests (~15)
- Payload format validation, pairing status events

#### NotificationCategorizerTests (~30)
- Keyword matching, channel-to-type mapping (health, calendar, stock, build, email, urgent)
- Priority rules, default categorization

#### GatewayUrlHelperTests (~25)
- URL normalization (http→ws, https→wss)
- Embedded credential stripping
- Port preservation, path handling

#### SystemRunTests (~20)
- Command execution, timeout handling, environment variables

#### ShellQuotingTests (~20)
- Shell metachar detection (`&`, `|`, `;`, `$`, `` ` ``, `*`, `?`, `<`, `>`, etc.)
- Quoting for safe shell invocation

#### WindowsNodeClientTests (~10)
- URL handling, endpoint construction

#### NodeInvokeResponseTests (~5)
- Default values, property setting

#### ReadmeValidationTests (~5)
- Documentation sync checks

---

### OpenClaw.Tray.Tests — 93 tests

#### MenuDisplayHelperTests (~40)
- `GetStatusIcon` — emoji mapping for Connected/Disconnected/Connecting/Error states
- `GetChannelStatusIcon` — status icons for running/idle/pending/error/disconnected + case-insensitive variants
- `GetNextToggleValue` — ON↔OFF toggling, case handling
- Unknown/empty status fallback

#### MenuPositionerTests (~15)
- Screen edge clamping (top-left, bottom-right)
- Taskbar-at-right scenario
- Menu positioning relative to cursor

#### SettingsRoundTripTests (~15)
- Serialization/deserialization round trips
- Default values on missing keys
- Backward compatibility with older settings formats

#### DeepLinkParserTests (~23)
- `ParseDeepLink` — protocol validation, null/empty handling, subpath parsing, trailing slash stripping
- Query parameter extraction (`GetQueryParam`)
- URL-encoded message handling
- Multiple query parameters, missing keys

---

## Running Tests

```bash
# All tests
dotnet test

# Single project
dotnet test tests/OpenClaw.Shared.Tests
dotnet test tests/OpenClaw.Tray.Tests

# Specific test class
dotnet test --filter "FullyQualifiedName~MenuDisplayHelperTests"

# Verbose output
dotnet test --logger "console;verbosity=detailed"
```

## Not Covered (Requires Integration Tests)

- WebSocket connection/reconnection flow
- Real gateway message parsing
- Concurrent event handling
- File I/O and thread synchronization

---

**Last Updated**: 2026-03-18
**Framework**: xUnit 2.9.3 / .NET 10.0
**Status**: ✅ 571 tests passing
