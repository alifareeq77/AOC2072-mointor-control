using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[assembly: AssemblyTitle("Monitor Control")]
[assembly: AssemblyDescription("Brightness, contrast and input control for external monitors over DDC/CI")]
[assembly: AssemblyProduct("Monitor Control")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]
[assembly: ComVisible(false)]

// Opts into .NET 4.8 runtime behavior (per-monitor DPI scaling among others);
// csc doesn't add this attribute on its own.
[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
