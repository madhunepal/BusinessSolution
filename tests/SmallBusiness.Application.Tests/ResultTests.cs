using SmallBusiness.Application.Common;

namespace SmallBusiness.Application.Tests;

public class ResultTests
{
    [Fact]
    public void Result_Success_IsSucceeded()
    {
        var result = Result.Success();

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Result_Failure_HasErrors()
    {
        var result = Result.Failure("Error 1", "Error 2");

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Errors.Length);
        Assert.Equal("Error 1", result.Errors[0]);
    }

    [Fact]
    public void ResultT_Success_HasValue()
    {
        var result = Result.Success(42);

        Assert.True(result.Succeeded);
        Assert.Equal(42, result.Value);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ResultT_Failure_HasNoValue()
    {
        var result = Result.Failure<int>("Not found");

        Assert.False(result.Succeeded);
        Assert.Equal(default, result.Value);
        Assert.Single(result.Errors);
    }
}
