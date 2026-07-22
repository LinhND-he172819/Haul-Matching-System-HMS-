using FluentValidation;
using HMS.Modules.Matching.Application.DTOs;
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

    public class CreateProposalValidator : AbstractValidator<CreateProposalRequest>
    {
        public CreateProposalValidator()
        {
            RuleFor(x => x.ShipmentId).NotEmpty();
            RuleFor(x => x.SenderName).NotEmpty().WithMessage("Sender Name không được để trống.");
            RuleFor(x => x.SenderPhone).NotEmpty().WithMessage("Sender Phone không được để trống.");
            RuleFor(x => x.PickupAddress).NotEmpty().WithMessage("Pickup Address không được để trống.");
        }
    }

    public class RejectProposalValidator : AbstractValidator<RejectProposalRequest>
    {
        public RejectProposalValidator()
        {
            RuleFor(x => x.Reason).NotEmpty().WithMessage("Lý do từ chối không được để trống.");
        }
    }

    public class AcceptAllProposalsValidator : AbstractValidator<AcceptAllProposalsRequest>
    {
        public AcceptAllProposalsValidator()
        {
            RuleFor(x => x.TripId).NotEmpty();
        }
    }
}
