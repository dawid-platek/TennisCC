using System.ComponentModel.DataAnnotations;

namespace TenisProgress.Server.Endpoints;

/// <summary>Runs the DataAnnotations rules shared with the Blazor client on the request body.</summary>
public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var model = ctx.Arguments.OfType<T>().FirstOrDefault();
        if (model is null) return Results.BadRequest();

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true))
        {
            var errors = results
                .GroupBy(r => r.MemberNames.FirstOrDefault() ?? "")
                .ToDictionary(g => g.Key, g => g.Select(r => r.ErrorMessage ?? "Błąd").ToArray());
            return Results.ValidationProblem(errors);
        }
        return await next(ctx);
    }
}

public static class ValidationExtensions
{
    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder) where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>();

    public static IResult Problem(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [""] = [message] });

    public static string? Clean(this string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
