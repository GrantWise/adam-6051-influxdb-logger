// Industrial.Adam.Logger.Tests - Simple Test to Verify Compilation
// Basic test to ensure the test infrastructure is working

using FluentAssertions;
using Xunit;

namespace Industrial.Adam.Logger.Tests;

/// <summary>
/// Simple test class to verify test infrastructure compilation
/// </summary>
public class SimpleTest
{
    [Fact]
    public void SimpleAssertion_ShouldWork()
    {
        // Arrange
        const int expected = 42;

        // Act
        const int actual = 42;

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void StringAssertion_ShouldWork()
    {
        // Arrange
        const string expected = "test";

        // Act
        const string actual = "test";

        // Assert
        actual.Should().Be(expected);
    }
}