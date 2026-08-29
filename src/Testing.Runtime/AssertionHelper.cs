using System.Text.Json;

namespace Testing.Runtime;

public static class AssertionHelper
{
    public static void AssertValue<T>(
        string expectedJson,
        T actual,
        string methodName,
        string filePath)
    {
        T expected;
        try
        {
            expected = JsonSerializer.Deserialize<T>(expectedJson)!;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Method '{methodName}' failed to deserialize the expected value for dataset '{filePath}': {ex.Message}");
        }

        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"Method '{methodName}' returned an unexpected value for dataset '{filePath}'. Expected: {expected}, Actual: {actual}.");
        }
    }

    public static void AssertThrows(
        string expectedExceptionType,
        Action action,
        string methodName,
        string filePath)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            if (ex.GetType().FullName == expectedExceptionType)
                return;

            throw new InvalidOperationException(
                $"Method '{methodName}' threw an unexpected exception type for dataset '{filePath}'. Expected: {expectedExceptionType}, Actual: {ex.GetType().FullName}.");
        }

        throw new InvalidOperationException(
            $"Method '{methodName}' was expected to throw '{expectedExceptionType}' for dataset '{filePath}', but no exception was thrown.");
    }

    public static T Invoke<T>(
        Func<T> invocation,
        string methodName,
        string filePath)
    {
        try
        {
            return invocation();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Method '{methodName}' threw an unexpected exception for dataset '{filePath}': {ex.GetType().FullName}: {ex.Message}");
        }
    }
}
