using FluentValidation;
using HMS.Modules.Matching.Application.Requests;

namespace HMS.Modules.Matching.Application.Validators
{
    public class SelectedRequestValidator : AbstractValidator<AcceptSelectedRequest>
    {
        public SelectedRequestValidator()
        {
            RuleFor(x => x.ShipmentIds).NotNull().NotEmpty();
        }
    }

    public class RejectSelectedValidator : AbstractValidator<RejectSelectedRequest>
    {
        public RejectSelectedValidator()
        {
            RuleFor(x => x.ShipmentIds).NotNull().NotEmpty();
        }
    }
}
