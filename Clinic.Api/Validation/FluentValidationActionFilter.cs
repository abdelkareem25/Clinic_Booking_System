using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Clinic.Api.Validation
{
    /// <summary>
    /// Runs the registered FluentValidation validators against every action argument.
    ///
    /// The validators existed but had never executed: FluentValidation was referenced and five
    /// validator classes were written, but nothing registered them and no auto-validation was wired
    /// up. Every rule in them - "End time must be after start time", "Name cannot exceed 100
    /// characters", "DoctorId must be greater than 0" - was dead code.
    ///
    /// Failures are merged into ModelState rather than short-circuiting with a response of their
    /// own. That is what <see cref="Order"/> is for: running before [ApiController]'s
    /// ModelStateInvalidFilter means DataAnnotation failures and FluentValidation failures arrive in
    /// the client's hands as ONE ValidationProblemDetails, in the shape TODO #12 established, with
    /// no second error contract to maintain.
    /// </summary>
    public sealed class FluentValidationActionFilter : IAsyncActionFilter
    {
        /// <summary>
        /// ModelStateInvalidFilter sits at -2000 and short-circuits as soon as ModelState is
        /// invalid. Anything lower runs first; anything higher would never run at all whenever a
        /// DataAnnotation had already failed.
        /// </summary>
        public const int FilterOrder = -2100;

        private readonly IServiceProvider _services;

        public FluentValidationActionFilter(IServiceProvider services)
        {
            _services = services;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            foreach (var (parameterName, argument) in context.ActionArguments)
            {
                if (argument is null) continue;

                // Resolved by the argument's runtime type, so a validator is picked up simply by
                // existing - no registry to keep in step with the DTOs.
                var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());

                if (_services.GetService(validatorType) is not IValidator validator) continue;

                var validationContext = new ValidationContext<object>(argument);
                var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

                foreach (var failure in result.Errors)
                {
                    // PropertyName is empty for rules declared against the model as a whole; key
                    // those under the parameter so the client still gets somewhere to attach them.
                    var key = string.IsNullOrEmpty(failure.PropertyName) ? parameterName : failure.PropertyName;

                    context.ModelState.AddModelError(key, failure.ErrorMessage);
                }
            }

            await next();
        }
    }
}
