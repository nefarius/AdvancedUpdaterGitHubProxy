using FluentValidation.Results;

namespace AdvancedUpdaterGitHubProxy.Extensions;

public class ValidationResponse
{
    public string Property { get; init; } = default!;
    public string Message { get; init; } = default!;
}

public static class ValidationExtensions
{
    public static List<ValidationResponse> ToResponse(this IEnumerable<ValidationFailure> errors)
    {
        return errors.Select(error =>
            new ValidationResponse { Property = error.PropertyName, Message = error.ErrorMessage }).ToList();
    }
}