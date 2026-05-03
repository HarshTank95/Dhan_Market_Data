using System.Runtime.Versioning;

// Single-user local app — Windows-only by design (token at-rest encryption uses
// Windows DPAPI). Tagging the assembly avoids spurious CA1416 warnings at the
// DI call sites where we register DpapiTokenProtector.
[assembly: SupportedOSPlatform("windows")]
