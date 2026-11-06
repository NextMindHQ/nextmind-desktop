using NextMind.Desktop.Core;

namespace NextMind.Desktop.App;

public static class V3TestSession
{
    public const string RuntimeMarker = "NEXTMIND-DESKTOP-V3";
    public const string OpenControlCenterVariable = "NEXTMIND_V3_TEST_OPEN_CONTROL_CENTER";
    public const string IsolatedInstanceVariable = "NEXTMIND_V3_TEST_INSTANCE";
    public const string IsolatedMutexName = @"Local\NextMind.Desktop.SingleInstance.v1.V3Test";

    public static bool ShouldOpenControlCenter(string? configDirectoryOverride, string? openControlCenter)
        => !string.IsNullOrWhiteSpace(configDirectoryOverride)
            && string.Equals(openControlCenter, "1", StringComparison.Ordinal);

    public static bool ShouldOpenControlCenterFromEnvironment()
        => ShouldOpenControlCenter(
            Environment.GetEnvironmentVariable(AppPaths.ConfigDirectoryOverrideVariable),
            Environment.GetEnvironmentVariable(OpenControlCenterVariable));

    public static string? SelectMutexName(string? configDirectoryOverride, string? isolatedInstance)
        => !string.IsNullOrWhiteSpace(configDirectoryOverride)
            && string.Equals(isolatedInstance, "1", StringComparison.Ordinal)
                ? IsolatedMutexName
                : null;

    public static string? SelectMutexNameFromEnvironment()
        => SelectMutexName(
            Environment.GetEnvironmentVariable(AppPaths.ConfigDirectoryOverrideVariable),
            Environment.GetEnvironmentVariable(IsolatedInstanceVariable));

    public static string SelectIsolationSuffix(string? configDirectoryOverride, string? isolatedInstance)
    {
        if (string.IsNullOrWhiteSpace(configDirectoryOverride))
        {
            return string.Empty;
        }

        return string.Equals(isolatedInstance, "1", StringComparison.Ordinal)
            ? ".V3Test"
            : ".dev";
    }

    public static string SelectIsolationSuffixFromEnvironment()
        => SelectIsolationSuffix(
            Environment.GetEnvironmentVariable(AppPaths.ConfigDirectoryOverrideVariable),
            Environment.GetEnvironmentVariable(IsolatedInstanceVariable));
}
