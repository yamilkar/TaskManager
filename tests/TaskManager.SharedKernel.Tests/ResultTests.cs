namespace TaskManager.SharedKernel.Tests;

public class ResultTests
{
    [Fact]
    public void Success_result_exposes_value()
    {
        Result<int> r = 5;
        r.IsSuccess.ShouldBeTrue();
        r.Value.ShouldBe(5);
    }

    [Fact]
    public void Failure_result_exposes_error_and_throws_on_value()
    {
        Result<int> r = Error.Conflict("x.conflict", "msg");
        r.IsFailure.ShouldBeTrue();
        r.Error!.Type.ShouldBe(ErrorType.Conflict);
        Should.Throw<InvalidOperationException>(() => _ = r.Value);
    }

    [Fact]
    public void Validation_error_carries_field_errors()
    {
        var fields = new Dictionary<string, string[]> { ["title"] = ["Title is required."] };
        var error = Error.Validation("task.invalid", "Invalid", fields);
        error.Type.ShouldBe(ErrorType.Validation);
        error.FieldErrors!["title"].ShouldBe(new[] { "Title is required." });
    }

    [Fact]
    public void Non_generic_results_report_state()
    {
        Result.Success().IsSuccess.ShouldBeTrue();
        var failed = Result.Failure(Error.NotFound("a.nf", "m"));
        failed.IsFailure.ShouldBeTrue();
        failed.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public void Unauthorized_factory_sets_type()
        => Error.Unauthorized("u", "m").Type.ShouldBe(ErrorType.Unauthorized);

    [Fact]
    public void FixedClock_returns_given_time()
    {
        var now = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        IClock clock = new TaskManager.Testing.FixedClock(now);
        clock.UtcNow.ShouldBe(now);
    }

    [Fact]
    public void SystemClock_returns_utc_now()
        => new SystemClock().UtcNow.Offset.ShouldBe(TimeSpan.Zero);
}
