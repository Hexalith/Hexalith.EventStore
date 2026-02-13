namespace Hexalith.EventStore.Server.Tests.Pipeline;

using FluentValidation;
using FluentValidation.Results;

using Hexalith.EventStore.CommandApi.Pipeline;

using MediatR;

using Shouldly;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        // Arrange
        var validators = Enumerable.Empty<IValidator<TestCommand>>();
        var behavior = new ValidationBehavior<TestCommand, TestResult>(validators);
        var request = new TestCommand("test");
        var expected = new TestResult("ok");
        RequestHandlerDelegate<TestResult> next = (_) => Task.FromResult(expected);

        // Act
        TestResult result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsNext()
    {
        // Arrange
        var validator = new AlwaysValidValidator();
        var behavior = new ValidationBehavior<TestCommand, TestResult>([validator]);
        var request = new TestCommand("test");
        var expected = new TestResult("ok");
        RequestHandlerDelegate<TestResult> next = (_) => Task.FromResult(expected);

        // Act
        TestResult result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationException()
    {
        // Arrange
        var validator = new FailingValidator("Value", "Value is required");
        var behavior = new ValidationBehavior<TestCommand, TestResult>([validator]);
        var request = new TestCommand("");
        RequestHandlerDelegate<TestResult> next = (_) => Task.FromResult(new TestResult("should not reach"));

        // Act & Assert
        ValidationException ex = await Should.ThrowAsync<ValidationException>(
            behavior.Handle(request, next, CancellationToken.None));
        ex.Errors.Count().ShouldBe(1);
        ex.Errors.First().PropertyName.ShouldBe("Value");
    }

    [Fact]
    public async Task Handle_MultipleValidators_AggregatesFailures()
    {
        // Arrange
        var validator1 = new FailingValidator("Field1", "Error 1");
        var validator2 = new FailingValidator("Field2", "Error 2");
        var behavior = new ValidationBehavior<TestCommand, TestResult>([validator1, validator2]);
        var request = new TestCommand("");
        RequestHandlerDelegate<TestResult> next = (_) => Task.FromResult(new TestResult("should not reach"));

        // Act & Assert
        ValidationException ex = await Should.ThrowAsync<ValidationException>(
            behavior.Handle(request, next, CancellationToken.None));
        ex.Errors.Count().ShouldBe(2);
        ex.Errors.ShouldContain(e => e.PropertyName == "Field1");
        ex.Errors.ShouldContain(e => e.PropertyName == "Field2");
    }

    [Fact]
    public async Task Handle_InvalidRequest_DoesNotCallNext()
    {
        // Arrange
        var validator = new FailingValidator("Value", "Required");
        var behavior = new ValidationBehavior<TestCommand, TestResult>([validator]);
        var request = new TestCommand("");
        bool nextCalled = false;
        RequestHandlerDelegate<TestResult> next = (_) =>
        {
            nextCalled = true;
            return Task.FromResult(new TestResult("should not reach"));
        };

        // Act
        await Should.ThrowAsync<ValidationException>(
            behavior.Handle(request, next, CancellationToken.None));

        // Assert
        nextCalled.ShouldBeFalse();
    }

    // Public types required for FluentValidation (Castle.DynamicProxy compatibility)
    public record TestCommand(string Value) : IRequest<TestResult>;

    public record TestResult(string Result);

    private sealed class AlwaysValidValidator : AbstractValidator<TestCommand>
    {
        // No rules = always valid
    }

    private sealed class FailingValidator : IValidator<TestCommand>
    {
        private readonly ValidationFailure _failure;

        public FailingValidator(string propertyName, string errorMessage) =>
            _failure = new ValidationFailure(propertyName, errorMessage);

        public ValidationResult Validate(TestCommand instance) =>
            new([_failure]);

        public ValidationResult Validate(IValidationContext context) =>
            new([_failure]);

        public Task<ValidationResult> ValidateAsync(TestCommand instance, CancellationToken cancellation = default) =>
            Task.FromResult(new ValidationResult([_failure]));

        public Task<ValidationResult> ValidateAsync(IValidationContext context, CancellationToken cancellation = default) =>
            Task.FromResult(new ValidationResult([_failure]));

        public IValidatorDescriptor CreateDescriptor() =>
            throw new NotSupportedException();

        public bool CanValidateInstancesOfType(Type type) =>
            type == typeof(TestCommand);
    }
}
